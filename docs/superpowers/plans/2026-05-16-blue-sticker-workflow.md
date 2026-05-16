# Blue Sticker Inspection Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a fully separate Blue Sticker inspection module — its own aggregate, state machine, OTP verification, on-site tablet finalize screen, and an Annex 1 PDF that is byte-for-byte the official MS0053813 layout.

**Architecture:** New `BlueStickerReport` aggregate (DDD, Stateless state machine) decoupled from the TPI `InspectionCertificate`. Isolated `IOtpService` (email now, SMS later). CQRS handlers (Scrutor auto-scan) + FluentValidation. PDF reuses the embedded `Annex1.docx` template via Gotenberg with a QuestPDF fallback. Angular 20 standalone pages reusing the existing signature pad.

**Tech Stack:** .NET 10, EF Core (SQL Server), Stateless, FluentValidation, DocumentFormat.OpenXml, QuestPDF, Gotenberg; Angular 20 (standalone, signals), PrimeNG; xUnit + FluentAssertions + Testcontainers.MsSql.

**Spec:** `docs/superpowers/specs/2026-05-16-blue-sticker-workflow-design.md`

---

## Conventions (read once)

- **Reference patterns** are copied verbatim from the existing TPI certificate code. When a task says "mirror X", open X and follow it exactly.
- **Migrations auto-apply at startup** via `IdentitySeeder.SeedAsync()` → `_db.Database.MigrateAsync()`. You still must generate the migration file.
- **Run a single unit test:** `dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~<Class>.<Method>"`
- **Run all unit tests:** `dotnet test tests/TuvInspection.UnitTests`
- **Run integration tests:** `dotnet test tests/TuvInspection.IntegrationTests` (needs Docker for Testcontainers)
- **Build:** `dotnet build TuvInspection.slnx`
- **Roles** constants live in `TuvInspection.Domain.Identity.Roles` (`Inspector`, `TechReviewer`, `Manager`, `Coordinator`, `ClientUser`).
- **Report-content fields map 1:1 to the Annex 1 sheet.** State, OTP, signatures, transitions, and audit columns are workflow plumbing — not report content — and do not violate "exactly the sheet".
- Commit after every task with the message shown in its final step.

## File Structure

**Domain** (`src/TuvInspection.Domain/BlueSticker/`)
- `BlueStickerEnums.cs` — state, trigger, result enums
- `BlueStickerReport.cs` — aggregate root (Annex 1 fields + workflow)
- `BlueStickerReportStateTransition.cs` — append-only audit row

**Application** (`src/TuvInspection.Application/BlueSticker/`)
- `BlueStickerReportStateMachine.cs` — Stateless config + role guards
- `BlueStickerCommands.cs` — commands/queries
- `BlueStickerValidators.cs` — FluentValidation
- `IOtpService.cs` — OTP port

**Infrastructure** (`src/TuvInspection.Infrastructure/BlueSticker/`)
- `BlueStickerHandlers.cs` — CQRS handlers
- `BlueStickerReportNoGenerator.cs` — `BSR-YYYY-NNNN`
- `EmailOtpService.cs` — `IOtpService` impl (outbox email)
- `BlueStickerReportTemplateFiller.cs` — fills `Annex1.docx`
- `BlueStickerReportPdfRenderer.cs` — Gotenberg + QuestPDF fallback
- `Persistence/Configurations/BlueStickerReportConfiguration.cs`

**Contracts** (`src/TuvInspection.Contracts/BlueSticker/BlueStickerDtos.cs`)

**API** (`src/TuvInspection.Api/Controllers/BlueStickerReportsController.cs`)

**Frontend** (`web/src/app/`)
- `core/api/blue-sticker.api.ts`, `core/models/blue-sticker.models.ts`
- `features/blue-sticker/pages/blue-sticker-list.page.ts`
- `features/blue-sticker/pages/blue-sticker-fill.page.ts`
- `features/blue-sticker/pages/blue-sticker-finalize.page.ts`

**Tests**
- `tests/TuvInspection.UnitTests/BlueSticker/*`
- `tests/TuvInspection.IntegrationTests/BlueSticker/*`

---

# Phase 1 — Domain

### Task 1: Blue Sticker enums

**Files:**
- Create: `src/TuvInspection.Domain/BlueSticker/BlueStickerEnums.cs`

- [ ] **Step 1: Create the enums file**

```csharp
namespace TuvInspection.Domain.BlueSticker;

/// <summary>
/// Lifecycle states for a Blue Sticker inspection report. Separate from the TPI
/// CertificateState — the Blue Sticker flow is the 9-step Aramco process and is not
/// shared with the certificate aggregate.
/// </summary>
public enum BlueStickerReportState
{
    Draft = 0,                     // created with a job order, admin fields filled
    InProgress = 1,                // inspector started on site; inspection date/time stamped
    UnderReview = 2,               // submitted to the technical reviewer
    Approved = 3,                  // reviewer approved (final); sticker auto-issued
    AwaitingClientSignature = 4,   // OTP sent to client; awaiting on-site signature
    ClientSigned = 5,              // terminal — client signed on the tablet
    Rejected = 6,                  // reviewer rejected; returns to InProgress
    Voided = 7                     // terminal
}

public enum BlueStickerReportTrigger
{
    StartInspection = 0,
    SubmitForReview = 1,
    Approve = 2,
    Reject = 3,
    RequestClientOtp = 4,
    VerifyOtpAndSign = 5,
    Void = 6
}

public enum BlueStickerResult
{
    NotSet = 0,
    Pass = 1,
    Fail = 2
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Domain`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Domain/BlueSticker/BlueStickerEnums.cs
git commit -m "feat(blue-sticker): add domain enums for report state/trigger/result"
```

---

### Task 2: BlueStickerReportStateTransition audit entity

**Files:**
- Create: `src/TuvInspection.Domain/BlueSticker/BlueStickerReportStateTransition.cs`

Mirror of `src/TuvInspection.Domain/Certificates/CertificateStateTransition.cs`.

- [ ] **Step 1: Create the entity**

```csharp
using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.BlueSticker;

/// <summary>Append-only audit row recording every state transition on a BlueStickerReport.</summary>
public class BlueStickerReportStateTransition : Entity<Guid>
{
    public Guid ReportId { get; private set; }
    public BlueStickerReportState FromState { get; private set; }
    public BlueStickerReportState ToState { get; private set; }
    public string ActorUserId { get; private set; } = default!;
    public string ActorRole { get; private set; } = default!;
    public string? Comments { get; private set; }
    public DateTime AtUtc { get; private set; }

    private BlueStickerReportStateTransition() { }

