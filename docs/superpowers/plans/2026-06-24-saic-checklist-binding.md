# SAIC Checklist Binding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a Blue Sticker inspector picks an equipment type on the Aramco Annex 1 form, load that equipment's real Saudi Aramco inspection checklist (`SAIC-U-70##`) into a fillable editor and store the filled result on the certificate.

**Architecture:** A backend catalog of the 18 SAIC checklists (items extracted from the source PDFs into JSON, embedded resources) plus a single authoritative `(Aramco category, equipment-type name) → SAIC number` mapping, served by one resolve endpoint. The Angular Aramco form emits its category/equipment-type selection; the certificate detail page resolves the checklist and renders it in the existing checklist editor inside the Blue Sticker section, persisting to the certificate's `ChecklistJson`.

**Tech Stack:** .NET (CQRS dispatcher, xUnit), ASP.NET controllers, Angular standalone components + signals, PrimeNG, `pdftotext` for extraction.

**Spec:** [2026-06-24-saic-checklist-binding-design.md](../specs/2026-06-24-saic-checklist-binding-design.md)

**Rollout:** Tasks 1–6 deliver the SAIC-U-7007 (Mobile Crane) pilot end-to-end. Tasks 7–9 extend to all 18 checklists and fix the category-only display lookup.

---

## File Structure

**Backend (new):**
- `src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists/*.json` — one catalog file per SAIC number (embedded resource).
- `src/TuvInspection.Contracts/BlueSticker/SaicChecklistDtos.cs` — `SaicChecklistDto`, `SaicChecklistItemDto`.
- `src/TuvInspection.Domain/BlueSticker/SaicChecklistMap.cs` — the `(category, type) → SAIC number` table.
- `src/TuvInspection.Application/BlueSticker/ResolveSaicChecklistQuery.cs` — query record.
- `src/TuvInspection.Infrastructure/BlueSticker/SaicChecklistCatalog.cs` — loads/caches JSON resources.
- `src/TuvInspection.Infrastructure/BlueSticker/ResolveSaicChecklistHandler.cs` — query handler.
- `src/TuvInspection.Api/Controllers/SaicChecklistsController.cs` — `GET /api/saic-checklists/resolve`.

**Backend (modified):**
- `src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj` — embed the JSON folder.
- `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs:39` — reuse the shared map (Task 9).

**Frontend (new):**
- `web/src/app/core/api/saic-checklists.api.ts` — API client + DTO interfaces.

**Frontend (modified):**
- `web/src/app/features/certificates/components/aramco-form.component.ts` — emit `(category, equipmentType)` selection.
- `web/src/app/features/certificates/pages/certificate-detail.page.ts` — SAIC checklist section in the Blue Sticker branch.

**Tooling (new):**
- `tools/extract-saic-checklists/extract.py` — deterministic PDF → catalog JSON extractor.

---

## Task 1: PDF extraction tool + SAIC-U-7007 catalog (pilot)

**Files:**
- Create: `tools/extract-saic-checklists/extract.py`
- Create: `src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists/SAIC-U-7007.json`

The catalog JSON shape (target for every checklist):

```json
{
  "saicNumber": "SAIC-U-7007",
  "title": "Mobile / Crawler Cranes",
  "sections": [
    {
      "no": "1",
      "title": "GENERAL REQUIREMENTS",
      "items": [
        { "itemNo": "1.1", "acceptanceCriteria": "Equipment documentation is available", "referenceStandard": "ASME B30.5 Sec:5-2.1.5" }
      ]
    }
  ]
}
```

- [ ] **Step 1: Write the extractor**

`tools/extract-saic-checklists/extract.py`:

