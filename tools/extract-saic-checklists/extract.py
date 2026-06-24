#!/usr/bin/env python3
"""Extract SAIC inspection-checklist items from the source PDFs into catalog JSON.

The SAIC "SAIC-U-70##" checklists are two-column grids. Each row holds an item:
  - left column:  item number "N.M" + a (possibly multi-line) Acceptance Criteria
  - right column: the Reference standard (also frequently multi-line, e.g.
                  "ASME B30.5" on one line and "Sec:5-2.1.5" on the next)
Section headers (e.g. "1   GENERAL REQUIREMENTS", sometimes written "4.0   CRAWLER
CRANE") sit in the left column spanning the full width with no reference.

`pdftotext -layout` collapses the grid by Y-then-X, which interleaves the criteria
of one item with the reference of its neighbour and bleeds text across items. To
keep items separate we instead read WORD BOUNDING BOXES (`pdftotext -bbox-layout`):
every item number defines the top (yMin) of a vertical band that runs until the next
item/section anchor, and every word is assigned to the band it falls in. Within a
band, words left of REF_X are criteria, words at/right of REF_X are the reference.

Usage: python3 extract.py "<SAIC-U-7007 - ...pdf>" SAIC-U-7007 "Mobile / Crawler Cranes" out.json
"""
import html, json, re, subprocess, sys

# Horizontal boundary (PDF points) between the Acceptance-Criteria column (left) and
# the Reference column (right). Item numbers sit at xMin ~51-78; a full criteria line
# can reach ~294; the reference column proper begins at xMin ~313. 305 separates them.
REF_X = 305.0
# Item numbers live in the far-left column; this guards against "7.030"-style tokens
# (part of a "GI 7.030" reference) being mistaken for item numbers.
ITEMNO_MAX_X = 150.0

# Every page repeats the same furniture: a form/header block at the top (ending with
# the "ITEM | ACCEPTANCE CRITERIA | REFERENCE" column header at y~179-191) and a
# "Saudi Aramco: Company General Use" footer near the bottom (y~769). The checklist
# body always lives between these; words outside this Y window are dropped as furniture.
BODY_TOP = 195.0
BODY_BOTTOM = 760.0

WORD_RE = re.compile(
    r'<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)"[^>]*>(.*?)</word>'
)
PAGE_RE = re.compile(r"<page ")
ITEMNO = re.compile(r"^\d+\.\d+$")
SECTIONNO = re.compile(r"^(\d+)(?:\.0)?$")
UPPER_TITLE = re.compile(r"^[A-Z][A-Z0-9 &/().,'\-]{3,}$")

# Lines (joined criteria) that are pure page furniture / form fields and never item text.
NOISE_TOKENS = {
    "ITEM", "No.", "ACCEPTANCE", "CRITERIA", "REFERENCE", "PASS", "FAIL", "N/A",
    "Remark", "#",
}
END_TITLES = ("REMARKS", "REFERENCE DOCUMENTS")


def words(pdf):
    """Yield (page_index, yMin, xMin, text) for every word, in document order."""
    xml = subprocess.run(
        ["pdftotext", "-bbox-layout", pdf, "-"], capture_output=True, text=True, check=True
    ).stdout
    page = -1
    for ln in xml.splitlines():
        if PAGE_RE.search(ln):
            page += 1
            continue
        m = WORD_RE.search(ln)
        if not m:
            continue
        x = float(m.group(1))
        y = float(m.group(2))
        # Drop repeated per-page header/form and footer furniture.
        if y < BODY_TOP or y > BODY_BOTTOM:
            continue
        text = html.unescape(m.group(5))
        if text.strip():
            yield page, y, x, text.strip()


def norm(s):
    return re.sub(r"\s+", " ", s).strip()