    public BlueStickerReportStateTransition(
        Guid id,
        Guid reportId,
        BlueStickerReportState from,
        BlueStickerReportState to,
        string actorUserId,
        string actorRole,
        string? comments,
        DateTime atUtc) : base(id)
    {
        ReportId = reportId;
        FromState = from;
        ToState = to;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        Comments = comments;
        AtUtc = atUtc;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Domain`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Domain/BlueSticker/BlueStickerReportStateTransition.cs
git commit -m "feat(blue-sticker): add state transition audit entity"
```

---

### Task 3: BlueStickerReport aggregate

**Files:**
- Create: `src/TuvInspection.Domain/BlueSticker/BlueStickerReport.cs`

Mirrors `InspectionCertificate` patterns: private parameterless ctor, public ctor with id, private setters, `ApplyTransition`, guarded mutators, `Ignore(DomainEvents)` later in EF config. Report-content properties = exactly the Annex 1 sheet.

- [ ] **Step 1: Create the aggregate**

```csharp
using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.BlueSticker;

/// <summary>
/// Aggregate root for a Blue Sticker (Aramco Annex 1 / MS0053813) inspection report.
/// Fully separate from InspectionCertificate. The report-content properties map 1:1 to
/// the official Annex 1 sheet; State/OTP/Signatures/Transitions/audit are workflow plumbing.
/// </summary>
public class BlueStickerReport : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    // ── Identity / links ──────────────────────────────────────────────
    public string ReportNo { get; private set; } = default!;      // BSR-YYYY-NNNN (Annex 1 "Report No.")
    public Guid JobOrderId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public Guid ClientId { get; private set; }

    // ── Sheet: header row (Coordinator @ job order + auto snapshots) ───
    public string TuvJobOrderNo { get; private set; } = default!;  // AUTO from JobOrder
    public string? AramcoCategoryNo { get; private set; }          // AUTO from Equipment
    public string? OrgCode { get; private set; }                   // MANUAL coordinator
    public string? RpoNo { get; private set; }                     // MANUAL coordinator
    public string? CrmNo { get; private set; }                     // MANUAL coordinator
    public string? DepartmentContractor { get; private set; }      // MANUAL coordinator

    // ── Sheet: inspection row ─────────────────────────────────────────
    public DateOnly? InspectionDate { get; private set; }          // AUTO at StartInspection
    public TimeOnly? InspectionTime { get; private set; }          // AUTO at StartInspection
    public string? PreviousStickerNo { get; private set; }         // AUTO
    public string? PreviousStickerIssuedBy { get; private set; }   // AUTO
    public string? AreaOfInspection { get; private set; }          // MANUAL inspector
    public BlueStickerResult Result { get; private set; } = BlueStickerResult.NotSet; // MANUAL inspector

    // ── Sheet: equipment snapshot (AUTO from Equipment) ───────────────
    public string EquipmentIdNo { get; private set; } = default!;
    public string? Capacity { get; private set; }
    public string? EquipmentLocation { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public string? EquipmentType { get; private set; }
    public string? EquipmentSerialNo { get; private set; }

    // ── Sheet: new sticker (AUTO at Approve) ──────────────────────────
    public string? NewStickerNo { get; private set; }
    public Guid? StickerId { get; private set; }
    public DateOnly? StickerExpirationDate { get; private set; }

    // ── Sheet: deficiencies (MANUAL inspector) ────────────────────────
    public string? Deficiencies { get; private set; }
    public string? CorrectiveActionsTaken { get; private set; }

    // ── Sheet: signature block ────────────────────────────────────────
    public string? ReceiverName { get; private set; }              // MANUAL @ site
    public string? ReceiverBadgeNo { get; private set; }           // MANUAL @ site
    public string? ReceiverTelephone { get; private set; }         // MANUAL @ site
    public string? InspectorName { get; private set; }             // AUTO at submit
    public string? InspectorSapNo { get; private set; }            // AUTO at submit
    public string? InspectorTelephone { get; private set; }        // inspector-entered (optional)
    public string? TechnicalReviewerName { get; private set; }     // AUTO at Approve
    public DateOnly? ReceivedDate { get; private set; }            // AUTO at ClientSigned
    public DateOnly? ReviewedDate { get; private set; }            // AUTO at Approve

    public string? ReceiverSignaturePng { get; private set; }      // captured on tablet
    public string? InspectorSignaturePng { get; private set; }     // captured on tablet
    public string? TechnicalReviewerSignaturePng { get; private set; } // captured at approve

    // ── Workflow plumbing (not report content) ────────────────────────
    public BlueStickerReportState State { get; private set; } = BlueStickerReportState.Draft;
    public string? ClientOtpHash { get; private set; }
    public DateTime? ClientOtpExpiresAtUtc { get; private set; }
    public int ClientOtpAttempts { get; private set; }
    public string? ClientOtpSentToEmail { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private readonly List<BlueStickerReportStateTransition> _transitions = new();
    public IReadOnlyCollection<BlueStickerReportStateTransition> Transitions => _transitions.AsReadOnly();

    private BlueStickerReport() { }

    public BlueStickerReport(
        Guid id,
        string reportNo,
        Guid jobOrderId,
        Guid equipmentId,
        Guid clientId,
        string tuvJobOrderNo,
        string equipmentIdNo) : base(id)
    {
        if (string.IsNullOrWhiteSpace(reportNo))
            throw new ArgumentException("Report number required", nameof(reportNo));
        ReportNo = reportNo.Trim();
        JobOrderId = jobOrderId;
        EquipmentId = equipmentId;
        ClientId = clientId;
        TuvJobOrderNo = tuvJobOrderNo;
        EquipmentIdNo = equipmentIdNo;
    }

    // ── Coordinator admin fields (Draft only) ─────────────────────────
    public void SetAdminFields(string? orgCode, string? rpoNo, string? crmNo,
        string? departmentContractor, string? aramcoCategoryNo)
    {
        EnsureState(BlueStickerReportState.Draft);
        OrgCode = orgCode?.Trim();
        RpoNo = rpoNo?.Trim();
        CrmNo = crmNo?.Trim();
        DepartmentContractor = departmentContractor?.Trim();
        AramcoCategoryNo = aramcoCategoryNo?.Trim();
    }

    // ── Equipment snapshot (taken at creation) ────────────────────────
    public void SetEquipmentSnapshot(string? capacity, string? location, string? manufacturer,
        string? model, string? equipmentType, string? serialNo)
    {
        Capacity = capacity?.Trim();
        EquipmentLocation = location?.Trim();
        Manufacturer = manufacturer?.Trim();
        Model = model?.Trim();
        EquipmentType = equipmentType?.Trim();
        EquipmentSerialNo = serialNo?.Trim();
    }

    public void SetPreviousSticker(string? stickerNo, string? issuedBy)
    {
        PreviousStickerNo = stickerNo?.Trim();
        PreviousStickerIssuedBy = issuedBy?.Trim();
    }

    // ── Inspector data entry (InProgress only) ────────────────────────
    public void UpdateInspectionData(string? areaOfInspection, BlueStickerResult result,
        string? deficiencies, string? correctiveActions, string? equipmentLocation,
        string? receiverName, string? receiverBadgeNo, string? receiverTelephone,
        string? inspectorTelephone)
    {
        EnsureState(BlueStickerReportState.InProgress);
        AreaOfInspection = areaOfInspection?.Trim();
        Result = result;
        Deficiencies = deficiencies?.Trim();
        CorrectiveActionsTaken = correctiveActions?.Trim();
        if (!string.IsNullOrWhiteSpace(equipmentLocation)) EquipmentLocation = equipmentLocation.Trim();
        ReceiverName = receiverName?.Trim();
        ReceiverBadgeNo = receiverBadgeNo?.Trim();
        ReceiverTelephone = receiverTelephone?.Trim();
        InspectorTelephone = inspectorTelephone?.Trim();
    }

    public void StampInspectionStart(DateOnly date, TimeOnly time)
    {
        InspectionDate = date;
        InspectionTime = time;
    }

    public void SetInspectorSnapshot(string? name, string? sapNo, string? signaturePng)
    {
        InspectorName = name?.Trim();
        InspectorSapNo = sapNo?.Trim();
        if (!string.IsNullOrWhiteSpace(signaturePng)) InspectorSignaturePng = signaturePng;
    }

    public void ApplyApprovalStamp(string reviewerName, string? reviewerSignaturePng,
        DateOnly reviewedDate, DateOnly stickerExpiration)
    {
        TechnicalReviewerName = reviewerName.Trim();
        TechnicalReviewerSignaturePng = reviewerSignaturePng;
        ReviewedDate = reviewedDate;
        StickerExpirationDate = stickerExpiration;
    }

    public void LinkSticker(Guid stickerId, string stickerNo)
    {
        StickerId = stickerId;
        NewStickerNo = stickerNo;
    }

    // ── OTP plumbing ──────────────────────────────────────────────────
    public void SetClientOtp(string otpHash, DateTime expiresAtUtc, string sentToEmail)
    {
        ClientOtpHash = otpHash;
        ClientOtpExpiresAtUtc = expiresAtUtc;
        ClientOtpAttempts = 0;
        ClientOtpSentToEmail = sentToEmail;
    }

    public void RecordOtpAttempt() => ClientOtpAttempts++;

    public void CaptureClientSignature(string receiverSignaturePng, DateOnly receivedDate)
    {
        ReceiverSignaturePng = receiverSignaturePng;
        ReceivedDate = receivedDate;
        // clear the OTP secret once consumed
        ClientOtpHash = null;
    }

    /// <summary>Internal use by BlueStickerReportStateMachine. Records the transition and sets state.</summary>
    public void ApplyTransition(BlueStickerReportState target, string actorUserId, string actorRole,
        string? comments, DateTime atUtc, Guid transitionId)
    {
        _transitions.Add(new BlueStickerReportStateTransition(
            transitionId, Id, State, target, actorUserId, actorRole, comments, atUtc));
        State = target;
    }

    private void EnsureState(BlueStickerReportState required)
    {
        if (State != required)
            throw new InvalidOperationException(
                $"Operation requires state {required} but report is in {State}.");
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Domain`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Domain/BlueSticker/BlueStickerReport.cs
git commit -m "feat(blue-sticker): add BlueStickerReport aggregate with Annex 1 fields"
```

---

### Task 4: State machine + unit tests

**Files:**
- Create: `src/TuvInspection.Application/BlueSticker/BlueStickerReportStateMachine.cs`
- Test: `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportStateMachineTests.cs`

Mirror `src/TuvInspection.Application/Certificates/CertificateStateMachine.cs`. The unit test project already references `TuvInspection.Application`; copy the `FixedClock`/`TestTenantContext` test doubles from `tests/TuvInspection.UnitTests/Certificates/CertificateStateMachineTests.cs`.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Domain.Identity;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class BlueStickerReportStateMachineTests
{
    [Fact]
    public void Inspector_starts_inspection_from_draft()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        r.State.Should().Be(BlueStickerReportState.InProgress);
        r.Transitions.Should().HaveCount(1);
    }

    [Fact]
    public void Coordinator_cannot_start_inspection()
    {
        var r = NewDraft();
        var act = () => Sm(r, Roles.Coordinator).Fire(BlueStickerReportTrigger.StartInspection);
        act.Should().Throw<InvalidOperationException>();
        r.State.Should().Be(BlueStickerReportState.Draft);
    }

    [Fact]
    public void Full_happy_path_reaches_ClientSigned()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.SubmitForReview);
        r.State.Should().Be(BlueStickerReportState.UnderReview);
        Sm(r, Roles.TechReviewer).Fire(BlueStickerReportTrigger.Approve);
        r.State.Should().Be(BlueStickerReportState.Approved);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.RequestClientOtp);
        r.State.Should().Be(BlueStickerReportState.AwaitingClientSignature);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.VerifyOtpAndSign);
        r.State.Should().Be(BlueStickerReportState.ClientSigned);
    }

    [Fact]
    public void Reviewer_reject_returns_to_InProgress()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.SubmitForReview);
        Sm(r, Roles.TechReviewer).Fire(BlueStickerReportTrigger.Reject, "Fix area of inspection");
        r.State.Should().Be(BlueStickerReportState.InProgress);
    }

    [Fact]
    public void Manager_can_also_approve()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.SubmitForReview);
        Sm(r, Roles.Manager).Fire(BlueStickerReportTrigger.Approve);
        r.State.Should().Be(BlueStickerReportState.Approved);
    }

    private static BlueStickerReport NewDraft() =>
        new(Guid.NewGuid(), "BSR-2026-0001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "JOD2026-0001", "EQ-1");

    private static BlueStickerReportStateMachine Sm(BlueStickerReport r, string role) =>
        new(r, new TestTenantContext(role, "u-" + role), new FixedClock(
            new DateTime(2026, 5, 16, 9, 0, 0, DateTimeKind.Utc)));

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime now) => UtcNow = now;
        public DateTime UtcNow { get; }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string role, string userId)
        { Roles = new HashSet<string> { role }; UserId = userId; PrimaryRole = role; }
        public bool IsAnonymous => false;
        public string? UserId { get; }
        public string? UserName => UserId;
        public string? PrimaryRole { get; }
        public IReadOnlySet<string> Roles { get; }
        public IReadOnlySet<Guid> AssignedClientIds { get; } = new HashSet<Guid>();
        public Guid? ActiveClientId => null;
        public string? IpAddress => null;
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
```

> If `ITenantContext` has more/fewer members than shown, open `tests/TuvInspection.UnitTests/Certificates/CertificateStateMachineTests.cs` and copy its `TestTenantContext`/`FixedClock` verbatim instead.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~BlueStickerReportStateMachineTests"`
Expected: FAIL — `BlueStickerReportStateMachine` does not exist.

- [ ] **Step 3: Implement the state machine**

```csharp
using Stateless;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Application.BlueSticker;

/// <summary>
/// Owns the 9-step Blue Sticker lifecycle. Role-gated; every successful trigger records a
/// BlueStickerReportStateTransition on the aggregate.
///
///   Draft → InProgress → UnderReview → Approved → AwaitingClientSignature → ClientSigned
///                            ↘ Rejected → InProgress (rework)
///                            ↘ Voided (terminal)
///
/// Approval is final at the TechReviewer step (Manager may also Approve — optional/observational).
/// </summary>
public sealed class BlueStickerReportStateMachine
{
    private readonly BlueStickerReport _r;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly StateMachine<BlueStickerReportState, BlueStickerReportTrigger> _fsm;
    private string? _pendingComments;

    public BlueStickerReportStateMachine(BlueStickerReport report, ITenantContext tenant, IClock clock)
    {
        _r = report ?? throw new ArgumentNullException(nameof(report));
        _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _fsm = new StateMachine<BlueStickerReportState, BlueStickerReportTrigger>(
            () => _r.State, _ => { });
        Configure();
    }

    public bool CanFire(BlueStickerReportTrigger t) => _fsm.CanFire(t);

    public void Fire(BlueStickerReportTrigger t, string? comments = null)
    {
        _pendingComments = comments;
        _fsm.Fire(t);
    }

    private void Configure()
    {
        _fsm.Configure(BlueStickerReportState.Draft)
            .PermitIf(BlueStickerReportTrigger.StartInspection, BlueStickerReportState.InProgress,
                IsInspector, "Inspector role required")
            .PermitIf(BlueStickerReportTrigger.Void, BlueStickerReportState.Voided,
                IsManagerOrCoordinator, "Manager or coordinator required");

        _fsm.Configure(BlueStickerReportState.InProgress)
            .PermitIf(BlueStickerReportTrigger.SubmitForReview, BlueStickerReportState.UnderReview,
                IsInspector, "Inspector role required")
            .PermitIf(BlueStickerReportTrigger.Void, BlueStickerReportState.Voided,
                IsManagerOrCoordinator, "Manager or coordinator required");

        _fsm.Configure(BlueStickerReportState.UnderReview)
            .PermitIf(BlueStickerReportTrigger.Approve, BlueStickerReportState.Approved,
                IsTechReviewerOrManager, "Tech reviewer or manager required")
            .PermitIf(BlueStickerReportTrigger.Reject, BlueStickerReportState.Rejected,
                IsTechReviewerOrManager, "Tech reviewer or manager required");

        _fsm.Configure(BlueStickerReportState.Rejected)
            .Permit(BlueStickerReportTrigger.Reject, BlueStickerReportState.InProgress);
        // Reject auto-routes back to InProgress so the inspector can rework.
        _fsm.Configure(BlueStickerReportState.Rejected)
            .OnEntry(() => _fsm.Fire(BlueStickerReportTrigger.Reject));

        _fsm.Configure(BlueStickerReportState.Approved)
            .PermitIf(BlueStickerReportTrigger.RequestClientOtp,
                BlueStickerReportState.AwaitingClientSignature, IsInspector, "Inspector role required")
            .PermitIf(BlueStickerReportTrigger.Void, BlueStickerReportState.Voided,
                IsManager, "Manager required");

        _fsm.Configure(BlueStickerReportState.AwaitingClientSignature)
            .PermitIf(BlueStickerReportTrigger.VerifyOtpAndSign, BlueStickerReportState.ClientSigned,
                IsInspector, "Inspector role required")
            .PermitReentryIf(BlueStickerReportTrigger.RequestClientOtp, IsInspector,
                "Inspector role required"); // resend OTP

        _fsm.Configure(BlueStickerReportState.ClientSigned);
        _fsm.Configure(BlueStickerReportState.Voided);

        _fsm.OnTransitioned(t =>
        {
            // Skip the synthetic Rejected→InProgress auto-hop's duplicate audit row:
            // it is still useful to record both, so keep it.
            _r.ApplyTransition(t.Destination, _tenant.UserId ?? "system",
                _tenant.PrimaryRole ?? "system", _pendingComments, _clock.UtcNow, Guid.NewGuid());
            _pendingComments = null;
        });
    }

    private bool IsInspector() => _tenant.IsInRole(Roles.Inspector);
    private bool IsManager() => _tenant.IsInRole(Roles.Manager);
    private bool IsManagerOrCoordinator() =>
        _tenant.IsInRole(Roles.Manager) || _tenant.IsInRole(Roles.Coordinator);
    private bool IsTechReviewerOrManager() =>
        _tenant.IsInRole(Roles.TechReviewer) || _tenant.IsInRole(Roles.Manager);
}
```

> The `Rejected` auto-hop uses Stateless `OnEntry` re-fire. If the installed Stateless version disallows re-entrant `Fire` inside `OnEntry`, replace the two `Rejected` `Configure` blocks with a single `.Permit(BlueStickerReportTrigger.Reject, BlueStickerReportState.InProgress)` on `UnderReview` directly (i.e. `Reject` goes straight `UnderReview → InProgress`) and delete the `Rejected` state usage from the test. Pick whichever the library supports; keep the test asserting final state `InProgress`.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~BlueStickerReportStateMachineTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TuvInspection.Application/BlueSticker/BlueStickerReportStateMachine.cs tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportStateMachineTests.cs
git commit -m "feat(blue-sticker): add state machine with role guards + tests"
```

---

# Phase 2 — Persistence

### Task 5: EF configuration

**Files:**
- Create: `src/TuvInspection.Infrastructure/Persistence/Configurations/BlueStickerReportConfiguration.cs`

Mirror `src/TuvInspection.Infrastructure/Persistence/Configurations/CertificateConfigurations.cs`. Signature PNGs are data-urls → `nvarchar(max)`.

- [ ] **Step 1: Create the configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.BlueSticker;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class BlueStickerReportConfiguration : IEntityTypeConfiguration<BlueStickerReport>
{
    public void Configure(EntityTypeBuilder<BlueStickerReport> e)
    {
        e.ToTable("BlueStickerReports");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.ReportNo).IsRequired().HasMaxLength(50);
        e.HasIndex(x => x.ReportNo).IsUnique();
        e.Property(x => x.JobOrderId).IsRequired();
        e.Property(x => x.EquipmentId).IsRequired();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.TuvJobOrderNo).IsRequired().HasMaxLength(50);
        e.Property(x => x.EquipmentIdNo).IsRequired().HasMaxLength(100);

        foreach (var p in new[] { "AramcoCategoryNo", "OrgCode", "RpoNo", "CrmNo",
            "DepartmentContractor", "AreaOfInspection", "Capacity", "EquipmentLocation",
            "Manufacturer", "Model", "EquipmentType", "EquipmentSerialNo", "PreviousStickerNo",
            "PreviousStickerIssuedBy", "NewStickerNo", "ReceiverName", "ReceiverBadgeNo",
            "ReceiverTelephone", "InspectorName", "InspectorSapNo", "InspectorTelephone",
            "TechnicalReviewerName", "ClientOtpSentToEmail" })
            e.Property(p).HasMaxLength(300);

        e.Property(x => x.Deficiencies).HasColumnType("nvarchar(max)");
        e.Property(x => x.CorrectiveActionsTaken).HasColumnType("nvarchar(max)");
        e.Property(x => x.ReceiverSignaturePng).HasColumnType("nvarchar(max)");
        e.Property(x => x.InspectorSignaturePng).HasColumnType("nvarchar(max)");
        e.Property(x => x.TechnicalReviewerSignaturePng).HasColumnType("nvarchar(max)");
        e.Property(x => x.ClientOtpHash).HasMaxLength(200);

        e.Property(x => x.Result).HasConversion<int>();
        e.Property(x => x.State).HasConversion<int>();
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);

        e.HasIndex(x => x.State);
        e.HasIndex(x => x.JobOrderId);
        e.HasIndex(x => x.EquipmentId);
        e.HasIndex(x => x.StickerId).IsUnique().HasFilter("[StickerId] IS NOT NULL");

        e.HasMany(x => x.Transitions)
            .WithOne()
            .HasForeignKey(t => t.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Navigation(x => x.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);

        e.Ignore(x => x.DomainEvents);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Infrastructure/Persistence/Configurations/BlueStickerReportConfiguration.cs
git commit -m "feat(blue-sticker): add EF configuration"
```

---

### Task 6: Register DbSet + generate migration

**Files:**
- Modify: `src/TuvInspection.Infrastructure/Persistence/AppDbContext.cs` (DbSet region, ~line 50)
- Create: migration files under `src/TuvInspection.Infrastructure/Persistence/Migrations/`

- [ ] **Step 1: Add the DbSets**

In `AppDbContext.cs`, in the block of `DbSet<>` properties (next to `public DbSet<Sticker> Stickers => Set<Sticker>();`), add:

```csharp
    public DbSet<BlueStickerReport> BlueStickerReports => Set<BlueStickerReport>();
    public DbSet<BlueStickerReportStateTransition> BlueStickerReportTransitions => Set<BlueStickerReportStateTransition>();
```

Add the using at the top of the file:

```csharp
using TuvInspection.Domain.BlueSticker;
```

- [ ] **Step 2: Add a tenant query filter**

In `OnModelCreating`, next to the existing `builder.Entity<InspectionCertificate>().HasQueryFilter(...)` line, add:

```csharp
        builder.Entity<BlueStickerReport>().HasQueryFilter(b =>
            _tenant.IsAnonymous || _tenant.AssignedClientIds.Contains(b.ClientId));
```

- [ ] **Step 3: Build**

Run: `dotnet build src/TuvInspection.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AddBlueStickerReports \
  --project src/TuvInspection.Infrastructure \
  --startup-project src/TuvInspection.Api \
  --output-dir Persistence/Migrations
```
Expected: new migration + updated `AppDbContextModelSnapshot.cs`. Open the migration and confirm `BlueStickerReports` + `BlueStickerReportTransitions` tables are created.

- [ ] **Step 5: Apply + smoke check**

Run:
```bash
dotnet ef database update --project src/TuvInspection.Infrastructure --startup-project src/TuvInspection.Api
```
Expected: applied with no error (SQL Server container must be running — `docker compose up -d`).

- [ ] **Step 6: Commit**

```bash
git add src/TuvInspection.Infrastructure/Persistence/AppDbContext.cs src/TuvInspection.Infrastructure/Persistence/Migrations/
git commit -m "feat(blue-sticker): register DbSets + migration"
```

---

# Phase 3 — OTP Service

### Task 7: IOtpService + EmailOtpService + tests

**Files:**
- Create: `src/TuvInspection.Application/BlueSticker/IOtpService.cs`
- Create: `src/TuvInspection.Infrastructure/BlueSticker/EmailOtpService.cs`
- Modify: `src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs`
- Test: `tests/TuvInspection.UnitTests/BlueSticker/OtpServiceTests.cs`

**Investigation first (do not skip):** Run `grep -rn "ClientSentCertificateEmail\|IOutbox\|OutboxMessage" src/TuvInspection.Infrastructure --include=*.cs | head` and open the file that defines the outbox email message records (e.g. the certificate email payloads) + the email renderer that consumes them. The OTP email must follow that exact pattern. Note the namespace + base type used by sibling messages.

- [ ] **Step 1: Define the port (pure, testable)**

`src/TuvInspection.Application/BlueSticker/IOtpService.cs`:

```csharp
namespace TuvInspection.Application.BlueSticker;

public sealed record OtpGenerationResult(string Code, string Hash, DateTime ExpiresAtUtc);

/// <summary>
/// Generates and verifies one-time codes. Email delivery is the only channel today;
/// the abstraction lets an SMS implementation slot in later without touching callers.
/// </summary>
public interface IOtpService
{
    /// <summary>Create a 6-digit code + its storable hash + expiry. Does NOT send.</summary>
    OtpGenerationResult Generate(DateTime nowUtc, TimeSpan validFor);

    /// <summary>Constant-time check of a candidate code against a stored hash.</summary>
    bool Verify(string candidate, string storedHash);

    /// <summary>Deliver the code to the client by email (enqueues an outbox message).</summary>
    Task SendAsync(string toEmail, string code, Guid reportId, CancellationToken ct);
}
```

- [ ] **Step 2: Write the failing test (Generate/Verify only — pure logic)**

`tests/TuvInspection.UnitTests/BlueSticker/OtpServiceTests.cs`:

```csharp
using FluentAssertions;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Infrastructure.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class OtpServiceTests
{
    // Hashing/codegen is pure and does not need the outbox; pass a null sender for these.
    private static EmailOtpService Svc() => new(outbox: null!, clock: null!);

    [Fact]
    public void Generate_produces_six_digit_code_and_future_expiry()
    {
        var now = new DateTime(2026, 5, 16, 9, 0, 0, DateTimeKind.Utc);
        var r = Svc().Generate(now, TimeSpan.FromMinutes(15));
        r.Code.Should().MatchRegex(@"^\d{6}$");
        r.ExpiresAtUtc.Should().Be(now.AddMinutes(15));
        r.Hash.Should().NotBeNullOrWhiteSpace().And.NotBe(r.Code);
    }

    [Fact]
    public void Verify_true_for_correct_code()
    {
        var r = Svc().Generate(DateTime.UtcNow, TimeSpan.FromMinutes(15));
        Svc().Verify(r.Code, r.Hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_false_for_wrong_code()
    {
        var r = Svc().Generate(DateTime.UtcNow, TimeSpan.FromMinutes(15));
        Svc().Verify("000000", r.Hash).Should().BeFalse();
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~OtpServiceTests"`
Expected: FAIL — `EmailOtpService` does not exist.

- [ ] **Step 4: Implement EmailOtpService**

`src/TuvInspection.Infrastructure/BlueSticker/EmailOtpService.cs`. **Replace `IOutbox`, the email message type, and `IClock` with the exact types found in Step 0 investigation.** The body below uses the same shape as `FireCertificateTriggerHandler` (`_outbox.Enqueue(new SomeEmail(...), ct)`):

```csharp
using System.Security.Cryptography;
using System.Text;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Outbox;   // adjust to the namespace found in investigation

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>OTP via the existing email outbox. SHA-256(code+salt) is stored, never the code.</summary>
public sealed class EmailOtpService : IOtpService
{
    private const string Salt = "tuv-bluesticker-otp-v1";
    private readonly IOutbox _outbox;
    private readonly IClock _clock;

    public EmailOtpService(IOutbox outbox, IClock clock)
    {
        _outbox = outbox;
        _clock = clock;
    }

    public OtpGenerationResult Generate(DateTime nowUtc, TimeSpan validFor)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        return new OtpGenerationResult(code, Hash(code), nowUtc.Add(validFor));
    }

    public bool Verify(string candidate, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(storedHash))
            return false;
        var a = Encoding.UTF8.GetBytes(Hash(candidate.Trim()));
        var b = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    public Task SendAsync(string toEmail, string code, Guid reportId, CancellationToken ct) =>
        _outbox.Enqueue(new ClientOtpEmail(reportId, toEmail, code, _clock.UtcNow), ct);

    private static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code + Salt));
        return Convert.ToHexString(bytes);
    }
}
```

- [ ] **Step 5: Add the outbox email message + its renderer, mirroring a sibling**

Open the file found in the Step 0 investigation that defines email payloads such as `ClientSentCertificateEmail` (with its `IOutbox`/email-renderer wiring). Add a sibling `ClientOtpEmail` record **in the same file/namespace and following the same base type and renderer-registration pattern as its siblings**. Concretely it must carry:

```csharp
public sealed record ClientOtpEmail(Guid ReportId, string ToEmail, string Code, DateTime AtUtc) /* : <same base/interface as sibling email messages> */;
```

In the email renderer/handler that switches over message types (the same place `ClientSentCertificateEmail` is rendered), add a branch that produces:
- **To:** `ToEmail`
- **Subject:** `TÜV Rheinland Arabia — Inspection signature code`
- **Body:** `Your one-time code to sign the Blue Sticker inspection report is: {Code}. It expires in 15 minutes. If you did not request this, ignore this email.`

(Use the exact templating mechanism the sibling uses — do not invent a new email path.)

- [ ] **Step 6: Register DI**

In `src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs`, next to `services.AddScoped<AramcoReportPdfRenderer>();`, add:

```csharp
services.AddScoped<TuvInspection.Application.BlueSticker.IOtpService,
    TuvInspection.Infrastructure.BlueSticker.EmailOtpService>();
```

- [ ] **Step 7: Run the test to verify it passes + build**

Run: `dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~OtpServiceTests"`
Expected: PASS (3 tests).
Run: `dotnet build TuvInspection.slnx`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add src/TuvInspection.Application/BlueSticker/IOtpService.cs src/TuvInspection.Infrastructure/BlueSticker/EmailOtpService.cs src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs tests/TuvInspection.UnitTests/BlueSticker/OtpServiceTests.cs
git add -A src/TuvInspection.Infrastructure   # the sibling email message file edited in step 5
git commit -m "feat(blue-sticker): isolated email OTP service + outbox message"
```

---

# Phase 4 — Application (Contracts, Generator, CQRS)

### Task 8: Contracts DTOs

**Files:**
- Create: `src/TuvInspection.Contracts/BlueSticker/BlueStickerDtos.cs`

Mirror `src/TuvInspection.Contracts/Certificates/CertificateDtos.cs` (sealed records, DTO enums duplicating domain values).

- [ ] **Step 1: Create the DTOs**

```csharp
namespace TuvInspection.Contracts.BlueSticker;

public enum BlueStickerReportStateDto
{
    Draft = 0, InProgress = 1, UnderReview = 2, Approved = 3,
    AwaitingClientSignature = 4, ClientSigned = 5, Rejected = 6, Voided = 7
}

public enum BlueStickerResultDto { NotSet = 0, Pass = 1, Fail = 2 }

public enum BlueStickerTriggerDto
{
    StartInspection, SubmitForReview, Approve, Reject, RequestClientOtp, VerifyOtpAndSign, Void
}

/// <summary>Coordinator admin fields, supplied when a Blue Sticker job order is created.</summary>
public sealed record CreateBlueStickerReportsRequest(
    Guid JobOrderId,
    string? OrgCode,
    string? RpoNo,
    string? CrmNo,
    string? DepartmentContractor);

/// <summary>Inspector data entry (InProgress only).</summary>
public sealed record UpdateBlueStickerInspectionRequest(
    string? AreaOfInspection,
    BlueStickerResultDto Result,
    string? Deficiencies,
    string? CorrectiveActionsTaken,
    string? EquipmentLocation,
    string? ReceiverName,
    string? ReceiverBadgeNo,
    string? ReceiverTelephone,
    string? InspectorTelephone);

public sealed record BlueStickerTransitionRequest(string? Comments, string? InspectorSignaturePng,
    string? TechnicalReviewerSignaturePng);

public sealed record RequestClientOtpRequest();   // body intentionally empty

public sealed record VerifyOtpAndSignRequest(string Otp, string ReceiverSignaturePng);

public sealed record BlueStickerTransitionDto(
    string FromState, string ToState, string ActorUserId, string ActorRole,
    string? Comments, DateTime AtUtc);

public sealed record BlueStickerReportDetailDto(
    Guid Id,
    string ReportNo,
    Guid JobOrderId,
    Guid EquipmentId,
    Guid ClientId,
    string TuvJobOrderNo,
    string? AramcoCategoryNo,
    string? OrgCode,
    string? RpoNo,
    string? CrmNo,
    string? DepartmentContractor,
    DateOnly? InspectionDate,
    TimeOnly? InspectionTime,
    string? PreviousStickerNo,
    string? PreviousStickerIssuedBy,
    string? AreaOfInspection,
    BlueStickerResultDto Result,
    string EquipmentIdNo,
    string? Capacity,
    string? EquipmentLocation,
    string? Manufacturer,
    string? Model,
    string? EquipmentType,
    string? EquipmentSerialNo,
    string? NewStickerNo,
    DateOnly? StickerExpirationDate,
    string? Deficiencies,
    string? CorrectiveActionsTaken,
    string? ReceiverName,
    string? ReceiverBadgeNo,
    string? ReceiverTelephone,
    string? InspectorName,
    string? InspectorSapNo,
    string? InspectorTelephone,
    string? TechnicalReviewerName,
    DateOnly? ReceivedDate,
    DateOnly? ReviewedDate,
    string? ReceiverSignaturePng,
    string? InspectorSignaturePng,
    string? TechnicalReviewerSignaturePng,
    BlueStickerReportStateDto State,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<BlueStickerTransitionDto> Transitions);

public sealed record BlueStickerReportListItemDto(
    Guid Id, string ReportNo, string TuvJobOrderNo, string EquipmentIdNo,
    BlueStickerReportStateDto State, DateOnly? InspectionDate, DateTime CreatedAtUtc);
```

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Contracts`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Contracts/BlueSticker/BlueStickerDtos.cs
git commit -m "feat(blue-sticker): add contracts DTOs"
```

---

### Task 9: ReportNo generator

**Files:**
- Create: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportNoGenerator.cs`
- Test: `tests/TuvInspection.IntegrationTests/BlueSticker/ReportNoGeneratorTests.cs`

Find the existing `CertificateNoGenerator` (`grep -rn "class CertificateNoGenerator" src`) and mirror its uniqueness/DB-count approach exactly (same `AppDbContext` injection + max/sequence strategy). Format: `BSR-YYYY-NNNN`.

- [ ] **Step 1: Implement the generator (mirror CertificateNoGenerator)**

```csharp
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>Generates BSR-YYYY-NNNN. NNNN is a per-year running count (zero-padded to 4).</summary>
public sealed class BlueStickerReportNoGenerator
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public BlueStickerReportNoGenerator(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"BSR-{year}-";
        var countThisYear = await _db.BlueStickerReports
            .IgnoreQueryFilters()
            .CountAsync(r => r.ReportNo.StartsWith(prefix), ct);
        return $"{prefix}{(countThisYear + 1):D4}";
    }
}
```

> If `CertificateNoGenerator` uses a different uniqueness strategy (e.g. a sequence table or retry loop), copy that strategy verbatim instead of the count approach to avoid race conditions.

- [ ] **Step 2: Register DI**

In `InfrastructureModule.cs` next to other concrete helpers (e.g. where `AramcoReportValidator` is registered), add:

```csharp
services.AddScoped<TuvInspection.Infrastructure.BlueSticker.BlueStickerReportNoGenerator>();
```

- [ ] **Step 3: Build**

Run: `dotnet build src/TuvInspection.Infrastructure`
Expected: Build succeeded. (Functional verification happens via the integration test in Task 27.)

- [ ] **Step 4: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportNoGenerator.cs src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs
git commit -m "feat(blue-sticker): report number generator"
```

---

### Task 10: Commands/queries definitions

**Files:**
- Create: `src/TuvInspection.Application/BlueSticker/BlueStickerCommands.cs`

Mirror `src/TuvInspection.Application/Certificates/CertificateCommands.cs` (record + `ICommand<T>`/`IQuery<T>`).

- [ ] **Step 1: Create commands/queries**

```csharp
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Application.BlueSticker;

public sealed record CreateBlueStickerReportsCommand(CreateBlueStickerReportsRequest Body)
    : ICommand<IReadOnlyList<BlueStickerReportDetailDto>>;

public sealed record UpdateBlueStickerInspectionCommand(Guid Id, UpdateBlueStickerInspectionRequest Body)
    : ICommand<BlueStickerReportDetailDto>;

public sealed record FireBlueStickerTriggerCommand(
    Guid Id, BlueStickerTriggerDto Trigger, BlueStickerTransitionRequest? Body)
    : ICommand<BlueStickerReportDetailDto>;

public sealed record RequestClientOtpCommand(Guid Id) : ICommand<BlueStickerReportDetailDto>;

public sealed record VerifyOtpAndSignCommand(Guid Id, VerifyOtpAndSignRequest Body)
    : ICommand<BlueStickerReportDetailDto>;

public sealed record GetBlueStickerReportByIdQuery(Guid Id) : IQuery<BlueStickerReportDetailDto?>;

public sealed record ListBlueStickerReportsQuery(
    Guid? JobOrderId, BlueStickerReportStateDto? State, string? Search, int Page, int PageSize)
    : IQuery<Contracts.Common.PagedResult<BlueStickerReportListItemDto>>;
```

> Confirm the `PagedResult<T>` namespace by opening `CertificateCommands.cs` / its query return type and matching it exactly.

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Application`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Application/BlueSticker/BlueStickerCommands.cs
git commit -m "feat(blue-sticker): CQRS command/query definitions"
```

---

### Task 11: Validators

**Files:**
- Create: `src/TuvInspection.Application/BlueSticker/BlueStickerValidators.cs`

Mirror `src/TuvInspection.Application/Certificates/CertificateValidators.cs`. Auto-scanned by `AddValidatorsFromAssembly`.

- [ ] **Step 1: Create validators**

```csharp
using FluentValidation;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Application.BlueSticker;

public sealed class UpdateBlueStickerInspectionRequestValidator
    : AbstractValidator<UpdateBlueStickerInspectionRequest>
{
    public UpdateBlueStickerInspectionRequestValidator()
    {
        RuleFor(x => x.AreaOfInspection).NotEmpty().WithMessage("Area of inspection is required.");
        RuleFor(x => x.Result).NotEqual(BlueStickerResultDto.NotSet)
            .WithMessage("Inspection result must be Pass or Fail.");
        RuleFor(x => x.ReceiverName).NotEmpty().WithMessage("Receiver name is required.");
        RuleFor(x => x.ReceiverBadgeNo).NotEmpty().WithMessage("Receiver badge No. is required.");
    }
}

public sealed class VerifyOtpAndSignRequestValidator : AbstractValidator<VerifyOtpAndSignRequest>
{
    public VerifyOtpAndSignRequestValidator()
    {
        RuleFor(x => x.Otp).NotEmpty().Matches(@"^\d{6}$").WithMessage("OTP must be 6 digits.");
        RuleFor(x => x.ReceiverSignaturePng).NotEmpty()
            .Must(s => s!.StartsWith("data:image/")).WithMessage("A client signature is required.");
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Application`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Application/BlueSticker/BlueStickerValidators.cs
git commit -m "feat(blue-sticker): request validators"
```

---

### Task 12: Handlers — create / get / list / update

**Files:**
- Create: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs`

Mirror `src/TuvInspection.Infrastructure/Certificates/CertificateHandlers.cs`. Handlers are Scrutor auto-scanned (no manual DI). The create handler reads the job order + its equipment and produces one report per Aramco-categorized equipment.

> **Investigation:** open `JobOrder` + how equipment is linked to a job order (grep `JobOrderId` usages and the `Equipment` entity). The create handler needs the set of equipment for a job order. If equipment is not directly linked to a job order in the schema, the handler instead creates reports for the equipment ids passed in — adjust `CreateBlueStickerReportsRequest` to include `Guid[] EquipmentIds` and update Task 8's DTO + Task 22 UI accordingly. Confirm the real linkage before writing the loop.

- [ ] **Step 1: Implement create / get / list / update handlers**

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.BlueSticker;

internal static class BlueStickerMapper
{
    public static BlueStickerReportDetailDto ToDetail(BlueStickerReport r) => new(
        r.Id, r.ReportNo, r.JobOrderId, r.EquipmentId, r.ClientId, r.TuvJobOrderNo,
        r.AramcoCategoryNo, r.OrgCode, r.RpoNo, r.CrmNo, r.DepartmentContractor,
        r.InspectionDate, r.InspectionTime, r.PreviousStickerNo, r.PreviousStickerIssuedBy,
        r.AreaOfInspection, (BlueStickerResultDto)(int)r.Result, r.EquipmentIdNo, r.Capacity,
        r.EquipmentLocation, r.Manufacturer, r.Model, r.EquipmentType, r.EquipmentSerialNo,
        r.NewStickerNo, r.StickerExpirationDate, r.Deficiencies, r.CorrectiveActionsTaken,
        r.ReceiverName, r.ReceiverBadgeNo, r.ReceiverTelephone, r.InspectorName, r.InspectorSapNo,
        r.InspectorTelephone, r.TechnicalReviewerName, r.ReceivedDate, r.ReviewedDate,
        r.ReceiverSignaturePng, r.InspectorSignaturePng, r.TechnicalReviewerSignaturePng,
        (BlueStickerReportStateDto)(int)r.State, r.CreatedAtUtc, r.UpdatedAtUtc,
        r.Transitions.OrderBy(t => t.AtUtc).Select(t => new BlueStickerTransitionDto(
            t.FromState.ToString(), t.ToState.ToString(), t.ActorUserId, t.ActorRole,
            t.Comments, t.AtUtc)).ToList());
}

public sealed class CreateBlueStickerReportsHandler
    : ICommandHandler<CreateBlueStickerReportsCommand, IReadOnlyList<BlueStickerReportDetailDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly BlueStickerReportNoGenerator _no;

    public CreateBlueStickerReportsHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        BlueStickerReportNoGenerator no)
    { _db = db; _tenant = tenant; _clock = clock; _no = no; }

    public async Task<IReadOnlyList<BlueStickerReportDetailDto>> Handle(
        CreateBlueStickerReportsCommand command, CancellationToken ct)
    {
        var b = command.Body;
        var jo = await _db.JobOrders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(j => j.Id == b.JobOrderId, ct)
            ?? throw new KeyNotFoundException($"Job order {b.JobOrderId} not found.");

        // Equipment for this job order's client that is Aramco-categorised (Blue Sticker eligible).
        var equipment = await _db.Equipment.IgnoreQueryFilters()
            .Where(e => e.ClientId == jo.ClientId
                        && e.AramcoCategory != null
                        && e.AramcoCategory != TuvInspection.Domain.Equipment.AramcoCategory.None)
            .ToListAsync(ct);
        if (equipment.Count == 0)
            throw new InvalidOperationException(
                "No Aramco-categorised equipment for this client — nothing to inspect for Blue Sticker.");

        var created = new List<BlueStickerReport>();
        foreach (var eq in equipment)
        {
            if (await _db.BlueStickerReports.IgnoreQueryFilters()
                    .AnyAsync(r => r.JobOrderId == jo.Id && r.EquipmentId == eq.Id, ct))
                continue; // idempotent

            var typeName = await _db.EquipmentTypes.IgnoreQueryFilters()
                .Where(t => t.Id == eq.EquipmentTypeId).Select(t => t.Name)
                .FirstOrDefaultAsync(ct);

            var report = new BlueStickerReport(Guid.NewGuid(), await _no.Next(ct),
                jo.Id, eq.Id, jo.ClientId, jo.JobOrderNo, eq.IdNo);
            report.SetAdminFields(b.OrgCode, b.RpoNo, b.CrmNo, b.DepartmentContractor,
                eq.AramcoCategory?.ToString());
            report.SetEquipmentSnapshot(eq.Swl, eq.Location, eq.Manufacturer, eq.Model,
                typeName, eq.SerialNo);

            var prev = await _db.Stickers.IgnoreQueryFilters()
                .Where(s => s.IssuedToEquipmentId == eq.Id)
                .OrderByDescending(s => s.IssuedAtUtc)
                .Select(s => new { s.StickerNo, s.AssignedToInspectorId })
                .FirstOrDefaultAsync(ct);
            if (prev is not null)
                report.SetPreviousSticker(prev.StickerNo, prev.AssignedToInspectorId);

            report.CreatedAtUtc = _clock.UtcNow;
            report.CreatedById = _tenant.UserId;
            _db.BlueStickerReports.Add(report);
            created.Add(report);
        }
        await _db.SaveChangesAsync(ct);
        return created.Select(BlueStickerMapper.ToDetail).ToList();
    }
}

public sealed class GetBlueStickerReportByIdHandler
    : IQueryHandler<GetBlueStickerReportByIdQuery, BlueStickerReportDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetBlueStickerReportByIdHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<BlueStickerReportDetailDto?> Handle(
        GetBlueStickerReportByIdQuery q, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().Include(r => r.Transitions)
            : _db.BlueStickerReports.Include(r => r.Transitions);
        var r = await query.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        return r is null ? null : BlueStickerMapper.ToDetail(r);
    }
}

public sealed class ListBlueStickerReportsHandler
    : IQueryHandler<ListBlueStickerReportsQuery, PagedResult<BlueStickerReportListItemDto>>
{
    private readonly AppDbContext _db;
    public ListBlueStickerReportsHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<BlueStickerReportListItemDto>> Handle(
        ListBlueStickerReportsQuery q, CancellationToken ct)
    {
        var query = _db.BlueStickerReports.AsNoTracking().AsQueryable();
        if (q.JobOrderId is { } jid) query = query.Where(r => r.JobOrderId == jid);
        if (q.State is { } st) query = query.Where(r => (int)r.State == (int)st);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(r => r.ReportNo.Contains(q.Search) ||
                                     r.EquipmentIdNo.Contains(q.Search));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAtUtc)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(r => new BlueStickerReportListItemDto(
                r.Id, r.ReportNo, r.TuvJobOrderNo, r.EquipmentIdNo,
                (BlueStickerReportStateDto)(int)r.State, r.InspectionDate, r.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<BlueStickerReportListItemDto>(items, total, q.Page, q.PageSize);
    }
}

public sealed class UpdateBlueStickerInspectionHandler
    : ICommandHandler<UpdateBlueStickerInspectionCommand, BlueStickerReportDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<UpdateBlueStickerInspectionRequest> _validator;

    public UpdateBlueStickerInspectionHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<UpdateBlueStickerInspectionRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<BlueStickerReportDetailDto> Handle(
        UpdateBlueStickerInspectionCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);
        var r = await _db.BlueStickerReports.Include(x => x.Transitions)
            .FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");
        var b = command.Body;
        r.UpdateInspectionData(b.AreaOfInspection, (BlueStickerResult)(int)b.Result,
            b.Deficiencies, b.CorrectiveActionsTaken, b.EquipmentLocation,
            b.ReceiverName, b.ReceiverBadgeNo, b.ReceiverTelephone, b.InspectorTelephone);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}
```

> Verify member names against the real entities: `Equipment.AramcoCategory`, `Equipment.Swl`, `Equipment.Location`, `Equipment.Manufacturer`, `Equipment.Model`, `Equipment.SerialNo`, `Equipment.EquipmentTypeId`, `Equipment.IdNo` (confirmed in research); `EquipmentTypes.Name`, `Sticker.IssuedToEquipmentId`, `Sticker.IssuedAtUtc`, `Sticker.AssignedToInspectorId` (confirmed). If `PagedResult<T>`'s constructor differs, match `ListCertificatesHandler` exactly.

- [ ] **Step 2: Build**

Run: `dotnet build TuvInspection.slnx`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs
git commit -m "feat(blue-sticker): create/get/list/update handlers"
```

---

### Task 13: Trigger handler (StartInspection / Submit / Approve / Reject / Void)

**Files:**
- Modify: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs` (append)

Mirror `FireCertificateTriggerHandler` including the sticker auto-issue block. On `Approve`, snapshot the reviewer + dates + auto-issue sticker. On `SubmitForReview`, snapshot inspector identity. On `StartInspection`, stamp inspection date/time.

> **Investigation:** confirm the validity period for `StickerExpirationDate`. The spec lists this as an open dependency ("validity per Aramco category"). Until the per-category table is provided, use **1 year from inspection date** and add a `// TODO(spec): per-Aramco-category validity` comment ONLY here (this is an explicitly-deferred spec dependency, not a plan placeholder). Surface it in the Task 27 integration assertion so it is visible.

- [ ] **Step 1: Append the trigger handler**

```csharp
public sealed class FireBlueStickerTriggerHandler
    : ICommandHandler<FireBlueStickerTriggerCommand, BlueStickerReportDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

    public FireBlueStickerTriggerHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        Microsoft.Extensions.Configuration.IConfiguration config)
    { _db = db; _tenant = tenant; _clock = clock; _config = config; }

    public async Task<BlueStickerReportDetailDto> Handle(
        FireBlueStickerTriggerCommand command, CancellationToken ct)
    {
        var r = await _db.BlueStickerReports.Include(x => x.Transitions)
            .FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");

        var trigger = (BlueStickerReportTrigger)(int)command.Trigger;

        if (trigger == BlueStickerReportTrigger.SubmitForReview)
        {
            if (r.Result == BlueStickerResult.NotSet || string.IsNullOrWhiteSpace(r.AreaOfInspection))
                throw new InvalidOperationException(
                    "Cannot submit — area of inspection and inspection result are required.");
            var insp = _tenant.UserId is null ? null : await _db.Users.AsNoTracking()
                .Where(u => u.Id == _tenant.UserId)
                .Select(u => new { Name = u.FullName ?? u.UserName ?? u.Email, u.SapNo })
                .FirstOrDefaultAsync(ct);
            r.SetInspectorSnapshot(insp?.Name, insp?.SapNo, command.Body?.InspectorSignaturePng);
        }

        if (trigger == BlueStickerReportTrigger.StartInspection)
        {
            var nowLocal = _clock.UtcNow; // store UTC date/time of start
            r.StampInspectionStart(DateOnly.FromDateTime(nowLocal), TimeOnly.FromDateTime(nowLocal));
        }

        if (trigger == BlueStickerReportTrigger.Approve && r.Result == BlueStickerResult.Fail)
            throw new InvalidOperationException(
                "Cannot approve a Failed Blue Sticker inspection. Re-inspect or void.");

        var sm = new BlueStickerReportStateMachine(r, _tenant, _clock);
        if (!sm.CanFire(trigger))
            throw new InvalidOperationException(
                $"Cannot {trigger} a report currently in state {r.State}.");
        sm.Fire(trigger, command.Body?.Comments);

        if (r.State == BlueStickerReportState.Approved)
        {
            var reviewer = _tenant.UserId is null ? "Reviewer" : await _db.Users.AsNoTracking()
                .Where(u => u.Id == _tenant.UserId)
                .Select(u => u.FullName ?? u.UserName ?? u.Email)
                .FirstOrDefaultAsync(ct) ?? "Reviewer";
            var inspDate = r.InspectionDate ?? DateOnly.FromDateTime(_clock.UtcNow);
            // TODO(spec): per-Aramco-category validity — interim 1 year (open dependency in spec §9)
            var expiry = inspDate.AddYears(1);
            r.ApplyApprovalStamp(reviewer!, command.Body?.TechnicalReviewerSignaturePng,
                DateOnly.FromDateTime(_clock.UtcNow), expiry);

            if (r.StickerId is null)
            {
                var sticker = await _db.Stickers
                    .Where(s => s.State == TuvInspection.Domain.Stickers.StickerState.Unallocated)
                    .OrderBy(s => s.StickerNo)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException(
                        "No Blue Sticker stock available. Manager: procure new stickers first.");
                sticker.Issue(r.Id, r.EquipmentId, r.ClientId, expiry, _clock.UtcNow);
                r.LinkSticker(sticker.Id, sticker.StickerNo);
            }
        }

        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}
```

> `Sticker.Issue(certificateId, equipmentId, clientId, validUntil, atUtc)` is the confirmed signature — the first arg is just a Guid link; passing `r.Id` is fine. Confirm `StickerState.Unallocated` namespace via the certificate handler (it uses `StickerState.Unallocated` directly).

- [ ] **Step 2: Build**

Run: `dotnet build TuvInspection.slnx`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs
git commit -m "feat(blue-sticker): trigger handler with snapshots + sticker auto-issue"
```

---

### Task 14: OTP request + verify-and-sign handlers

**Files:**
- Modify: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs` (append)

- [ ] **Step 1: Append the OTP handlers**

```csharp
public sealed class RequestClientOtpHandler
    : ICommandHandler<RequestClientOtpCommand, BlueStickerReportDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IOtpService _otp;

    public RequestClientOtpHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IOtpService otp)
    { _db = db; _tenant = tenant; _clock = clock; _otp = otp; }

    public async Task<BlueStickerReportDetailDto> Handle(
        RequestClientOtpCommand command, CancellationToken ct)
    {
        var r = await _db.BlueStickerReports.Include(x => x.Transitions)
            .FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");

        var email = await _db.Clients.IgnoreQueryFilters()
            .Where(c => c.Id == r.ClientId).Select(c => c.ContactEmail)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Client has no contact email on file — cannot send the signature OTP. " +
                "Set the client's contact email first.");

        // Drive the state machine: Approved → AwaitingClientSignature, or reentry to resend.
        var sm = new BlueStickerReportStateMachine(r, _tenant, _clock);
        if (!sm.CanFire(BlueStickerReportTrigger.RequestClientOtp))
            throw new InvalidOperationException(
                $"Cannot request a client OTP while report is in state {r.State}.");
        sm.Fire(BlueStickerReportTrigger.RequestClientOtp);

        var gen = _otp.Generate(_clock.UtcNow, TimeSpan.FromMinutes(15));
        r.SetClientOtp(gen.Hash, gen.ExpiresAtUtc, email!);
        await _otp.SendAsync(email!, gen.Code, r.Id, ct);

        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}

public sealed class VerifyOtpAndSignHandler
    : ICommandHandler<VerifyOtpAndSignCommand, BlueStickerReportDetailDto>
{
    private const int MaxAttempts = 5;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IOtpService _otp;
    private readonly IValidator<VerifyOtpAndSignRequest> _validator;

    public VerifyOtpAndSignHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IOtpService otp, IValidator<VerifyOtpAndSignRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _otp = otp; _validator = validator; }

    public async Task<BlueStickerReportDetailDto> Handle(
        VerifyOtpAndSignCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);
        var r = await _db.BlueStickerReports.Include(x => x.Transitions)
            .FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");

        if (r.State != BlueStickerReportState.AwaitingClientSignature)
            throw new InvalidOperationException(
                $"Report is not awaiting a client signature (state {r.State}).");
        if (r.ClientOtpHash is null || r.ClientOtpExpiresAtUtc is null)
            throw new InvalidOperationException("No OTP has been requested for this report.");
        if (_clock.UtcNow > r.ClientOtpExpiresAtUtc)
            throw new InvalidOperationException("OTP has expired — request a new one.");
        if (r.ClientOtpAttempts >= MaxAttempts)
            throw new InvalidOperationException(
                "Too many incorrect attempts — request a new OTP.");

        if (!_otp.Verify(command.Body.Otp, r.ClientOtpHash))
        {
            r.RecordOtpAttempt();
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Incorrect OTP.");
        }

        var sm = new BlueStickerReportStateMachine(r, _tenant, _clock);
        if (!sm.CanFire(BlueStickerReportTrigger.VerifyOtpAndSign))
            throw new InvalidOperationException(
                $"Cannot finalize from state {r.State}.");
        sm.Fire(BlueStickerReportTrigger.VerifyOtpAndSign);
        r.CaptureClientSignature(command.Body.ReceiverSignaturePng,
            DateOnly.FromDateTime(_clock.UtcNow));

        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build TuvInspection.slnx`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs
git commit -m "feat(blue-sticker): OTP request + verify-and-sign handlers"
```

---

# Phase 5 — API

### Task 15: BlueStickerReportsController

**Files:**
- Create: `src/TuvInspection.Api/Controllers/BlueStickerReportsController.cs`

Mirror `src/TuvInspection.Api/Controllers/CertificatesController.cs` (dispatcher, `[Authorize]`, role attributes, `File(...)` for PDF). PDF renderer is injected (built in Phase 6 — add the field/endpoint now; the type is created in Task 17).

- [ ] **Step 1: Create the controller**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.BlueSticker;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/blue-sticker-reports")]
[Produces("application/json")]
public class BlueStickerReportsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly BlueStickerReportPdfRenderer _pdf;

    public BlueStickerReportsController(IDispatcher dispatcher, BlueStickerReportPdfRenderer pdf)
    { _dispatcher = dispatcher; _pdf = pdf; }

    [HttpGet]
    public Task<PagedResult<BlueStickerReportListItemDto>> List(
        [FromQuery] Guid? jobOrderId, [FromQuery] BlueStickerReportStateDto? state,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListBlueStickerReportsQuery(jobOrderId, state, search, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlueStickerReportDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetBlueStickerReportByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Coordinator},{Roles.Manager}")]
    public Task<IReadOnlyList<BlueStickerReportDetailDto>> Create(
        [FromBody] CreateBlueStickerReportsRequest body, CancellationToken ct) =>
        _dispatcher.Send(new CreateBlueStickerReportsCommand(body), ct);

    [HttpPut("{id:guid}/inspection")]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager}")]
    public Task<BlueStickerReportDetailDto> UpdateInspection(
        Guid id, [FromBody] UpdateBlueStickerInspectionRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateBlueStickerInspectionCommand(id, body), ct);

    [HttpPost("{id:guid}/transitions/{trigger}")]
    public Task<BlueStickerReportDetailDto> Transition(
        Guid id, string trigger, [FromBody] BlueStickerTransitionRequest? body, CancellationToken ct)
    {
        if (!Enum.TryParse<BlueStickerTriggerDto>(trigger, ignoreCase: true, out var t))
            throw new ArgumentException($"Unknown trigger '{trigger}'.");
        return _dispatcher.Send(new FireBlueStickerTriggerCommand(id, t, body), ct);
    }

    [HttpPost("{id:guid}/request-otp")]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager}")]
    public Task<BlueStickerReportDetailDto> RequestOtp(Guid id, CancellationToken ct) =>
        _dispatcher.Send(new RequestClientOtpCommand(id), ct);

    [HttpPost("{id:guid}/verify-and-sign")]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager}")]
    public Task<BlueStickerReportDetailDto> VerifyAndSign(
        Guid id, [FromBody] VerifyOtpAndSignRequest body, CancellationToken ct) =>
        _dispatcher.Send(new VerifyOtpAndSignCommand(id, body), ct);

    [HttpGet("{id:guid}/report.pdf")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetBlueStickerReportByIdQuery(id), ct);
        if (dto is null) return NotFound();
        var bytes = await _pdf.RenderAsync(dto, ct);
        return File(bytes, "application/pdf", $"{dto.ReportNo}-Annex1.pdf");
    }
}
```

> This will not compile until Task 17 creates `BlueStickerReportPdfRenderer`. That is intentional — Phase 6 completes the controller. Do **not** commit a broken build; this task's commit happens after Task 17 (see Task 17 Step 5). Skip the commit here.

- [ ] **Step 2: Build (expected to fail until Task 17)**

Run: `dotnet build src/TuvInspection.Api`
Expected: FAIL — `BlueStickerReportPdfRenderer` not found. Proceed to Phase 6.

---

# Phase 6 — PDF

### Task 16: BlueStickerReportTemplateFiller

**Files:**
- Create: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportTemplateFiller.cs`

Reuse the **already-embedded** `Annex1.docx` (resource name `TuvInspection.Infrastructure.Certificates.Templates.Annex1.docx`). Mirror `Annex1TemplateFiller.cs` exactly — same row-index map, same `FillRowCells`/`SetCellText`. Add image insertion for the three signature cells (row 14).

- [ ] **Step 1: Create the filler (text + signature images)**

```csharp
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>
/// Fills the embedded official Annex 1 (MS0053813) docx with BlueStickerReport values.
/// Reuses the same template + positional row map as Annex1TemplateFiller; adds the three
/// signature images. Output is docx bytes for Gotenberg.
/// </summary>
public sealed class BlueStickerReportTemplateFiller
{
    private const string TemplateResourceName =
        "TuvInspection.Infrastructure.Certificates.Templates.Annex1.docx";

    public byte[] Fill(BlueStickerReportDetailDto r)
    {
        using var output = new MemoryStream();
        using (var template = typeof(global::TuvInspection.Infrastructure.Certificates
                   .Annex1TemplateFiller).Assembly
                   .GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{TemplateResourceName}' not found."))
        {
            template.CopyTo(output);
        }
        output.Position = 0;

        using (var doc = WordprocessingDocument.Open(output, isEditable: true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var table = body.Elements<Table>().FirstOrDefault()
                ?? throw new InvalidOperationException("Annex 1 template has no table.");
            var rows = table.Elements<TableRow>().ToList();

            FillRowCells(rows[2], r.TuvJobOrderNo, r.AramcoCategoryNo, r.OrgCode, r.RpoNo,
                r.CrmNo, r.ReportNo);
            FillRowCells(rows[4], r.DepartmentContractor,
                r.InspectionDate?.ToString("dd MMM yyyy"),
                r.InspectionTime?.ToString("HH:mm"),
                r.PreviousStickerNo, r.PreviousStickerIssuedBy);
            FillRowCells(rows[6], r.AreaOfInspection, r.EquipmentIdNo, r.Capacity,
                r.EquipmentLocation, ResultLabel(r.Result), r.NewStickerNo);
            FillRowCells(rows[8], r.Manufacturer, r.Model, r.EquipmentType,
                r.EquipmentSerialNo, r.StickerExpirationDate?.ToString("dd MMM yyyy"));
            FillRowCells(rows[11], r.Deficiencies, r.CorrectiveActionsTaken);
            FillRowCells(rows[14], r.ReceiverName, r.ReceiverBadgeNo, r.ReceiverTelephone,
                r.InspectorName, r.InspectorSapNo, r.InspectorTelephone,
                r.TechnicalReviewerName, r.ReceivedDate?.ToString("dd MMM yyyy"),
                r.ReviewedDate?.ToString("dd MMM yyyy"));

            // Signature row (row 15 = "Signature" placeholders, 3 cells: Receiver/Inspector/Reviewer)
            var sigCells = rows[15].Elements<TableCell>().ToList();
            PlaceSignature(doc, sigCells.ElementAtOrDefault(0), r.ReceiverSignaturePng);
            PlaceSignature(doc, sigCells.ElementAtOrDefault(1), r.InspectorSignaturePng);
            PlaceSignature(doc, sigCells.ElementAtOrDefault(2), r.TechnicalReviewerSignaturePng);

            doc.MainDocumentPart.Document.Save();
        }
        return output.ToArray();
    }

    private static void FillRowCells(TableRow row, params string?[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        for (var i = 0; i < values.Length && i < cells.Count; i++)
            SetCellText(cells[i], values[i]);
    }

    private static void SetCellText(TableCell cell, string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim();
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
        if (paragraph is null) { paragraph = new Paragraph(); cell.Append(paragraph); }
        var existingRun = paragraph.Elements<Run>().FirstOrDefault();
        var rPr = existingRun?.RunProperties?.CloneNode(true) as RunProperties;
        foreach (var run in paragraph.Elements<Run>().ToList()) run.Remove();
        foreach (var extra in cell.Elements<Paragraph>().Skip(1).ToList()) extra.Remove();
        var newRun = new Run();
        if (rPr is not null) newRun.AppendChild(rPr);
        newRun.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.AppendChild(newRun);
    }

    private static void PlaceSignature(WordprocessingDocument doc, TableCell? cell, string? dataUrl)
    {
        if (cell is null || string.IsNullOrWhiteSpace(dataUrl)) return;
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return;
        byte[] png;
        try { png = Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch { return; }

        var part = doc.MainDocumentPart!.AddImagePart(ImagePartType.Png);
        using (var s = new MemoryStream(png)) part.FeedData(s);
        var relId = doc.MainDocumentPart.GetIdOfPart(part);

        long cx = 1200000, cy = 400000; // ~1.25in x 0.42in EMUs — fits the signature cell
        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.DocProperties { Id = (UInt32Value)(uint)Random.Shared.Next(1, 100000),
                    Name = "sig" },
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = "sig.png" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList())
                                { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U,
              DistanceFromLeft = 0U, DistanceFromRight = 0U });

        var para = cell.Elements<Paragraph>().FirstOrDefault();
        if (para is null) { para = new Paragraph(); cell.Append(para); }
        foreach (var rn in para.Elements<Run>().ToList()) rn.Remove();
        para.AppendChild(new Run(drawing));
    }

    private static string ResultLabel(BlueStickerResultDto r) => r switch
    {
        BlueStickerResultDto.Pass => "PASS",
        BlueStickerResultDto.Fail => "FAIL",
        _ => "—",
    };
}
```

> Row indices 0–15 are the documented Annex 1 map from `Annex1TemplateFiller`. If a row index is off (template revised), open `Annex1TemplateFiller.cs` and use its exact indices — they share the same template.

- [ ] **Step 2: Build**

Run: `dotnet build src/TuvInspection.Infrastructure`
Expected: Build succeeded (OpenXml drawing types resolve — same package the existing filler uses).

- [ ] **Step 3: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportTemplateFiller.cs
git commit -m "feat(blue-sticker): Annex 1 docx template filler with signature images"
```

---

### Task 17: BlueStickerReportPdfRenderer + DI + finish controller

**Files:**
- Create: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportPdfRenderer.cs`
- Modify: `src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs`

Mirror `AramcoReportPdfRenderer` (try Gotenberg, fallback QuestPDF). Reuse the existing `GotenbergClient`.

- [ ] **Step 1: Create the renderer**

```csharp
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Infrastructure.Certificates;   // GotenbergClient

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>
/// Renders the Blue Sticker Annex 1 PDF. Primary: fill the official docx + Gotenberg.
/// Fallback: inline QuestPDF (same 11-column grid) when Gotenberg is unreachable.
/// </summary>
public sealed class BlueStickerReportPdfRenderer
{
    private static readonly int[] Cols =
        { 3187, 1133, 1260, 30, 1784, 110, 1305, 1431, 2540, 1440, 1525 };

    private readonly BlueStickerReportTemplateFiller _filler;
    private readonly GotenbergClient _gotenberg;
    private readonly ILogger<BlueStickerReportPdfRenderer> _log;

    static BlueStickerReportPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public BlueStickerReportPdfRenderer(BlueStickerReportTemplateFiller filler,
        GotenbergClient gotenberg, ILogger<BlueStickerReportPdfRenderer> log)
    { _filler = filler; _gotenberg = gotenberg; _log = log; }

    public async Task<byte[]> RenderAsync(BlueStickerReportDetailDto r, CancellationToken ct = default)
    {
        try
        {
            var docx = _filler.Fill(r);
            return await _gotenberg.ConvertDocxToPdfAsync(docx, $"{r.ReportNo}-Annex1.docx", ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Blue Sticker Annex 1 docx→pdf via gotenberg failed for {Report}; using fallback.",
                r.ReportNo);
            return RenderFallback(r);
        }
    }

    private byte[] RenderFallback(BlueStickerReportDetailDto r) =>
        Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.A4.Landscape());
            p.Margin(20);
            p.Content().Table(t =>
            {
                t.ColumnsDefinition(cd => { foreach (var w in Cols) cd.RelativeColumn(w); });
                t.Cell().ColumnSpan(11).Background("#548DD4").Padding(8).AlignCenter()
                    .Text("LIFTING EQUIPMENT INSPECTION REPORT").FontSize(13).Bold()
                    .FontColor("#FFFFFF");
                void L(int s, string v) => t.Cell().ColumnSpan((uint)s).Background("#D9D9D9")
                    .Border(0.4f).Padding(4).AlignCenter().Text(v).FontSize(8).Bold();
                void D(int s, string? v) => t.Cell().ColumnSpan((uint)s).Border(0.4f)
                    .Padding(6).MinHeight(22).Text(string.IsNullOrWhiteSpace(v) ? " " : v)
                    .FontSize(9).Bold();
                L(1, "TUV Job Order. No."); L(3, "Aramco Category No."); L(2, "Org. Code");
                L(2, "RPO NO."); L(1, "CRM No."); L(2, "Report No.");
                D(1, r.TuvJobOrderNo); D(3, r.AramcoCategoryNo); D(2, r.OrgCode);
                D(2, r.RpoNo); D(1, r.CrmNo); D(2, r.ReportNo);
                L(4, "Department / Contractor"); L(2, "Inspection Date"); L(2, "Inspection Time");
                L(1, "Previous Sticker No."); L(2, "Previous Sticker Issued By");
                D(4, r.DepartmentContractor); D(2, r.InspectionDate?.ToString("dd MMM yyyy"));
                D(2, r.InspectionTime?.ToString("HH:mm")); D(1, r.PreviousStickerNo);
                D(2, r.PreviousStickerIssuedBy);
                L(1, "Area of Inspection"); L(3, "Equipment ID No."); L(2, "Capacity");
                L(2, "Equipment Location"); L(1, "Inspection Result"); L(2, "New Sticker No.");
                D(1, r.AreaOfInspection); D(3, r.EquipmentIdNo); D(2, r.Capacity);
                D(2, r.EquipmentLocation); D(1, r.Result.ToString()); D(2, r.NewStickerNo);
                L(1, "Manufacturer"); L(3, "Model"); L(4, "Equipment Type");
                L(1, "Equipment Serial No."); L(2, "Sticker Expiration Date");
                D(1, r.Manufacturer); D(3, r.Model); D(4, r.EquipmentType);
                D(1, r.EquipmentSerialNo); D(2, r.StickerExpirationDate?.ToString("dd MMM yyyy"));
                t.Cell().ColumnSpan(8).Background("#548DD4").Padding(4).AlignCenter()
                    .Text("DEFICIENCES / OBSERVATIONS").FontSize(9).Bold().FontColor("#FFFFFF");
                t.Cell().ColumnSpan(3).Background("#548DD4").Padding(4).AlignCenter()
                    .Text("CORRECTIVE ACTION TAKEN").FontSize(9).Bold().FontColor("#FFFFFF");
                t.Cell().ColumnSpan(8).Border(0.4f).Padding(8).MinHeight(60)
                    .Text(r.Deficiencies ?? " ").FontSize(8.5f);
                t.Cell().ColumnSpan(3).Border(0.4f).Padding(8).MinHeight(60)
                    .Text(r.CorrectiveActionsTaken ?? " ").FontSize(8.5f);
            });
        })).GeneratePdf();
}
```

- [ ] **Step 2: Register DI**

In `InfrastructureModule.cs`, next to `services.AddScoped<AramcoReportPdfRenderer>();` add:

```csharp
services.AddSingleton<TuvInspection.Infrastructure.BlueSticker.BlueStickerReportTemplateFiller>();
services.AddScoped<TuvInspection.Infrastructure.BlueSticker.BlueStickerReportPdfRenderer>();
```

- [ ] **Step 3: Build the whole solution (controller from Task 15 now compiles)**

Run: `dotnet build TuvInspection.slnx`
Expected: Build succeeded.

- [ ] **Step 4: Smoke test the API boots**

Run: `dotnet run --project src/TuvInspection.Api --launch-profile http` (Ctrl+C after "Now listening"). Confirm no DI resolution error for `BlueStickerReportsController`.

- [ ] **Step 5: Commit (renderer + controller together)**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportPdfRenderer.cs src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs src/TuvInspection.Api/Controllers/BlueStickerReportsController.cs
git commit -m "feat(blue-sticker): PDF renderer (gotenberg + fallback) + API controller"
```

---

# Phase 7 — Frontend (Angular)

### Task 18: Models + API service

**Files:**
- Create: `web/src/app/core/models/blue-sticker.models.ts`
- Create: `web/src/app/core/api/blue-sticker.api.ts`

Mirror `web/src/app/core/api/certificates.api.ts` (inject HttpClient, `environment.apiBaseUrl`, blob for PDF).

- [ ] **Step 1: Create models**

```typescript
export enum BlueStickerState {
  Draft = 0, InProgress = 1, UnderReview = 2, Approved = 3,
  AwaitingClientSignature = 4, ClientSigned = 5, Rejected = 6, Voided = 7,
}
export const BlueStickerStateName: Record<number, string> = {
  0: 'Draft', 1: 'In progress', 2: 'Under review', 3: 'Approved',
  4: 'Awaiting client signature', 5: 'Client signed', 6: 'Rejected', 7: 'Voided',
};
export enum BlueStickerResult { NotSet = 0, Pass = 1, Fail = 2 }

export interface CreateBlueStickerReportsRequest {
  jobOrderId: string;
  orgCode?: string | null;
  rpoNo?: string | null;
  crmNo?: string | null;
  departmentContractor?: string | null;
}
export interface UpdateBlueStickerInspectionRequest {
  areaOfInspection?: string | null;
  result: BlueStickerResult;
  deficiencies?: string | null;
  correctiveActionsTaken?: string | null;
  equipmentLocation?: string | null;
  receiverName?: string | null;
  receiverBadgeNo?: string | null;
  receiverTelephone?: string | null;
  inspectorTelephone?: string | null;
}
export interface BlueStickerReportDetail {
  id: string; reportNo: string; jobOrderId: string; equipmentId: string;
  tuvJobOrderNo: string; aramcoCategoryNo?: string | null;
  orgCode?: string | null; rpoNo?: string | null; crmNo?: string | null;
  departmentContractor?: string | null;
  inspectionDate?: string | null; inspectionTime?: string | null;
  areaOfInspection?: string | null; result: BlueStickerResult;
  equipmentIdNo: string; capacity?: string | null; equipmentLocation?: string | null;
  manufacturer?: string | null; model?: string | null; equipmentType?: string | null;
  equipmentSerialNo?: string | null; newStickerNo?: string | null;
  stickerExpirationDate?: string | null;
  deficiencies?: string | null; correctiveActionsTaken?: string | null;
  receiverName?: string | null; receiverBadgeNo?: string | null;
  receiverTelephone?: string | null; inspectorName?: string | null;
  inspectorSapNo?: string | null; inspectorTelephone?: string | null;
  technicalReviewerName?: string | null;
  receivedDate?: string | null; reviewedDate?: string | null;
  receiverSignaturePng?: string | null; inspectorSignaturePng?: string | null;
  technicalReviewerSignaturePng?: string | null;
  state: BlueStickerState; createdAtUtc: string;
}
export interface BlueStickerReportListItem {
  id: string; reportNo: string; tuvJobOrderNo: string; equipmentIdNo: string;
  state: BlueStickerState; inspectionDate?: string | null; createdAtUtc: string;
}
export type BlueStickerTrigger =
  'StartInspection' | 'SubmitForReview' | 'Approve' | 'Reject' | 'Void';
```

- [ ] **Step 2: Create the API service**

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  BlueStickerReportDetail, BlueStickerReportListItem, BlueStickerTrigger,
  CreateBlueStickerReportsRequest, UpdateBlueStickerInspectionRequest,
} from '../models/blue-sticker.models';

@Injectable({ providedIn: 'root' })
export class BlueStickerApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/blue-sticker-reports`;

  list(filters: { jobOrderId?: string; state?: number; search?: string;
    page?: number; pageSize?: number } = {}): Observable<PagedResult<BlueStickerReportListItem>> {
    let p = new HttpParams();
    Object.entries(filters).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
    });
    return this.http.get<PagedResult<BlueStickerReportListItem>>(this.base, { params: p });
  }
  get(id: string) {
    return this.http.get<BlueStickerReportDetail>(`${this.base}/${id}`);
  }
  create(body: CreateBlueStickerReportsRequest) {
    return this.http.post<BlueStickerReportDetail[]>(this.base, body);
  }
  updateInspection(id: string, body: UpdateBlueStickerInspectionRequest) {
    return this.http.put<BlueStickerReportDetail>(`${this.base}/${id}/inspection`, body);
  }
  transition(id: string, trigger: BlueStickerTrigger, comments?: string,
    inspectorSignaturePng?: string, technicalReviewerSignaturePng?: string) {
    return this.http.post<BlueStickerReportDetail>(
      `${this.base}/${id}/transitions/${trigger}`,
      { comments: comments ?? null,
        inspectorSignaturePng: inspectorSignaturePng ?? null,
        technicalReviewerSignaturePng: technicalReviewerSignaturePng ?? null });
  }
  requestOtp(id: string) {
    return this.http.post<BlueStickerReportDetail>(`${this.base}/${id}/request-otp`, {});
  }
  verifyAndSign(id: string, otp: string, receiverSignaturePng: string) {
    return this.http.post<BlueStickerReportDetail>(
      `${this.base}/${id}/verify-and-sign`, { otp, receiverSignaturePng });
  }
  pdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/report.pdf`, { responseType: 'blob' });
  }
}
```

> Confirm `PagedResult` import path by opening `web/src/app/core/api/certificates.api.ts` (it imports from `../models/common.models`).

- [ ] **Step 3: Build**

Run: `cd web && npx ng build --configuration development` (or rely on the running `ng serve` recompile — check `/tmp/tuv-web.log` for "Application bundle generation complete" with no errors).
Expected: compiles, no TS errors.

- [ ] **Step 4: Commit**

```bash
git add web/src/app/core/models/blue-sticker.models.ts web/src/app/core/api/blue-sticker.api.ts
git commit -m "feat(blue-sticker): Angular models + API client"
```

---

### Task 19: Job order create — add admin fields when Blue Sticker

**Files:**
- Modify: `web/src/app/features/job-management/pages/job-orders.page.ts`

When the selected service includes Blue Sticker (value `2` or `7`), show Org Code / RPO / CRM / Department inputs in the create dialog and, after the job order is created, call `BlueStickerApi.create({ jobOrderId, orgCode, rpoNo, crmNo, departmentContractor })`.

- [ ] **Step 1: Add admin-field state + inputs**

In `JobOrdersPage`, add `private bsApi = inject(BlueStickerApi);` (import it) and fields:

```typescript
  protected newOrgCode = '';
  protected newRpoNo = '';
  protected newCrmNo = '';
  protected newDepartment = '';
  protected isBlueSticker = () => this.newService === 2 || this.newService === 7;