```python
#!/usr/bin/env python3
"""Extract SAIC inspection-checklist items from the source PDFs into catalog JSON.

Usage: python3 extract.py "<SAIC-U-7007 - ...pdf>" SAIC-U-7007 "Mobile / Crawler Cranes" out.json
Run `pdftotext -layout` per page, then parse the item grid:
  - A section header row looks like `   1   GENERAL REQUIREMENTS` (int + title, no dot).
  - An item row starts with `N.M` (e.g. `1.1`, `2.10`); its acceptance criteria is the wrapped
    text up to the reference column; the reference standard is the trailing right-hand column.
Because pdftotext -layout preserves columns by spaces, we split each item block on 2+ spaces and
take the first chunk as criteria and the last non-empty chunk(s) as the reference. Multi-line
criteria/refs are joined. This is a best-effort parse — Task 1 Step 4 verifies the output by hand.
"""
import json, re, subprocess, sys

def lines(pdf):
    txt = subprocess.run(["pdftotext", "-layout", pdf, "-"],
                         capture_output=True, text=True, check=True).stdout
    return txt.splitlines()

SECTION = re.compile(r"^\s{2,}(\d+)\s{2,}([A-Z][A-Z0-9 &/().,'-]{4,})\s*$")
ITEM = re.compile(r"^\s*(\d+\.\d+)\s{2,}(.*)$")
NOISE = re.compile(r"Saudi Aramco|SAIC NUMBER|ACCEPTANCE CRITERIA|ISSUE DATE|^\s*#\s*$"
                   r"|PROJECT TITLE|EQUIPMENT ID|CAPACITY|REQUESTED INSPECTION|ITEM", re.I)

def parse(pdf):
    sections, cur, item = [], None, None
    def flush():
        nonlocal item
        if item: cur["items"].append(item); item = None
    for ln in lines(pdf):
        if NOISE.search(ln): continue
        ms = SECTION.match(ln)
        if ms:
            flush()
            cur = {"no": ms.group(1), "title": ms.group(2).strip(), "items": []}
            sections.append(cur); continue
        mi = ITEM.match(ln)
        if mi and cur is not None:
            flush()
            parts = re.split(r"\s{2,}", mi.group(2).strip())
            crit = parts[0].strip()
            ref = " ".join(p.strip() for p in parts[1:]).strip()
            item = {"itemNo": mi.group(1), "acceptanceCriteria": crit, "referenceStandard": ref}
            continue
        if item is not None and ln.strip() and cur is not None:
            # continuation line: append to criteria (or ref if it looks like a standard)
            extra = re.split(r"\s{2,}", ln.strip())
            item["acceptanceCriteria"] = (item["acceptanceCriteria"] + " " + extra[0]).strip()
            if len(extra) > 1:
                item["referenceStandard"] = (item["referenceStandard"] + " " + " ".join(extra[1:])).strip()
    flush()
    return [s for s in sections if s["items"]]

def main():
    pdf, num, title, out = sys.argv[1:5]
    doc = {"saicNumber": num, "title": title, "sections": parse(pdf)}
    with open(out, "w") as f: json.dump(doc, f, indent=2, ensure_ascii=False)
    n = sum(len(s["items"]) for s in doc["sections"])
    print(f"{num}: {len(doc['sections'])} sections, {n} items -> {out}")

if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Run the extractor for the pilot**

```bash
cd "/Volumes/development/Projects/Ahmed Gouz Eman"
SRC="SOFTWARE IMPLEMENTATION/Blue Sticker Inspection Service/SIAC 7001-7018"
mkdir -p src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists
python3 tools/extract-saic-checklists/extract.py \
  "$SRC/SAIC-U-7007 - MOBILE  CRAWLER CRANES-2023.pdf" \
  "SAIC-U-7007" "Mobile / Crawler Cranes" \
  src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists/SAIC-U-7007.json
```

Expected: prints `SAIC-U-7007: N sections, ~92 items -> ...`.

- [ ] **Step 3: Verify item count matches the PDF**

```bash
SRC="SOFTWARE IMPLEMENTATION/Blue Sticker Inspection Service/SIAC 7001-7018"
echo "PDF items:";  pdftotext -layout "$SRC/SAIC-U-7007 - MOBILE  CRAWLER CRANES-2023.pdf" - | grep -cE '^[[:space:]]*[0-9]+\.[0-9]+[[:space:]]'
echo "JSON items:"; python3 -c "import json;d=json.load(open('src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists/SAIC-U-7007.json'));print(sum(len(s['items']) for s in d['sections']))"
```

Expected: both print the same count (~92). If they differ, fix the parser before continuing.

- [ ] **Step 4: Hand-verify the pilot content (mandatory)**

Open `SAIC-U-7007.json` and spot-check the first 10 items of section 1 against the PDF text: each `acceptanceCriteria` reads as a full sentence (no truncation/merge of two items) and `referenceStandard` holds the standard (e.g. `ASME B30.5 Sec:5-2.1.5`), not inspection text. Fix the parser and re-run Steps 2–3 until correct. This is a compliance artifact — accuracy is required.

- [ ] **Step 5: Commit**

```bash
git add tools/extract-saic-checklists/extract.py \
  src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists/SAIC-U-7007.json
