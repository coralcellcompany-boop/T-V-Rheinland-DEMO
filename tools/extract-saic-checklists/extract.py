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
import bisect, html, json, re, subprocess, sys

# Horizontal boundary (PDF points) between the Acceptance-Criteria column (left) and
# the Reference column (right). Item numbers sit at xMin ~51-78; a full criteria line
# can reach ~294; the reference column proper begins at xMin ~313. 305 separates them.
REF_X = 305.0
# Item numbers live in the far-left column; this guards against "7.030"-style tokens
# (part of a "GI 7.030" reference) being mistaken for item numbers.
ITEMNO_MAX_X = 150.0
# The far-left item-number column proper. Item/section numbers print at xMin ~50.8;
# criteria text (incl. bullets and standalone digits like "6 randomly broken wires")
# is indented to xMin >= 72. Only a number-form token at/left of this boundary is the
# row's item-number marker; a bare digit further right is criteria text, never a marker.
NUMBER_COL_MAX_X = 70.0
# Left-column lines that wrap within one item are spaced ~9pt apart; a new item's first
# line opens a row gap of ~20pt or more. Any vertical gap larger than this between two
# adjacent left-column text lines therefore marks an item boundary (used to find the
# TOP of an item's text block when its number is printed mid-block, e.g. SAIC-U-7007
# wire-rope item 2.52).
LINE_GAP = 15.0

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

    # Per-page sorted list of distinct left-column TEXT-line y-values (criteria words
    # only, excluding the item-number markers themselves). Used to anchor each item by
    # the TOP of its text block rather than by its number's y: a number can be printed
    # several lines INSIDE its own multi-line criteria (SAIC-U-7007 item 2.52), so the
    # raw number-y would steal the lead-in lines from the previous item and leave a
    # fragment glued to the next. We instead grow each item's block upward over
    # contiguous wrapped lines (gap <= LINE_GAP) and stop at the first row gap.
    anchor_pos = {(pg, round(y, 1)) for pg, y, _k, _p in anchors}
    text_lines = {}
    for wpg, wy, wx, wt in ws:
        if wx >= REF_X:
            continue
        if wt in NOISE_TOKENS:
            continue
        ry = round(wy, 1)
        # A number-form token in the far-left column is the row marker, not body text;
        # do not let it seed a text line (it shares the y of its own first criteria
        # line anyway, so nothing is lost).
        if wx <= NUMBER_COL_MAX_X and (ITEMNO.match(wt) or SECTIONNO.match(wt)):
            continue
        text_lines.setdefault(wpg, set()).add(ry)
    for pg in text_lines:
        text_lines[pg] = sorted(text_lines[pg])

    def block_top(pg, y):
        """Top y of the contiguous left-column text block containing anchor (pg, y).

        Walk upward through adjacent text lines while each step is a within-item wrap
        (gap <= LINE_GAP); stop at the first larger gap (a new row) or the page top.
        """
        lines = text_lines.get(pg, [])
        if not lines:
            return y
        # Highest text line at or above the anchor's number y (the number's own line).
        i = bisect.bisect_right(lines, y + 0.5) - 1
        if i < 0:
            # No text line at/above the number (e.g. number floats above its text);
            # fall back to the number's own y.
            return y
        top = lines[i]
        while i - 1 >= 0 and (lines[i] - lines[i - 1]) <= LINE_GAP:
            i -= 1
            top = lines[i]
        return top

    block_tops = [block_top(pg, y) for pg, y, _k, _p in anchors]

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
        # CRITERIA band: anchor by the TOP of this item's own text block (block_tops[idx])
        # and run until the next anchor's text-block top on the same page. Using block
        # tops (not the raw number y, nor the midpoint to it) keeps every wrapped line
        # with its owning item even when a number is printed several lines inside its
        # block (SAIC-U-7007 wire-rope item 2.52).
        ctop = block_tops[idx] - 0.5
        cbottom = float("inf")
        if idx + 1 < n and anchors[idx + 1][0] == pg:
            cbottom = block_tops[idx + 1] - 0.5
        # REFERENCE band: the reference standard prints ~4-5pt ABOVE its row's number and
        # always on the number's row (it never floats mid-block the way criteria can), so
        # we keep the original robust midpoint-between-consecutive-number-y bands for the
        # right column. This avoids block-top criteria boundaries (which sit just below a
        # criteria line) from cutting a reference into the previous row.
        rtop = -float("inf")
        if idx - 1 >= 0 and anchors[idx - 1][0] == pg:
            rtop = (anchors[idx - 1][1] + y) / 2.0
        rbottom = float("inf")
        if idx + 1 < n and anchors[idx + 1][0] == pg:
            rbottom = (y + anchors[idx + 1][1]) / 2.0
        # Clamp both bands to the checklist-end heading (REMARKS / REFERENCE DOCUMENTS).
        if pg in end_y:
            if y >= end_y[pg]:
                continue
            cbottom = min(cbottom, end_y[pg])
            rbottom = min(rbottom, end_y[pg])

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
            # Right column uses the reference band; left column uses the criteria band.
            if wx >= REF_X:
                if wy < rtop or wy >= rbottom:
                    continue
            else:
                if wy < ctop or wy >= cbottom:
                    continue
            # Skip ONLY a true item/section-number MARKER: a number-form token sitting
            # in the far-left number column (xMin <= NUMBER_COL_MAX_X). A bare integer
            # in criteria text (e.g. "6 randomly broken wires") is indented to x >= 72
            # and must be preserved -- dropping it silently corrupts safety-critical
            # acceptance numbers. We never treat a bare integer as a marker; only the
            # full "N.M" item form or an "N"/"N.0" section form in the number column.
            if wx <= NUMBER_COL_MAX_X and (ITEMNO.match(wt) or SECTIONNO.match(wt)):
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