```

In the create dialog template, after the Location input, add:

```html
<ng-container *ngIf="isBlueSticker()">
  <label>Org Code</label><input pInputText [(ngModel)]="newOrgCode" />
  <label>RPO No.</label><input pInputText [(ngModel)]="newRpoNo" />
  <label>CRM No.</label><input pInputText [(ngModel)]="newCrmNo" />
  <label>Department / Contractor</label><input pInputText [(ngModel)]="newDepartment" />
</ng-container>
```

- [ ] **Step 2: After job-order creation, create Blue Sticker reports**

Replace the `next:` callback in `createOrder()` so that when `isBlueSticker()` it chains the report creation:

```typescript
      next: (jo) => {
        this.creating.set(false);
        this.newDialog = false;
        if (this.isBlueSticker()) {
          this.bsApi.create({
            jobOrderId: jo.id, orgCode: this.newOrgCode || null,
            rpoNo: this.newRpoNo || null, crmNo: this.newCrmNo || null,
            departmentContractor: this.newDepartment || null,
          }).subscribe({
            next: (reports) => this.notify.success(
              `Created ${jo.jobOrderNo} + ${reports.length} Blue Sticker report(s)`),
            error: (err) => showHttpError(this.notify, err),
          });
        } else {
          this.notify.success(`Created ${jo.jobOrderNo}`);
        }
        this.newLocation = '';
        this.refresh();
      },