git commit -m "feat(saic): extract SAIC-U-7007 pilot checklist + extractor tool"
```

---

## Task 2: Backend catalog, mapping, resolve endpoint (pilot)

**Files:**
- Create: `src/TuvInspection.Contracts/BlueSticker/SaicChecklistDtos.cs`
- Create: `src/TuvInspection.Domain/BlueSticker/SaicChecklistMap.cs`
- Create: `src/TuvInspection.Application/BlueSticker/ResolveSaicChecklistQuery.cs`
- Create: `src/TuvInspection.Infrastructure/BlueSticker/SaicChecklistCatalog.cs`
- Create: `src/TuvInspection.Infrastructure/BlueSticker/ResolveSaicChecklistHandler.cs`
- Create: `src/TuvInspection.Api/Controllers/SaicChecklistsController.cs`
- Modify: `src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj`
- Test: `tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistMapTests.cs`
- Test: `tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistCatalogTests.cs`

- [ ] **Step 1: Write the DTOs**

`src/TuvInspection.Contracts/BlueSticker/SaicChecklistDtos.cs`:

```csharp
namespace TuvInspection.Contracts.BlueSticker;

/// <summary>A Saudi Aramco inspection checklist (SAIC-U-70##) resolved for an equipment type.
/// Items are flattened across the PDF's sections; section headers are carried so the UI can group.</summary>
public sealed record SaicChecklistDto(
    string SaicNumber,
    string Title,
    IReadOnlyList<SaicChecklistItemDto> Items);

public sealed record SaicChecklistItemDto(
    string ItemNo,
    string AcceptanceCriteria,
    string ReferenceStandard,
    string? SectionNo,
    string? SectionTitle);
```

- [ ] **Step 2: Write the failing map test**

`tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistMapTests.cs`:

```csharp
using TuvInspection.Domain.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class SaicChecklistMapTests
{
    [Theory]
    [InlineData("CR01", "Crawler Crane", "SAIC-U-7007")]
    [InlineData("CR04", "Floating Crane", "SAIC-U-7009")]
    [InlineData("CR04", "Tower Crane", "SAIC-U-7003")]
    [InlineData("CR11", "Jib Crane", "SAIC-U-7011")]
    public void Resolves_known_equipment_types(string cat, string type, string expected)
        => Assert.Equal(expected, SaicChecklistMap.Resolve(cat, type));

    [Fact]
    public void Unknown_type_returns_null()
        => Assert.Null(SaicChecklistMap.Resolve("CR01", "Nonexistent Crane"));
}
```

- [ ] **Step 3: Run it (fails — type missing)**

Run: `dotnet test tests/TuvInspection.UnitTests --filter SaicChecklistMapTests`
Expected: FAIL — `SaicChecklistMap` does not exist.

- [ ] **Step 4: Write the map (pilot rows + full table)**

`src/TuvInspection.Domain/BlueSticker/SaicChecklistMap.cs`:

```csharp
namespace TuvInspection.Domain.BlueSticker;