def parse(pdf):
    ws = list(words(pdf))

    def row_title(i, pg, y):
        """Collect the UPPER-CASE title words on the same row, right of the number."""
        parts = []
        for pg2, y2, x2, t2 in ws[i + 1 : i + 12]:
            if pg2 != pg or abs(y2 - y) > 3:
                break
            if x2 > REF_X:
                continue
            parts.append(t2)
        return norm(" ".join(parts))

    # 1) Locate anchors: section headers and item-number markers, in reading order.
    #    An anchor is (page, y, kind, payload). kind in {"section","item"}.
    #    A number like "4.0" is ambiguous (looks like an item) so we test for a section
    #    title FIRST: a leading number/"N.0" followed on the same row by an UPPER-CASE
    #    title means a section header; otherwise an "N.M" number is a checklist item.
    anchors = []
    for i, (pg, y, x, t) in enumerate(ws):
        if x > ITEMNO_MAX_X:
            continue
        ms = SECTIONNO.match(t)
        if ms:
            title = row_title(i, pg, y)
            if title and UPPER_TITLE.match(title):
                anchors.append((pg, y, "section", (ms.group(1), title)))
                continue
        if ITEMNO.match(t):
            anchors.append((pg, y, "item", t))

    # Stop at end-of-checklist marker (REMARKS / REFERENCE DOCUMENTS heading).
    end_y = {}
    for pg, y, x, t in ws:
        if t.rstrip(":").upper() in END_TITLES and x < REF_X:
            end_y.setdefault(pg, y)

    # 2) Order anchors by (page, y); build vertical bands. Each band runs from its
    #    anchor's y to the next anchor's y (same page) or page end / checklist end.
    anchors.sort(key=lambda a: (a[0], a[1]))

    sections = []
    cur = None
    item = None
    item_words = []  # (y, x, text) collected for the open item band

    def flush_item():
        nonlocal item, item_words
        if item is None:
            return
        crit, ref = [], []
        for y, x, t in sorted(item_words, key=lambda w: (w[0], w[1])):
            (ref if x >= REF_X else crit).append(t)
        item["acceptanceCriteria"] = norm(" ".join(crit))
        item["referenceStandard"] = norm(" ".join(ref))
        if cur is not None:
            cur["items"].append(item)
        item = None
        item_words = []

    n = len(anchors)
    for idx in range(n):
        pg, y, kind, payload = anchors[idx]
        # An item number's baseline sits a few points BELOW the top of its own first
        # criteria line, and the next item's criteria likewise floats above its number.
        # So we split bands at the MIDPOINT between consecutive anchors' y-values rather
        # than at the raw number y, which keeps each row's wrapped lines with the right
        # item. Top = midpoint with previous anchor (same page); bottom = midpoint with
        # next anchor (same page) or +inf (page end).
        top = -float("inf")
        if idx - 1 >= 0 and anchors[idx - 1][0] == pg:
            top = (anchors[idx - 1][1] + y) / 2.0
        bottom = float("inf")
        if idx + 1 < n and anchors[idx + 1][0] == pg:
            bottom = (y + anchors[idx + 1][1]) / 2.0
        # Clamp to the checklist-end heading (REMARKS / REFERENCE DOCUMENTS) on this page.
        if pg in end_y:
            if y >= end_y[pg]:
                continue
            bottom = min(bottom, end_y[pg])

        if kind == "section":
            flush_item()
            cur = {"no": payload[0], "title": payload[1], "items": []}
            sections.append(cur)
            continue

        # kind == "item"
        flush_item()
        if cur is None:
            continue
        item = {"itemNo": payload, "acceptanceCriteria": "", "referenceStandard": ""}
        # collect words on this page within [top, bottom) belonging to the band, but not
        # the anchor token itself and not other anchor tokens.
        for wpg, wy, wx, wt in ws:
            if wpg != pg:
                continue
            if wy < top or wy >= bottom:
                continue
            # skip the item-number token itself
            if wx <= ITEMNO_MAX_X and (ITEMNO.match(wt) or SECTIONNO.match(wt)):
                continue
            if wt in NOISE_TOKENS:
                continue
            item_words.append((wy, wx, wt))

    flush_item()
    return [s for s in sections if s["items"]]


def main():
    pdf, num, title, out = sys.argv[1:5]
    doc = {"saicNumber": num, "title": title, "sections": parse(pdf)}
    with open(out, "w") as f:
        json.dump(doc, f, indent=2, ensure_ascii=False)
    n = sum(len(s["items"]) for s in doc["sections"])
    print(f"{num}: {len(doc['sections'])} sections, {n} items -> {out}")


if __name__ == "__main__":
    main()