```

> `jo.id` must be the created job order's GUID — confirm the create response field name in `job-management.models.ts` / the existing `createOrder` (it logs `jo.jobOrderNo`, so the object has the order; use its id field).

- [ ] **Step 3: Build (web) + manual check**

Recompile (ng serve). In the browser at http://localhost:4201, create a Blue Sticker job order for a client that has Aramco-categorised equipment; confirm the success toast shows N reports.
Expected: no console errors; toast shows report count.

- [ ] **Step 4: Commit**

```bash
git add web/src/app/features/job-management/pages/job-orders.page.ts
git commit -m "feat(blue-sticker): coordinator enters admin fields + auto-creates reports"
```

---

### Task 20: Blue Sticker list page + route + nav

**Files:**
- Create: `web/src/app/features/blue-sticker/pages/blue-sticker-list.page.ts`
- Modify: `web/src/app/app.routes.ts`
- Modify: nav (find the shell/sidebar component: `grep -rn "job-orders" web/src/app/core/layout`)

Mirror `job-orders.page.ts` structure (standalone, signals, PrimeNG table).

- [ ] **Step 1: Create the list page**

```typescript
import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import { BlueStickerReportListItem, BlueStickerStateName } from '../../../core/models/blue-sticker.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [CommonModule, DatePipe, ButtonModule, TableModule],
  template: `
    <h2>Blue Sticker Inspections</h2>
    <div class="card">
      @if (loading()) { <div class="loader">Loading…</div> }
      @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr><th>Report</th><th>Job Order</th><th>Equipment</th>
              <th>Inspection date</th><th>State</th><th></th></tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>{{ r.reportNo }}</td>
              <td>{{ r.tuvJobOrderNo }}</td>
              <td>{{ r.equipmentIdNo }}</td>
              <td>{{ r.inspectionDate ? (r.inspectionDate | date: 'dd MMM yyyy') : '—' }}</td>
              <td>{{ stateName(r.state) }}</td>
              <td>
                <p-button label="Open" size="small" [text]="true"
                  (onClick)="open(r)" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [`.card{background:#fff;border:1px solid #e5e9f2;border-radius:14px;padding:1rem}
    .loader{padding:2rem;text-align:center;color:#64748b}`],
})
export class BlueStickerListPage {
  private api = inject(BlueStickerApi);
  private notify = inject(NotifyService);
  private router = inject(Router);
  protected loading = signal(true);
  protected rows = signal<BlueStickerReportListItem[]>([]);
  protected stateName = (s: number) => BlueStickerStateName[s];