/// <summary>Authoritative (Aramco category short-code, equipment-type name) → SAIC-U-70##
/// inspection-checklist mapping. Source: "Aramco Category & Equipment Types.xlsx" (Blue Sticker
/// Services). Equipment-type names match the Aramco Annex 1 form's category dropdown verbatim.</summary>
public static class SaicChecklistMap
{
    private static readonly Dictionary<(string Cat, string Type), string> Map = new()
    {
        [("CR01", "Mobile Crane - All Terrain")] = "SAIC-U-7007",
        [("CR01", "Mobile Crane - Rough Terrain")] = "SAIC-U-7007",
        [("CR01", "Mobile Crane - Truck Mounted Crane")] = "SAIC-U-7007",
        [("CR01", "Mobile Crane - Boom Truck")] = "SAIC-U-7007",
        [("CR01", "Crawler Crane")] = "SAIC-U-7007",
        [("CR02", "Elevator")] = "SAIC-U-7005",
        [("CR02", "Escalator")] = "SAIC-U-7006",
        [("CR03", "Manlift - Boom Supported EWP")] = "SAIC-U-7013",
        [("CR03", "Scissor Lift - Self Propelled EWP")] = "SAIC-U-7014",
        [("CR03", "Manually Propelled EWP")] = "SAIC-U-7015",
        [("CR03", "Mast Climbing Personal Platform")] = "SAIC-U-7015",
        [("CR04", "Pedestal Crane")] = "SAIC-U-7018",
        [("CR04", "Pedestal Crane - Articulating Boom")] = "SAIC-U-7004",
        [("CR04", "Floating Crane - Articulating Boom")] = "SAIC-U-7004",
        [("CR04", "Floating Crane")] = "SAIC-U-7009",
        [("CR04", "Overhead Crane")] = "SAIC-U-7008",
        [("CR04", "Monorail Crane")] = "SAIC-U-7011",
        [("CR04", "Tower Crane")] = "SAIC-U-7003",
        [("CR04", "Portal Crane")] = "SAIC-U-7018",
        [("CR05", "Storage Retrieval Machine (SRM)")] = "SAIC-U-7012",
        [("CR06", "Articulating Boom Crane")] = "SAIC-U-7004",
        [("CR07", "Lifting Beam")] = "SAIC-U-7002",
        [("CR07", "Spreader Beam")] = "SAIC-U-7002",
        [("CR08", "Powered Platform / Sky Climber")] = "SAIC-U-7016",
        [("CR09", "Bucket Truck")] = "SAIC-U-7013",
        [("CR10", "Manbasket")] = "SAIC-U-7017",
        [("CR11", "Overhead Crane")] = "SAIC-U-7008",
        [("CR11", "Monorail Crane")] = "SAIC-U-7011",
        [("CR11", "Jib Crane")] = "SAIC-U-7011",
        [("CR12", "Side Boom Tractor")] = "SAIC-U-7010",
        [("CR13", "A-frame")] = "SAIC-U-7011",
        [("CR13", "Gantry Crane")] = "SAIC-U-7011",
        [("CR14", "Tower Crane")] = "SAIC-U-7003",
    };

    /// <summary>Resolve the SAIC number for a category + equipment-type name, or null if unmapped.</summary>
    public static string? Resolve(string? categoryShortCode, string? equipmentType)
    {
        if (string.IsNullOrWhiteSpace(categoryShortCode) || string.IsNullOrWhiteSpace(equipmentType))
            return null;
        return Map.TryGetValue((categoryShortCode.Trim(), equipmentType.Trim()), out var saic) ? saic : null;
    }

    /// <summary>All distinct SAIC numbers referenced by the mapping — used to assert catalog coverage.</summary>
    public static IReadOnlyCollection<string> AllSaicNumbers() => Map.Values.Distinct().ToList();
}
```

- [ ] **Step 5: Run the map test (passes)**

Run: `dotnet test tests/TuvInspection.UnitTests --filter SaicChecklistMapTests`
Expected: PASS.

- [ ] **Step 6: Embed the catalog JSON as resources**

In `src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj`, inside an `<ItemGroup>` (next to the existing `Annex1.docx` embedded resource):

```xml
<EmbeddedResource Include="BlueSticker\SaicChecklists\*.json" />
```

- [ ] **Step 7: Write the failing catalog test**

`tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistCatalogTests.cs`:

```csharp
using TuvInspection.Infrastructure.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class SaicChecklistCatalogTests
{
    [Fact]
    public void Loads_pilot_7007_with_items()
    {
        var doc = new SaicChecklistCatalog().Get("SAIC-U-7007");
        Assert.NotNull(doc);
        Assert.Equal("SAIC-U-7007", doc!.SaicNumber);
        Assert.True(doc.Items.Count >= 90, $"expected ~92 items, got {doc.Items.Count}");
        Assert.All(doc.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.AcceptanceCriteria)));
    }

    [Fact]
    public void Unknown_number_returns_null()
        => Assert.Null(new SaicChecklistCatalog().Get("SAIC-U-9999"));
}
```

- [ ] **Step 8: Run it (fails — catalog missing)**

Run: `dotnet test tests/TuvInspection.UnitTests --filter SaicChecklistCatalogTests`
Expected: FAIL — `SaicChecklistCatalog` does not exist.

- [ ] **Step 9: Write the catalog loader**

`src/TuvInspection.Infrastructure/BlueSticker/SaicChecklistCatalog.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>Loads the embedded SAIC checklist JSON resources and flattens their sections into the
/// resolve DTO. Results are cached per process — the catalog is immutable reference data.</summary>
public sealed class SaicChecklistCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SaicChecklistDto?> Cache = new();
    private static readonly Assembly Asm = typeof(SaicChecklistCatalog).Assembly;

    private sealed record RawDoc(string SaicNumber, string Title, List<RawSection> Sections);
    private sealed record RawSection(string No, string Title, List<RawItem> Items);
    private sealed record RawItem(string ItemNo, string AcceptanceCriteria, string ReferenceStandard);

    public SaicChecklistDto? Get(string saicNumber)
        => Cache.GetOrAdd(saicNumber, Load);

    private static SaicChecklistDto? Load(string saicNumber)
    {
        var name = Asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"SaicChecklists.{saicNumber}.json", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = Asm.GetManifestResourceStream(name)!;
        var raw = JsonSerializer.Deserialize<RawDoc>(stream, Json);
        if (raw is null) return null;
        var items = raw.Sections
            .SelectMany(s => s.Items.Select(i =>
                new SaicChecklistItemDto(i.ItemNo, i.AcceptanceCriteria, i.ReferenceStandard, s.No, s.Title)))
            .ToList();
        return new SaicChecklistDto(raw.SaicNumber, raw.Title, items);
    }
}
```

- [ ] **Step 10: Run the catalog test (passes)**

Run: `dotnet test tests/TuvInspection.UnitTests --filter SaicChecklistCatalogTests`
Expected: PASS.

- [ ] **Step 11: Write the resolve query + handler**

`src/TuvInspection.Application/BlueSticker/ResolveSaicChecklistQuery.cs`:

```csharp
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Application.BlueSticker;

