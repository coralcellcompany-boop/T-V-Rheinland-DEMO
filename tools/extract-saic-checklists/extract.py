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

# ---------------------------------------------------------------------------
# Column geometry is DERIVED PER PDF, not hardcoded.
#
# Every "SAIC-U-70##" checklist shares one Aramco template, but the templates are
# not laid out at identical coordinates: the whole grid is shifted right by ~20-30
# points and down by a variable header height from file to file (e.g. SAIC-U-7007
# prints the item-number column at xMin ~51 with the column-header row ending at
# y~187, while SAIC-U-7018 prints the number column at xMin ~80 with its header row
# ending at y~168). Hardcoding the 7007 coordinates (REF_X=305, BODY_TOP=195, ...)
# silently produced 0 sections / merged items on the shifted templates.
#
# Instead we anchor every threshold to the one landmark that is present, identical
# in structure, and easy to locate on every page of every file: the column-header
# row "ITEM | ACCEPTANCE CRITERIA | REFERENCE". Its three word x-origins and its
# bottom y give us the grid origin; the offsets below were validated on 7007 and
# expressed RELATIVE to that header so they translate with each template's shift.
#
# 7007 reference header: ITEM.xMin=48.84  ACCEPTANCE.xMin=147.24  REFERENCE.xMin=323.52
#                        header bottom yMax=187.06
# and the offsets reproduce the original 7007-validated constants (REF_X is the lone
# exception, see below):
#   REF_X            = REFERENCE.xMin - 13.50  -> 310.0  (was 305.0; see note)
#   NUMBER_COL_MAX_X = ITEM.xMin      + 21.16  ->  70.0
#   ITEMNO_MAX_X     = ACCEPTANCE.xMin + 2.76  -> 150.0
#   BODY_TOP         = header.yMax    +  7.94  -> 195.0
#
# REF_X note: the column boundary sits between the right edge of wrapped criteria and
# the left edge of the reference text. Measuring both columns across the family, the
# reference text never begins left of REFERENCE.xMin - 10 (e.g. 7007 "SAES-B067" @313,
# "Sec" @316 vs header @323.5), while criteria occasionally wraps to REFERENCE.xMin - 18
# (e.g. 7009 item 2.2 "...broken wires |in| 1 lay" with "in" @305 vs header @323.8). The
# original 7007-only constant of 18.52 therefore swallowed that "in" into the reference
# column on the tighter-wrapping templates (dropping a criteria word AND prepending a
# spurious word to the reference). A margin of 13.5 (REF_X~310) sits in the safe overlap
# for every file and leaves 7007 byte-identical (verified) since no 7007 body criteria
# word reaches x>=306 and no 7007 reference word starts at x<313.
REF_X_FROM_REFHDR = 13.50
NUMBERCOL_FROM_ITEMHDR = 21.16
ITEMNOMAX_FROM_ACCHDR = 2.76
BODYTOP_FROM_HDR = 7.94

# Left-column lines that wrap within one item are spaced ~9pt apart; a new item's first
# line opens a row gap of ~20pt or more. Any vertical gap larger than this between two
# adjacent left-column text lines therefore marks an item boundary (used to find the
# TOP of an item's text block when its number is printed mid-block, e.g. SAIC-U-7007
# wire-rope item 2.52). This is a font-leading quantity, not a column origin, so it is
# shared across the (single-template) family and does not need per-file derivation.
LINE_GAP = 15.0

# Footer furniture ("Saudi Aramco: Company General Use") sits at y~757-760 on every
# page. BODY_BOTTOM is the body clamp; the default 760 keeps the two-level files
# byte-identical to their verified extraction (some of those, e.g. 7002/7016, already
# carry a trailing footer fragment on their last item, and the mandate is to NOT touch
# those files). For 3-level files we lower the clamp to BODY_BOTTOM_LEAF (=750) so the
# footer can never bleed into the final per-page leaf's open-ended downward band; the
# only words in 745-762 in every file are that footer line, so nothing real is clipped.
BODY_BOTTOM = 760.0
BODY_BOTTOM_LEAF = 750.0

# Per-PDF derived geometry, filled by geometry() before parsing. Module-level so the
# helper functions (which the parser closes over) can read them without threading args.
REF_X = 305.0
ITEMNO_MAX_X = 150.0
NUMBER_COL_MAX_X = 70.0
BODY_TOP = 195.0          # fallback / page-0 default
PAGE_BODY_TOP = {}        # per-page BODY_TOP (page index -> y), filled by geometry()