  constructor() {
    this.api.list({ pageSize: 100 }).subscribe({
      next: (r) => { this.rows.set(r.items); this.loading.set(false); },
      error: (e) => { this.loading.set(false); showHttpError(this.notify, e); },
    });
  }
  open(r: BlueStickerReportListItem) {
    this.router.navigate(['/blue-sticker', r.id]);
  }
}
```

- [ ] **Step 2: Add routes**

In `web/src/app/app.routes.ts`, inside the authenticated `children` array (next to the `job-orders` route), add:

```typescript
      {
        path: 'blue-sticker',
        loadComponent: () =>
          import('./features/blue-sticker/pages/blue-sticker-list.page')
            .then((m) => m.BlueStickerListPage),
      },
      {
        path: 'blue-sticker/:id',
        loadComponent: () =>
          import('./features/blue-sticker/pages/blue-sticker-fill.page')
            .then((m) => m.BlueStickerFillPage),
      },
      {
        path: 'blue-sticker/:id/finalize',
        loadComponent: () =>
          import('./features/blue-sticker/pages/blue-sticker-finalize.page')
            .then((m) => m.BlueStickerFinalizePage),
      },
```

(The fill/finalize pages are created in Tasks 21–22; the lazy import is fine to add now since routes are lazy.)

- [ ] **Step 3: Add a nav link**

In the sidebar/shell component that lists "Job Orders", add an entry linking to `/blue-sticker` labelled "Blue Sticker" (copy the exact pattern of the adjacent nav item).

- [ ] **Step 4: Build + manual check**

Recompile. Navigate to `/blue-sticker` — the list renders (rows from Task 19).
Expected: table shows created reports.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/features/blue-sticker/pages/blue-sticker-list.page.ts web/src/app/app.routes.ts web/src/app/core/layout/
git commit -m "feat(blue-sticker): list page + routes + nav"
```