/// <summary>Resolve the SAIC checklist (number + items) for an Aramco category + equipment type.
/// Returns null when the type is unmapped or its catalog entry isn't available yet.</summary>
public sealed record ResolveSaicChecklistQuery(string Category, string EquipmentType)
    : IQuery<SaicChecklistDto?>;
```

`src/TuvInspection.Infrastructure/BlueSticker/ResolveSaicChecklistHandler.cs`:

```csharp
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Domain.BlueSticker;

namespace TuvInspection.Infrastructure.BlueSticker;

public sealed class ResolveSaicChecklistHandler
    : IQueryHandler<ResolveSaicChecklistQuery, SaicChecklistDto?>
{
    private readonly SaicChecklistCatalog _catalog = new();

    public Task<SaicChecklistDto?> Handle(ResolveSaicChecklistQuery q, CancellationToken ct)
    {
        var saic = SaicChecklistMap.Resolve(q.Category, q.EquipmentType);
        return Task.FromResult(saic is null ? null : _catalog.Get(saic));
    }
}
```

> Note: match the exact `IQuery`/`IQueryHandler` interfaces and handler-registration convention used by `ListDefectCodesQuery` / its handler. If handlers are auto-registered by assembly scan, no DI edit is needed; otherwise register `ResolveSaicChecklistHandler` where the other Blue Sticker handlers are registered.

- [ ] **Step 12: Write the controller**

`src/TuvInspection.Api/Controllers/SaicChecklistsController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/saic-checklists")]
[Produces("application/json")]
public class SaicChecklistsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public SaicChecklistsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>Resolve the SAIC checklist for an equipment selection. 204 when unmapped.</summary>
    [HttpGet("resolve")]
    public async Task<ActionResult<SaicChecklistDto>> Resolve(
        [FromQuery] string category, [FromQuery] string equipmentType, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new ResolveSaicChecklistQuery(category, equipmentType), ct);
        return dto is null ? NoContent() : Ok(dto);
    }
}
```

- [ ] **Step 13: Build + run the full unit suite**

Run: `dotnet test tests/TuvInspection.UnitTests`
Expected: PASS (new tests green, nothing else broken).

- [ ] **Step 14: Commit**

```bash
git add src/TuvInspection.Contracts/BlueSticker/SaicChecklistDtos.cs \
  src/TuvInspection.Domain/BlueSticker/SaicChecklistMap.cs \
  src/TuvInspection.Application/BlueSticker/ResolveSaicChecklistQuery.cs \
  src/TuvInspection.Infrastructure/BlueSticker/SaicChecklistCatalog.cs \
  src/TuvInspection.Infrastructure/BlueSticker/ResolveSaicChecklistHandler.cs \
  src/TuvInspection.Api/Controllers/SaicChecklistsController.cs \
  src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj \
  tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistMapTests.cs \
  tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistCatalogTests.cs