WORD_RE = re.compile(
    r'<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)"[^>]*>(.*?)</word>'
)
PAGE_RE = re.compile(r"<page ")
# A checklist item number is "N.M" with a ONE- or TWO-digit minor part. Aramco
# reference citations that also leak into the left column on the signature/
# reference-documents page use a THREE-digit fractional ("GI 7.030", "GI 7.025"),
# so restricting the minor to 1-2 digits keeps those references from being mistaken
# for items (they would otherwise appear as phantom items with empty criteria).
ITEMNO = re.compile(r"^\d+\.\d{1,2}$")
# A 3-level leaf number "N.M.K" (e.g. "1.2.21", "2.3.17"). Six of the eighteen SAIC
# checklists (7003/7004/7005/7008/7010/7018) nest every leaf inspection check one
# level deeper: a top-level section "N", a group "N.M" (a sub-heading row with a
# title and no reference), and the actual checks "N.M.K". The other twelve files are
# strictly two-level. We auto-detect 3-level layout per file (see leaf3_present) by
# the presence of N.M.K tokens IN THE NUMBER COLUMN -- the twelve two-level files do
# contain N.M.K-shaped tokens, but only as wrapped reference citations far right in
# the REFERENCE column (e.g. 7006 "6.1.6.3.1", 7013 "4.12.1"), never in the number
# column, so the column guard keeps their behaviour byte-identical.
LEAFNO = re.compile(r"^\d+\.\d{1,2}\.\d{1,3}$")
SECTIONNO = re.compile(r"^(\d+)(?:\.0)?$")
# All-caps section heading (e.g. "GENERAL REQUIREMENTS", "INSPECTION POINTS").
UPPER_TITLE = re.compile(r"^[A-Z][A-Z0-9 &/().,'\-]{3,}$")
# Title-case section heading (e.g. "General", "External Inspection"): every significant
# word is Capitalised; short connectives (of/and/the/for/&) may stay lowercase.
TITLE_CASE = re.compile(r"^[A-Z][a-zA-Z]+(?: (?:[A-Z][a-zA-Z]*|of|and|the|for|to|&))*$")
TITLE_CONNECTIVES = {"of", "and", "the", "for", "to", "&"}


def is_section_title(s):
    """True if `s` reads as a section HEADING, not item criteria text.

    Only reached for a bare-integer token that is already confirmed to live in the
    far-left NUMBER column (the caller guards on x), so `s` is the text printed beside
    a section number, never the criteria of an item (items are "N.M", handled
    separately). Section headings across the SAIC family come in three casings:
      * ALL-CAPS         "GENERAL REQUIREMENTS", "INSPECTION POINTS:"
      * Title-Case       "General", "External Inspection"
      * Sentence-case    "Load chain, Rope and Hook Block", "Rope drum"
    We therefore accept any short phrase that opens with a capital and is not an
    obvious sentence continuation; we reject long phrases and trailing periods
    (full sentences) and phrases that open lowercase (a wrapped criteria fragment).
    """
    s = s.strip().rstrip(":").strip()
    words_ = s.split()
    if len(s) < 3 or not (1 <= len(words_) <= 8):
        return False
    if s.endswith("."):
        return False
    if not s[0].isupper():
        return False
    if UPPER_TITLE.match(s) or TITLE_CASE.match(s):
        return True
    # Sentence-case heading: first word capitalised and not a connective.
    return words_[0][:1].isupper() and words_[0].lower() not in TITLE_CONNECTIVES

# Lines (joined criteria) that are pure page furniture / form fields and never item text.
NOISE_TOKENS = {
    "ITEM", "No.", "ACCEPTANCE", "CRITERIA", "REFERENCE", "PASS", "FAIL", "N/A",
    "Remark", "#",
}
END_TITLES = ("REMARKS", "REFERENCE DOCUMENTS")

# Words that belong to the repeated per-page top furniture (the equipment-info form and
# the column header). The bottom of the lowest such word on a page marks the bottom of
# the furniture for THAT page, which is how BODY_TOP is derived per page: the column
# header repeats on every page in some templates (SAIC-U-7007) but only on page 1 in
# others (SAIC-U-7002), where continuation pages instead end their furniture at the
# "REQUESTED INSPECTION DATE ... TASK COMPLETED DATE" form row. Anchoring BODY_TOP to
# whichever furniture row is lowest on each page keeps a body item that starts high on a
# header-less continuation page (e.g. 7002 item 2.11 at y~228) from being clipped.
FURNITURE_WORDS = {
    "REFERENCE", "ACCEPTANCE", "ITEM",            # column header (present some pages)
    "REQUESTED", "RFI", "TASK",                   # last equipment-info form row
}