---

### Task 21: Inspector fill page (start / fill / submit)

**Files:**
- Create: `web/src/app/features/blue-sticker/pages/blue-sticker-fill.page.ts`

Reuse `SignaturePad` for the inspector's own signature at submit.

- [ ] **Step 1: Create the fill page**

```typescript
import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import {
  BlueStickerReportDetail, BlueStickerResult, BlueStickerState,
} from '../../../core/models/blue-sticker.models';
import { SignaturePad } from '../../certificates/components/signature-pad.component';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, SelectModule, SignaturePad],
  template: `
    @if (r(); as rep) {
      <h2>{{ rep.reportNo }} — {{ rep.equipmentIdNo }}</h2>
      <p>State: <strong>{{ rep.state }}</strong></p>

      @if (rep.state === S.Draft) {
        <p-button label="Start inspection" icon="pi pi-play"
          (onClick)="fire('StartInspection')" [loading]="busy()" />
      }

      @if (rep.state === S.InProgress) {
        <div class="form">
          <label>Area of inspection</label>
          <input pInputText [(ngModel)]="form.areaOfInspection" />
          <label>Result</label>
          <p-select [options]="resultOptions" optionLabel="label" optionValue="value"
            [(ngModel)]="form.result" appendTo="body" />
          <label>Deficiencies / observations</label>
          <input pInputText [(ngModel)]="form.deficiencies" />
          <label>Corrective action taken</label>
          <input pInputText [(ngModel)]="form.correctiveActionsTaken" />
          <label>Equipment location</label>
          <input pInputText [(ngModel)]="form.equipmentLocation" />
          <label>Receiver name</label>
          <input pInputText [(ngModel)]="form.receiverName" />
          <label>Receiver badge No.</label>
          <input pInputText [(ngModel)]="form.receiverBadgeNo" />
          <label>Receiver telephone</label>
          <input pInputText [(ngModel)]="form.receiverTelephone" />
          <label>Inspector telephone</label>
          <input pInputText [(ngModel)]="form.inspectorTelephone" />
          <p-button label="Save" icon="pi pi-save" (onClick)="save()" [loading]="busy()" />

          <h3>Inspector signature (sign before submitting)</h3>
          <tuv-signature-pad (commitSignature)="inspectorSig.set($event)" />
          <p-button label="Submit to technical reviewer" icon="pi pi-send"
            [disabled]="!inspectorSig()" [loading]="busy()"
            (onClick)="submit()" />
        </div>
      }

      @if (rep.state === S.UnderReview) {
        <p>Submitted — awaiting technical reviewer.</p>
      }
      @if (rep.state === S.Approved || rep.state === S.AwaitingClientSignature) {
        <p-button label="Go to client signing" icon="pi pi-pencil"
          (onClick)="goFinalize()" />
      }
      @if (rep.state === S.ClientSigned) {
        <p-button label="Download Annex 1 PDF" icon="pi pi-file-pdf"
          (onClick)="download()" />
      }
    } @else { <p>Loading…</p> }
  `,
  styles: [`.form{display:flex;flex-direction:column;gap:.5rem;max-width:560px}
    label{font-size:.85rem;color:#334155;margin-top:.3rem}`],
})
export class BlueStickerFillPage {
  private api = inject(BlueStickerApi);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private notify = inject(NotifyService);