git commit -m "feat(saic): catalog, type->SAIC map, resolve endpoint"
```

---

## Task 3: Frontend API client

**Files:**
- Create: `web/src/app/core/api/saic-checklists.api.ts`

- [ ] **Step 1: Write the client**

`web/src/app/core/api/saic-checklists.api.ts`:

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SaicChecklistItem {
  itemNo: string;
  acceptanceCriteria: string;
  referenceStandard: string;
  sectionNo: string | null;
  sectionTitle: string | null;
}

export interface SaicChecklist {
  saicNumber: string;
  title: string;
  items: SaicChecklistItem[];
}

@Injectable({ providedIn: 'root' })
export class SaicChecklistsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/saic-checklists`;

  /** Resolve the checklist for a category + equipment type. Emits null (204) when unmapped. */
  resolve(category: string, equipmentType: string): Observable<SaicChecklist | null> {
    const p = new HttpParams().set('category', category).set('equipmentType', equipmentType);
    return this.http.get<SaicChecklist | null>(`${this.base}/resolve`, { params: p });
  }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `cd web && npx tsc -p tsconfig.app.json --noEmit`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add web/src/app/core/api/saic-checklists.api.ts
git commit -m "feat(saic): frontend resolve API client"
```

---

## Task 4: Aramco form emits its selection

**Files:**
- Modify: `web/src/app/features/certificates/components/aramco-form.component.ts`

The form already has `onCategoryChange()` and binds `form.aramcoCategoryNo` + `form.equipmentType`. Add an output that fires whenever the (category, equipmentType) pair is meaningful, so the parent can resolve the SAIC checklist.

- [ ] **Step 1: Add the output + emit**

In `aramco-form.component.ts`, add an `@Output` near the existing `@Output() save`:

```typescript
/** Fires when the equipment selection that drives the SAIC checklist changes. */
@Output() equipmentSelectionChange = new EventEmitter<{ category: string; equipmentType: string }>();
```

Add a private emitter and call it from both `onCategoryChange()` and the equipment-type change handler (the `(onChange)` of the Equipment Type `p-select`). Emit only when both are set:

```typescript
private emitSelection() {
  const category = this.form.aramcoCategoryNo;
  const equipmentType = this.form.equipmentType;
  if (category && equipmentType) {
    this.equipmentSelectionChange.emit({ category, equipmentType });
  }
}
```

Wire the Equipment Type `p-select` in the template to call it, e.g.:

```html
(onChange)="emitSelection()"
```

and call `this.emitSelection();` at the end of `onCategoryChange()`. Also emit once after the form loads an existing value (end of the `value` setter / `parse`) so opening a saved Blue Sticker cert resolves its checklist.

- [ ] **Step 2: Verify it compiles**

Run: `cd web && npx tsc -p tsconfig.app.json --noEmit`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add web/src/app/features/certificates/components/aramco-form.component.ts
git commit -m "feat(saic): aramco form emits equipment selection"
```

---

## Task 5: SAIC checklist section in the Blue Sticker branch

**Files:**
- Modify: `web/src/app/features/certificates/pages/certificate-detail.page.ts`

Render a checklist editor inside the Blue Sticker (`@if (c.isBlueStickerCertificate)`) section, populated from the resolved SAIC checklist and persisted via the existing `saveChecklist($event)` path.

- [ ] **Step 1: Add state + resolver**

In the page component class, inject the API and hold the resolved checklist + the loaded editor JSON:

```typescript
private saicApi = inject(SaicChecklistsApi);
protected saicNumber = signal<string | null>(null);
protected saicChecklistJson = signal<string | null>(null);

/** Resolve + load the SAIC checklist for the current Aramco selection into the editor. */
onAramcoSelection(sel: { category: string; equipmentType: string }, currentChecklistJson: string | null) {
  this.saicApi.resolve(sel.category, sel.equipmentType).subscribe((res) => {
    this.saicNumber.set(res?.saicNumber ?? null);
    // Don't clobber an already-filled checklist; only seed when empty.
    if (res && !this.hasChecklistItems(currentChecklistJson)) {
      this.saicChecklistJson.set(JSON.stringify({
        items: res.items.map((i, idx) => ({
          itemNo: i.itemNo, acceptanceCriteria: i.acceptanceCriteria,
          referenceStandard: i.referenceStandard, result: 'NotSet', remark: '',
        })),
        generatedFromTemplateId: res.saicNumber,
      }));
    } else {
      this.saicChecklistJson.set(currentChecklistJson);
    }
  });
}

private hasChecklistItems(json: string | null): boolean {
  if (!json) return false;
  try { return (JSON.parse(json)?.items?.length ?? 0) > 0; } catch { return false; }
}
```