def raw_words(pdf):
    """Return [(page, yMin, xMin, yMax, text)] for EVERY word (no furniture filter)."""
    try:
        proc = subprocess.run(
            ["pdftotext", "-bbox-layout", pdf, "-"], capture_output=True, text=True, check=True
        )
    except FileNotFoundError:
        sys.exit("pdftotext not found - install poppler (brew install poppler)")
    except subprocess.CalledProcessError as e:
        sys.exit(f"pdftotext failed (exit {e.returncode}):\n{e.stderr}")
    out = []
    page = -1
    for ln in proc.stdout.splitlines():
        if PAGE_RE.search(ln):
            page += 1
            continue
        m = WORD_RE.search(ln)
        if not m:
            continue
        text = html.unescape(m.group(5)).strip()
        if text:
            out.append((page, float(m.group(2)), float(m.group(1)), float(m.group(4)), text))
    return out


def _valley_ref_x(raw, acc_x, ref_x):
    """Find the criteria|reference column boundary as the empty x-valley between them.

    Build a 1px histogram of body word LEFT edges over [acc_x+30, ref_x+8] and slide a
    5px window across it; the boundary is the lowest-density window that is immediately
    followed (to its right) by a dense cluster (the reference column). Returns the window
    centre x, or None if no clear right cluster is found (caller keeps header REF_X).
    """
    lo = acc_x + 30.0
    hi = ref_x + 8.0
    hist = {}
    for _pg, y, x, _ymax, _t in raw:
        if 260.0 < y < BODY_BOTTOM and lo <= x < hi:
            hist[round(x)] = hist.get(round(x), 0) + 1
    best = None  # (window_count, centre_x)
    w = int(lo) + 2
    while w < int(hi) - 2:
        win = sum(hist.get(i, 0) for i in range(w - 2, w + 3))
        right = sum(hist.get(i, 0) for i in range(w + 3, w + 13))
        if right >= 20 and (best is None or win < best[0]):
            best = (win, float(w))
        w += 1
    return best[1] if best else None


def geometry(raw):
    """Derive REF_X / ITEMNO_MAX_X / NUMBER_COL_MAX_X / BODY_TOP from the column header.

    Find the "ITEM | ACCEPTANCE | REFERENCE" header row (same on every page; first
    occurrence is enough) and set the module-level thresholds RELATIVE to it so they
    track each template's horizontal shift / header height. Falls back to the
    7007-validated absolute defaults if the header cannot be located.
    """
    global REF_X, ITEMNO_MAX_X, NUMBER_COL_MAX_X, BODY_TOP, PAGE_BODY_TOP, BODY_BOTTOM
    BODY_BOTTOM = 760.0  # reset per run (module global is mutated below for 3-level files)
    item_x = acc_x = ref_x = hdr_ymax = None
    for pg, y, x, ymax, t in raw:
        if y > 260:  # header always sits in the top band of page 1
            continue
        if t == "ITEM" and item_x is None:
            item_x, hdr_ymax = x, ymax
        elif t == "ACCEPTANCE" and acc_x is None:
            acc_x = x
        elif t == "REFERENCE" and ref_x is None:
            ref_x = x
        if item_x is not None and acc_x is not None and ref_x is not None:
            break
    if None in (item_x, acc_x, ref_x, hdr_ymax):
        sys.exit("geometry: could not locate ITEM/ACCEPTANCE/REFERENCE column header")
    REF_X = ref_x - REF_X_FROM_REFHDR
    NUMBER_COL_MAX_X = item_x + NUMBERCOL_FROM_ITEMHDR
    ITEMNO_MAX_X = acc_x + ITEMNOMAX_FROM_ACCHDR
    BODY_TOP = hdr_ymax + BODYTOP_FROM_HDR

    # 3-level files only: the reference column on these templates wraps further LEFT than
    # the header-relative REF_X (e.g. 7005 wraps standards to x~245 while the header sits
    # at x~267, REF_X~253), so a header-derived boundary swallows the left edge of the
    # reference into the criteria (and vice-versa). The criteria and reference clusters
    # are separated by a near-empty vertical "valley" in the body word x-distribution; we
    # set REF_X to that valley. This runs ONLY when N.M.K leaf tokens exist in the number
    # column, so the twelve two-level files keep their header-derived REF_X byte-for-byte.
    has_leaf = any(
        LEAFNO.match(t) and x <= NUMBER_COL_MAX_X
        for _pg, y, x, _ymax, t in raw
        if y <= BODY_BOTTOM
    )
    if has_leaf:
        BODY_BOTTOM = BODY_BOTTOM_LEAF
        v = _valley_ref_x(raw, acc_x, ref_x)
        if v is not None:
            REF_X = v

    # Per-page BODY_TOP: the bottom (max yMax) of the lowest top-furniture word on each
    # page, plus the same margin. On pages that repeat the column header this reproduces
    # the header-relative BODY_TOP; on header-less continuation pages it falls back to
    # the equipment-info form's last row so high-starting body items are not clipped.
    PAGE_BODY_TOP = {}
    for pg, y, x, ymax, t in raw:
        if y > 260 or t not in FURNITURE_WORDS:
            continue
        if ymax > PAGE_BODY_TOP.get(pg, 0.0):
            PAGE_BODY_TOP[pg] = ymax
    for pg in list(PAGE_BODY_TOP):
        PAGE_BODY_TOP[pg] += BODYTOP_FROM_HDR