  protected S = BlueStickerState;
  protected r = signal<BlueStickerReportDetail | null>(null);
  protected busy = signal(false);
  protected inspectorSig = signal<string | null>(null);
  private id = this.route.snapshot.paramMap.get('id')!;

  protected resultOptions = [
    { label: 'Pass', value: BlueStickerResult.Pass },
    { label: 'Fail', value: BlueStickerResult.Fail },
  ];
  protected form: any = { result: BlueStickerResult.Pass };

  constructor() { this.load(); }

  private load() {
    this.api.get(this.id).subscribe({
      next: (rep) => {
        this.r.set(rep);
        this.form = {
          areaOfInspection: rep.areaOfInspection ?? '',
          result: rep.result || BlueStickerResult.Pass,
          deficiencies: rep.deficiencies ?? '',
          correctiveActionsTaken: rep.correctiveActionsTaken ?? '',
          equipmentLocation: rep.equipmentLocation ?? '',
          receiverName: rep.receiverName ?? '',
          receiverBadgeNo: rep.receiverBadgeNo ?? '',
          receiverTelephone: rep.receiverTelephone ?? '',
          inspectorTelephone: rep.inspectorTelephone ?? '',
        };
      },
      error: (e) => showHttpError(this.notify, e),
    });
  }
  fire(trigger: any) {
    this.busy.set(true);
    this.api.transition(this.id, trigger).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
  save() {
    this.busy.set(true);
    this.api.updateInspection(this.id, this.form).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Saved'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
  submit() {
    this.busy.set(true);
    this.api.updateInspection(this.id, this.form).subscribe({
      next: () => this.api.transition(this.id, 'SubmitForReview', undefined,
        this.inspectorSig() ?? undefined).subscribe({
          next: (rep) => { this.r.set(rep); this.busy.set(false);
            this.notify.success('Submitted'); },
          error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
        }),
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
  goFinalize() { this.router.navigate(['/blue-sticker', this.id, 'finalize']); }
  download() {
    this.api.pdf(this.id).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    });
  }
}
```

- [ ] **Step 2: Build + manual check**

Recompile. As an Inspector, open a Draft report → Start inspection → fill → sign → Submit. Confirm state goes Draft→InProgress→UnderReview.
Expected: no console errors; state transitions visible.

- [ ] **Step 3: Commit**

```bash
git add web/src/app/features/blue-sticker/pages/blue-sticker-fill.page.ts
git commit -m "feat(blue-sticker): inspector fill page (start/fill/submit)"
```

---

### Task 22: On-site Finalize tablet page (OTP + signature + auto-submit)

**Files:**
- Create: `web/src/app/features/blue-sticker/pages/blue-sticker-finalize.page.ts`

Single tablet-first screen: request OTP → enter OTP → client signs → auto verify-and-sign.

- [ ] **Step 1: Create the finalize page**

```typescript
import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import { BlueStickerReportDetail, BlueStickerState } from '../../../core/models/blue-sticker.models';
import { SignaturePad } from '../../certificates/components/signature-pad.component';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, SignaturePad],
  template: `
    <div class="finalize">
      @if (r(); as rep) {
        <h1>{{ rep.reportNo }}</h1>
        <p class="sub">{{ rep.equipmentIdNo }} · {{ rep.tuvJobOrderNo }}</p>

        @if (rep.state === S.Approved) {
          <p>Step 1 — send the verification code to the client's email.</p>
          <p-button label="Send OTP to client" icon="pi pi-envelope" size="large"
            [loading]="busy()" (onClick)="requestOtp()" />
        }

        @if (rep.state === S.AwaitingClientSignature) {
          <div class="otp">
            <label>Step 2 — enter the code the client received</label>
            <input pInputText inputmode="numeric" maxlength="6"
              [(ngModel)]="otp" placeholder="••••••" class="otp-input" />
            <p-button label="Resend" [text]="true" size="small"
              (onClick)="requestOtp()" [loading]="busy()" />
          </div>
          <div class="sign">
            <label>Step 3 — hand the tablet to the client to sign</label>
            <tuv-signature-pad (commitSignature)="onClientSign($event)" />
          </div>
        }

        @if (rep.state === S.ClientSigned) {
          <div class="done">
            <i class="pi pi-check-circle"></i>
            <h2>Signed & submitted</h2>
            <p-button label="Download Annex 1 PDF" icon="pi pi-file-pdf"
              size="large" (onClick)="download()" />
          </div>
        }
      } @else { <p>Loading…</p> }
    </div>
  `,
  styles: [`
    .finalize{max-width:760px;margin:0 auto;padding:1.5rem;display:flex;
      flex-direction:column;gap:1.1rem}
    h1{font-size:1.6rem;margin:0}
    .sub{color:#64748b;margin:0}
    .otp{display:flex;flex-direction:column;gap:.5rem}
    .otp-input{font-size:1.8rem;letter-spacing:.4rem;text-align:center;
      padding:.7rem;width:220px}
    .sign{display:flex;flex-direction:column;gap:.5rem}
    label{font-weight:600;color:#0f172a}
    .done{text-align:center;color:#16a34a}
    .done .pi{font-size:3rem}
    :host ::ng-deep p-button button{min-height:48px;font-size:1.05rem}
  `],
})
export class BlueStickerFinalizePage {
  private api = inject(BlueStickerApi);
  private route = inject(ActivatedRoute);
  private notify = inject(NotifyService);
  protected S = BlueStickerState;
  protected r = signal<BlueStickerReportDetail | null>(null);
  protected busy = signal(false);
  protected otp = '';
  private id = this.route.snapshot.paramMap.get('id')!;