Add the imports for `inject`, `signal`, and `SaicChecklistsApi` if not already present.

- [ ] **Step 2: Wire the template (Blue Sticker branch)**

Inside the `@if (c.isBlueStickerCertificate)` block, after `<tuv-aramco-form ...>`, add:

```html
<div class="saic-checklist" *ngIf="saicNumber()">
  <header class="block-header">
    <h3><i class="pi pi-list-check"></i> Inspection checklist — {{ saicNumber() }}</h3>
  </header>
  <tuv-checklist-editor
    [value]="saicChecklistJson() ?? c.checklistJson"
    [equipmentTypeName]="c.equipmentTypeName"
    [readonly]="!isMutable()"
    (save)="saveChecklist($event)" />
</div>
```

On `<tuv-aramco-form>` add the selection handler:

```html
(equipmentSelectionChange)="onAramcoSelection($event, c.checklistJson)"
```

- [ ] **Step 3: Verify it compiles**

Run: `cd web && npx tsc -p tsconfig.app.json --noEmit`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add web/src/app/features/certificates/pages/certificate-detail.page.ts
git commit -m "feat(saic): load + fill SAIC checklist on Blue Sticker certs"
```

---

## Task 6: End-to-end pilot verification

**Files:** none (manual verification).

- [ ] **Step 1: Run backend + frontend**

Start the API and the Angular dev server per the project's run steps (see `/run` or CLAUDE.md plan).

- [ ] **Step 2: Drive the pilot**

Open a Blue Sticker certificate for CR01 Mobile Crane equipment. On the Annex 1 form pick Equipment Type = "Crawler Crane".
Expected: an "Inspection checklist — SAIC-U-7007" section appears with ~92 items, each showing acceptance criteria + reference + a Pass/Fail/N/A selector.

- [ ] **Step 3: Fill + save + reload**

Set a few items to Pass/Fail with a remark, click Save checklist, reload the page.
Expected: the filled results persist (checklist re-renders with the same results).

- [ ] **Step 4: Unmapped fallback**

Confirm a non-Blue-Sticker (TPI) cert is unchanged (its generic checklist editor still shows). Confirm picking a category/type with no mapping shows no SAIC section and doesn't error.

- [ ] **Step 5: Commit any fixes found during verification**

```bash
git commit -am "fix(saic): pilot verification fixes"   # only if changes were needed
```

---

## Task 7: Extract the remaining 17 checklists

**Files:**
- Create: `src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists/SAIC-U-7001.json` … `SAIC-U-7018.json` (the 17 not done in Task 1).

- [ ] **Step 1: Batch-extract**

```bash
cd "/Volumes/development/Projects/Ahmed Gouz Eman"
SRC="SOFTWARE IMPLEMENTATION/Blue Sticker Inspection Service/SIAC 7001-7018"
OUT="src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists"
declare -A T=(
 [7001]="Load Test Report" [7002]="Below-the-Hook Lifting Equipment" [7003]="Tower Cranes"
 [7004]="Articulating Boom Cranes" [7005]="Elevators - Electric & Hydraulic" [7006]="Escalators"
 [7008]="Overhead and Gantry Cranes" [7009]="Floating Cranes and Floating Derricks"
 [7010]="Side Boom Tractor-Cranes" [7011]="Jib & Monorail Cranes"
 [7012]="Storage/Retrieval Machines" [7013]="Boom Supported E.W.P." [7014]="Self Propelled E.W.P."
 [7015]="Manually Propelled Elevating Aerial Platform" [7016]="Powered Platform (Skyclimber)"
 [7017]="Man Basket" [7018]="Portal & Pedestal Cranes" )
for n in "${!T[@]}"; do
  f=$(ls "$SRC"/SAIC-U-$n\ *.pdf)
  python3 tools/extract-saic-checklists/extract.py "$f" "SAIC-U-$n" "${T[$n]}" "$OUT/SAIC-U-$n.json"