def page_body_top(pg):
    """BODY_TOP for a given page (per-page furniture bottom, else the global default)."""
    return PAGE_BODY_TOP.get(pg, BODY_TOP)


def words(pdf=None, raw=None):
    """[(page, yMin, xMin, text)] for body words, dropping header/footer furniture.

    Pass the cached `raw` list (so geometry() has already run); `pdf` is accepted for
    backward compatibility (re-reads + re-derives geometry on its own).
    """
    if raw is None:
        raw = raw_words(pdf)
        geometry(raw)
    return [
        (pg, y, x, t)
        for pg, y, x, ymax, t in raw
        if page_body_top(pg) <= y <= BODY_BOTTOM
    ]


def norm(s):
    return re.sub(r"\s+", " ", s).strip()


def is_marker(x, t):
    """True if (x, t) is a number-column row marker: a section "N"/"N.0", an item
    "N.M", or (3-level files) a leaf "N.M.K" token sitting in the far-left number
    column. Markers must never be emitted as criteria/reference body text."""
    return x <= NUMBER_COL_MAX_X and (
        SECTIONNO.match(t) or ITEMNO.match(t) or LEAFNO.match(t)
    )


def parse(pdf):
    raw = raw_words(pdf)
    geometry(raw)
    ws = words(raw=raw)

    # Auto-detect 3-level (N.M.K) layout: TRUE only when leaf tokens appear IN the
    # number column. The twelve two-level files have no such tokens there (their
    # N.M.K-shaped strings are wrapped reference citations far right), so they take
    # the unchanged two-level path and stay byte-identical.
    leaf_groups = set()  # group numbers "N.M" that own at least one "N.M.K" leaf
    for pg, y, x, t in ws:
        if LEAFNO.match(t) and x <= NUMBER_COL_MAX_X:
            leaf_groups.add(".".join(t.split(".")[:2]))
    leaf3 = bool(leaf_groups)

    def row_title(i, pg, y):
        """Collect the heading words on the same row as the section number.

        The section title sits just right of the number in the criteria column. It is
        NOT safe to scan only forward from the number's index: in several templates
        pdftotext emits the title words BEFORE the number in document order (e.g.
        SAIC-U-7018 "INSPECTION ITEMS" then "2"). So gather every body word sharing
        this row (|dy| small), left of the reference column, except the number itself,
        then order them left-to-right.
        """
        parts = []
        for pg2, y2, x2, t2 in ws:
            if pg2 != pg or abs(y2 - y) > 3:
                continue
            if x2 > REF_X:
                continue
            if x2 <= ITEMNO_MAX_X and (
                SECTIONNO.match(t2) or ITEMNO.match(t2) or LEAFNO.match(t2)
            ):
                continue  # skip the number marker(s) themselves
            parts.append((x2, t2))
        parts.sort()
        return norm(" ".join(t for _x, t in parts))

    # 1) Locate anchors: section headers and item-number markers, in reading order.
    #    An anchor is (page, y, kind, payload). kind in {"section","item"}.
    #    A number like "4.0" is ambiguous (looks like an item) so we test for a section
    #    title FIRST: a leading number/"N.0" followed on the same row by an UPPER-CASE
    #    title means a section header; otherwise an "N.M" number is a checklist item.
    #
    #    3-level files (leaf3): a group "N.M" that owns "N.M.K" leaves becomes a SECTION
    #    (its title prefixed with the enclosing top-level section name for readability),
    #    each leaf "N.M.K" becomes an ITEM under it, and a standalone "N.M" (no leaves)
    #    stays a normal ITEM under its top-level section -- exactly the representation the
    #    flattening C# loader expects. Two-level files are untouched.
    top_title = {}  # top-level section number -> its label (for prefixing group titles)
    anchors = []
    for i, (pg, y, x, t) in enumerate(ws):
        if x > ITEMNO_MAX_X:
            continue
        ms = SECTIONNO.match(t)
        # A bare-integer / "N.0" token is a SECTION number only when it sits in the
        # far-left NUMBER column (x <= NUMBER_COL_MAX_X). A bare integer indented into
        # the criteria column is body text (e.g. the "15" of "15 times the nominal
        # diameter", or "2720 kg or more") and must not seed a section.
        if ms and x <= NUMBER_COL_MAX_X:
            title = row_title(i, pg, y)
            if title and is_section_title(title):
                sectitle = title.rstrip(":").strip()
                if leaf3:
                    top_title[ms.group(1)] = sectitle
                anchors.append((pg, y, "section", (ms.group(1), sectitle)))
                continue
        # 3-level group row "N.M" with leaf children: emit as a SECTION whose title is the
        # group label (e.g. "2 INSPECTION POINTS / BRIDGE COMPONENTS"). Its leaves follow.
        if leaf3 and ITEMNO.match(t) and x <= NUMBER_COL_MAX_X and t in leaf_groups:
            gtitle = row_title(i, pg, y)
            top = top_title.get(t.split(".")[0], "")
            label = f"{top.split('/')[0].strip()} / {gtitle}".strip(" /") if top else gtitle
            anchors.append((pg, y, "section", (t, label or gtitle or t)))
            continue
        # 3-level leaf row "N.M.K": a checklist ITEM (anchored by its leaf number).
        if leaf3 and LEAFNO.match(t) and x <= NUMBER_COL_MAX_X:
            anchors.append((pg, y, "item", t))
            continue
        # An item-number marker likewise lives in the far-left NUMBER column. A decimal
        # token indented into the criteria column is body text, not a marker (e.g.
        # SAIC-U-7017 item 2.5's criteria contains "0.5 in. (13 mm)" at x~91, which must
        # stay as criteria rather than spawning a phantom "0.5" item that steals the row).
        # In 3-level files a leaf-bearing "N.M" was already consumed above as a section,
        # so only standalone "N.M" (no leaves) reaches here -> a normal item.
        if ITEMNO.match(t) and x <= NUMBER_COL_MAX_X:
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
        if is_marker(wx, wt):
            continue
        text_lines.setdefault(wpg, set()).add(ry)
    for pg in text_lines:
        text_lines[pg] = sorted(text_lines[pg])

    def block_top(pg, y, floor):
        """Top y of the contiguous left-column text block containing anchor (pg, y).

        Walk upward through adjacent text lines while each step is a within-item wrap
        (gap <= LINE_GAP); stop at the first larger gap (a new row), the page top, or
        `floor` (the previous anchor's y on this page). The floor is essential when the
        body is set with tight, uniform line spacing (e.g. SAIC-U-7010): there the gap
        between one item's last criteria line and the next item's first line is itself
        <= LINE_GAP, so a pure gap walk would merge the previous item's trailing line
        into this item. An item's block can never legitimately start above the previous
        anchor, so we clamp there.
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
        while i - 1 >= 0 and (lines[i] - lines[i - 1]) <= LINE_GAP and lines[i - 1] > floor:
            i -= 1
            top = lines[i]
        return top

    block_tops = []
    for idx, (pg, y, _k, _p) in enumerate(anchors):
        # Floor = previous anchor's y on the SAME page (else -inf). Keeps the upward
        # walk from crossing into the previous item/section's rows.
        floor = -float("inf")
        if idx - 1 >= 0 and anchors[idx - 1][0] == pg:
            floor = anchors[idx - 1][1]
        block_tops.append(block_top(pg, y, floor))

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
            # full "N.M" item form, an "N"/"N.0" section form, or (3-level files) an
            # "N.M.K" leaf form in the number column.
            if is_marker(wx, wt):
                continue
            if wt in NOISE_TOKENS:
                continue
            item_words.append((wy, wx, wt))

    flush_item()
    return [s for s in sections if s["items"]]


def main():
    if len(sys.argv) < 5:
        sys.exit(__doc__)
    pdf, num, title, out = sys.argv[1:5]
    doc = {"saicNumber": num, "title": title, "sections": parse(pdf)}
    with open(out, "w") as f:
        json.dump(doc, f, indent=2, ensure_ascii=False)
    n = sum(len(s["items"]) for s in doc["sections"])
    print(f"{num}: {len(doc['sections'])} sections, {n} items -> {out}")


if __name__ == "__main__":
    main()