  constructor() { this.load(); }

  private load() {
    this.api.get(this.id).subscribe({
      next: (rep) => this.r.set(rep),
      error: (e) => showHttpError(this.notify, e),
    });
  }
  requestOtp() {
    this.busy.set(true);
    this.api.requestOtp(this.id).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('OTP emailed to the client'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
  onClientSign(dataUrl: string) {
    if (!/^\d{6}$/.test(this.otp.trim())) {
      this.notify.error('Enter the 6-digit OTP before the client signs.');
      return;
    }
    this.busy.set(true);
    this.api.verifyAndSign(this.id, this.otp.trim(), dataUrl).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Report signed & submitted'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
  download() {
    this.api.pdf(this.id).subscribe((blob) =>
      window.open(URL.createObjectURL(blob), '_blank'));
  }
}
```

- [ ] **Step 2: Build + manual check (golden path)**

Recompile. With a report in `Approved` (after a reviewer approves — Task 21 path + a TechReviewer user approving via the transition endpoint), open `/blue-sticker/:id/finalize`:
1. Click "Send OTP to client" → check MailHog at http://localhost:8025 for the code.
2. Enter the code, sign on the pad → state becomes ClientSigned.
3. Download the PDF → Annex 1 layout with the three signatures.
Expected: full golden path works in the browser; MailHog shows the OTP email.

- [ ] **Step 3: Commit**

```bash
git add web/src/app/features/blue-sticker/pages/blue-sticker-finalize.page.ts
git commit -m "feat(blue-sticker): on-site finalize tablet screen (OTP + client signature)"
```

---

### Task 23: Reviewer approve action

**Files:**
- Modify: `web/src/app/features/blue-sticker/pages/blue-sticker-fill.page.ts`

Add Approve/Reject buttons visible when the report is `UnderReview` and the current user is TechReviewer/Manager. Reuse the existing `AuthService.hasAnyRole`.

- [ ] **Step 1: Add reviewer controls**

Add to imports/inject: `import { AuthService } from '../../../core/auth/auth.service'; import { Roles } from '../../../core/models/auth.models';` and `protected auth = inject(AuthService);`. Add a reviewer signature signal: `protected reviewerSig = signal<string | null>(null);`

In the template, replace the `@if (rep.state === S.UnderReview)` block with:

```html
@if (rep.state === S.UnderReview) {
  @if (auth.hasAnyRole([Roles.TechReviewer, Roles.Manager])) {
    <h3>Technical reviewer signature</h3>
    <tuv-signature-pad (commitSignature)="reviewerSig.set($event)" />
    <p-button label="Approve" icon="pi pi-check" severity="success"
      [disabled]="!reviewerSig()" [loading]="busy()"
      (onClick)="approve()" />
    <p-button label="Reject" icon="pi pi-times" severity="danger" [text]="true"
      [loading]="busy()" (onClick)="fire('Reject')" />
  } @else {
    <p>Submitted — awaiting technical reviewer.</p>
  }
}
```

Add the `Roles` reference for the template (`protected Roles = Roles;`) and the method:

```typescript
  approve() {
    this.busy.set(true);
    this.api.transition(this.id, 'Approve', undefined, undefined,
      this.reviewerSig() ?? undefined).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Approved — sticker issued'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
```

- [ ] **Step 2: Build + manual check**

Recompile. As a TechReviewer, open an `UnderReview` report → sign → Approve → state becomes `Approved`, `newStickerNo` populated.
Expected: approval works; sticker number shown on reload.

- [ ] **Step 3: Commit**

```bash
git add web/src/app/features/blue-sticker/pages/blue-sticker-fill.page.ts
git commit -m "feat(blue-sticker): technical reviewer approve/reject UI"
```

---

# Phase 8 — Integration Tests

### Task 24: End-to-end 9-step integration test

**Files:**
- Create: `tests/TuvInspection.IntegrationTests/BlueSticker/BlueStickerWorkflowTests.cs`

**Investigation:** open an existing API integration test (`grep -rln "WebApplicationFactory" tests`) and copy its fixture verbatim — Testcontainers SQL setup, auth-token helper (how it logs in / fakes roles), and `HttpClient` creation. The test below assumes a fixture exposing an authenticated client per role; adapt names to the real fixture.

- [ ] **Step 1: Write the end-to-end test**

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using TuvInspection.Contracts.BlueSticker;
using Xunit;

namespace TuvInspection.IntegrationTests.BlueSticker;

// Adapt the base/fixture to the existing API integration-test fixture in this project.
public class BlueStickerWorkflowTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fx;
    public BlueStickerWorkflowTests(ApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Full_nine_step_flow_reaches_ClientSigned_and_renders_pdf()
    {
        // Arrange: seed a client (with ContactEmail), Aramco-categorised equipment,
        // an unallocated sticker, and a Blue Sticker job order — via the fixture's
        // seeding helpers (mirror how other integration tests arrange data).
        var seed = await _fx.SeedBlueStickerScenarioAsync();

        var coordinator = _fx.ClientFor("Coordinator");
        var inspector = _fx.ClientFor("Inspector");
        var reviewer = _fx.ClientFor("TechReviewer");

        // Step 1: coordinator creates reports for the job order
        var createResp = await coordinator.PostAsJsonAsync("/api/blue-sticker-reports",
            new CreateBlueStickerReportsRequest(seed.JobOrderId, "ORG1", "RPO1", "CRM1", "Dept"));
        createResp.EnsureSuccessStatusCode();
        var reports = await createResp.Content
            .ReadFromJsonAsync<List<BlueStickerReportDetailDto>>();
        var id = reports!.Single().Id;

        // Step 3: inspector starts
        (await inspector.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{id}/transitions/StartInspection",
            new BlueStickerTransitionRequest(null, null, null))).EnsureSuccessStatusCode();

        // Step 4: fill
        (await inspector.PutAsJsonAsync(
            $"/api/blue-sticker-reports/{id}/inspection",
            new UpdateBlueStickerInspectionRequest("Yard A", BlueStickerResultDto.Pass,
                null, null, null, "Client Rep", "B-1", "0500000000", "0511111111")))
            .EnsureSuccessStatusCode();

        // Step 5: submit
        (await inspector.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{id}/transitions/SubmitForReview",
            new BlueStickerTransitionRequest(null, "data:image/png;base64,AAAA", null)))
            .EnsureSuccessStatusCode();

        // Step 6: reviewer approves (final)
        (await reviewer.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{id}/transitions/Approve",
            new BlueStickerTransitionRequest(null, null, "data:image/png;base64,BBBB")))
            .EnsureSuccessStatusCode();

        var afterApprove = await (await inspector.GetAsync(
            $"/api/blue-sticker-reports/{id}"))
            .Content.ReadFromJsonAsync<BlueStickerReportDetailDto>();
        afterApprove!.State.Should().Be(BlueStickerReportStateDto.Approved);
        afterApprove.NewStickerNo.Should().NotBeNullOrWhiteSpace("sticker auto-issued on approve");
        afterApprove.StickerExpirationDate.Should()
            .Be(afterApprove.InspectionDate!.Value.AddYears(1),
                "interim 1-year validity (spec §9 open dependency)");

        // Step 7: request OTP
        (await inspector.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{id}/request-otp", new { }))
            .EnsureSuccessStatusCode();

        // The OTP is emailed; in tests, read it from the fixture's outbox/email capture.
        var otp = await _fx.LastOtpForReportAsync(id);

        // Steps 8+9: verify + client signs (one call) → auto-submit
        var signResp = await inspector.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{id}/verify-and-sign",
            new VerifyOtpAndSignRequest(otp, "data:image/png;base64,CCCC"));
        signResp.EnsureSuccessStatusCode();

        var final = await signResp.Content
            .ReadFromJsonAsync<BlueStickerReportDetailDto>();
        final!.State.Should().Be(BlueStickerReportStateDto.ClientSigned);
        final.ReceivedDate.Should().NotBeNull();
        final.ReceiverSignaturePng.Should().StartWith("data:image/png");

        // PDF renders (fallback path is fine when gotenberg is absent in CI)
        var pdf = await inspector.GetByteArrayAsync(
            $"/api/blue-sticker-reports/{id}/report.pdf");
        pdf.Should().NotBeEmpty();
        pdf[..4].Should().Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
    }
}
```

> `ApiFixture`, `ClientFor`, `SeedBlueStickerScenarioAsync`, `LastOtpForReportAsync` are names to map onto the **existing** integration fixture. If the project has no API-level fixture yet (the research found only domain-level integration tests), create a minimal `WebApplicationFactory<Program>` fixture mirroring `Program`'s test hook (`public partial class Program {}`) and a Testcontainers SQL container, and expose: role-authenticated `HttpClient`s, a scenario seeder, and an OTP capture that reads the stored `ClientOtpHash` is NOT possible (hashed) — instead capture the code from the email outbox the fixture drains. Keep this helper minimal and scoped to the test project.

- [ ] **Step 2: Run the integration test**

Run: `dotnet test tests/TuvInspection.IntegrationTests --filter "FullyQualifiedName~BlueStickerWorkflowTests"`
Expected: PASS (Docker running for Testcontainers; Gotenberg optional — fallback yields a valid `%PDF`).

- [ ] **Step 3: Commit**

```bash
git add tests/TuvInspection.IntegrationTests/BlueSticker/
git commit -m "test(blue-sticker): end-to-end 9-step workflow + PDF integration test"
```

---

### Task 25: Final regression + cleanup

- [ ] **Step 1: Full build**

Run: `dotnet build TuvInspection.slnx`
Expected: Build succeeded, 0 warnings introduced by Blue Sticker files.

- [ ] **Step 2: Full unit suite**

Run: `dotnet test tests/TuvInspection.UnitTests`
Expected: all PASS (Blue Sticker state machine + OTP tests included).

- [ ] **Step 3: Full integration suite**

Run: `dotnet test tests/TuvInspection.IntegrationTests`
Expected: all PASS (existing certificate/sticker tests still green — Blue Sticker is additive, TPI untouched).

- [ ] **Step 4: Frontend build**

Run: `cd web && npx ng build`
Expected: bundle generated, no TS errors (only pre-existing NG8113 unused-import warnings allowed).

- [ ] **Step 5: Manual end-to-end in browser (golden path)**

With `docker compose up -d`, API on :5282, web on :4201:
Coordinator creates Blue Sticker job order (admin fields) → Inspector starts/fills/signs/submits → TechReviewer approves (sticker issued) → Inspector opens Finalize → Send OTP (verify in MailHog :8025) → enter OTP + client signs → download Annex 1 PDF and visually compare against `~/Downloads/SOFTWARE IMPLEMENTATION/Blue Sticker Inspection Service/Annex 1 - MS0053813 Aramco Inspection Report format.docx`.
Expected: PDF layout matches the official docx; all sheet fields populated; 3 signatures present; only the documented manual fields were typed (Org/RPO/CRM/Dept by coordinator; Area/Result/Deficiencies/Corrective/Receiver by inspector); all dates auto.

- [ ] **Step 6: Commit any cleanup**

```bash
git add -A
git commit -m "chore(blue-sticker): final regression pass"
```

---

## Self-Review Notes (addressed)

- **Spec §2 9-step flow** → Tasks 4 (state machine), 13/14 (handlers), 21/22 (UI). Step 2 OTP-at-creation intentionally dropped per spec; OTP on-demand = Tasks 14/22.
- **Spec §3 exact Annex 1 fields** → Task 3 aggregate (report-content properties = sheet 1:1); §3 auto/manual split enforced in Tasks 12/13 (auto snapshots) + 11/19/21 (manual entry points).
- **Spec §4 isolated OTP** → Task 7 (`IOtpService`/`EmailOtpService`), abstraction for future SMS.
- **Spec §5 finalize screen** → Task 22.
- **Spec §6 PDF reuse** → Tasks 16/17 reuse embedded `Annex1.docx` + `GotenbergClient` + QuestPDF fallback.
- **Spec §7 out of scope** → TPI `InspectionCertificate` never modified; verified by Task 25 Step 3.
- **Spec §8 testing** → Tasks 4, 7 (unit), 24 (integration), 25 (regression).
- **Spec §9 open dependencies** → Client email (Task 14 guards on missing `ContactEmail`), sticker validity (interim 1yr, flagged in Task 13 + asserted in Task 24), inspector telephone (Task 3 — inspector-entered field).
- **Placeholder scan:** the only `TODO` is the explicitly spec-deferred per-category validity (Task 13), surfaced in an assertion — not an unfilled plan step.
- **Type consistency:** `BlueStickerReport*` names, `IOtpService` surface, DTO records, and trigger strings are consistent across Tasks 3–24.