done
```

- [ ] **Step 2: Structural sanity check per file**

Do NOT use the `pdftotext -layout | grep '^N.M'` count as the source of truth — it UNDERcounts, because the whole reason the extractor uses bbox geometry is that item numbers printed mid-block don't start a line (verified on 7007: grep=92, true=116). Instead, verify each extracted file is internally well-formed:

```bash
OUT="src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists"
for n in 7001 7002 7003 7004 7005 7006 7008 7009 7010 7011 7012 7013 7014 7015 7016 7017 7018; do
  python3 - "$OUT/SAIC-U-$n.json" <<'PY'
import json, sys, re
d = json.load(open(sys.argv[1]))
total = sum(len(s["items"]) for s in d["sections"])
problems = []
for s in d["sections"]:
    nums = [it["itemNo"] for it in s["items"]]
    # within a section, the minor numbers should be contiguous 1..N
    minors = [int(x.split(".")[1]) for x in nums if re.match(r"^\d+\.\d+$", x)]
    if minors and minors != list(range(1, len(minors) + 1)):
        problems.append(f"section {s['no']} non-contiguous: {nums}")
    for it in s["items"]:
        if not it["acceptanceCriteria"].strip(): problems.append(f"{it['itemNo']} empty criteria")
print(f"{d['saicNumber']}: {len(d['sections'])} sections, {total} items", "OK" if not problems else "PROBLEMS")
for p in problems: print("   !", p)
PY
done
```

Expected: each file reports `OK` with a plausible item total and ≥1 section. Any `PROBLEMS` (non-contiguous item numbers — the signature of the mis-banding bug fixed in 7007 — or empty criteria) must be fixed in the parser or the JSON before continuing.

- [ ] **Step 3: Spot-check 3 random checklists**

Open `SAIC-U-7009.json`, `SAIC-U-7013.json`, `SAIC-U-7018.json` and confirm criteria/reference columns read correctly (as in Task 1 Step 4). Fix as needed.

- [ ] **Step 4: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/SaicChecklists/*.json
git commit -m "feat(saic): extract remaining 17 SAIC checklists"
```

---

## Task 8: Catalog coverage test

**Files:**
- Test: `tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistCoverageTests.cs`

- [ ] **Step 1: Write the failing coverage test**

`tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistCoverageTests.cs`:

```csharp
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Infrastructure.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class SaicChecklistCoverageTests
{
    [Fact]
    public void Every_mapped_saic_number_has_a_catalog_entry()
    {
        var catalog = new SaicChecklistCatalog();
        foreach (var saic in SaicChecklistMap.AllSaicNumbers())
        {
            var doc = catalog.Get(saic);
            Assert.True(doc is { Items.Count: > 0 }, $"missing/empty catalog for {saic}");
        }
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/TuvInspection.UnitTests --filter SaicChecklistCoverageTests`
Expected: PASS once all 18 JSONs exist (it fails earlier, confirming the guard works).

- [ ] **Step 3: Commit**

```bash
git add tests/TuvInspection.UnitTests/BlueSticker/SaicChecklistCoverageTests.cs
git commit -m "test(saic): assert every mapped SAIC number has a catalog entry"
```

---

## Task 9: Fix the category-only display lookup

**Files:**
- Modify: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs:34-56`

The Blue Sticker report DTO currently derives `InspectionChecklistNumber` from category only. Upgrade it to use the type-aware `SaicChecklistMap`, falling back to the category-level number when the type is unknown.

- [ ] **Step 1: Use the shared map in the mapper**

In `BlueStickerHandlers.cs`, change the `ToDetail` call to pass the equipment type, and replace `ChecklistFor`:

```csharp
InspectionChecklistNumber: SaicChecklistMap.Resolve(r.AramcoCategoryNo, r.EquipmentType)
    ?? AramcoCategoryShortCodeFallback(r.AramcoCategoryNo));
```

Keep a private fallback that maps the short code to the category-level default (move the existing `ChecklistFor` switch body into `AramcoCategoryShortCodeFallback`). Add `using TuvInspection.Domain.BlueSticker;`.

- [ ] **Step 2: Build + run unit suite**

Run: `dotnet test tests/TuvInspection.UnitTests`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs
git commit -m "fix(saic): type-aware checklist number on blue sticker report DTO"
```

---

## Done When

- A Blue Sticker inspector picks an equipment type and the matching SAIC checklist's real items load, fill, save, and reload on the certificate.
- All 18 catalog JSONs exist, item counts match their PDFs, and the coverage test passes.
- No equipment type in the Aramco form's mapping resolves to a missing catalog entry.
- The TPI checklist flow is unchanged.
