using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Certificates;

/// <summary>
/// Aggregate root for an inspection certificate. Holds the immutable identification block,
/// the mutable inspection data, and the trail of state transitions. The state machine
/// itself lives in <c>TuvInspection.Application.Certificates.CertificateStateMachine</c>;
/// this aggregate exposes guarded methods that the state machine calls.
/// </summary>
public class InspectionCertificate : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public string CertificateNo { get; private set; } = default!;     // IS-NNNNNN-26-NNNN
    public Guid? JobOrderId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public Guid ClientId { get; private set; }

    public DateOnly ReportIssueDate { get; private set; }
    public DateOnly InspectionDate { get; private set; }
    public DateOnly? NextDueDate { get; private set; }

    public string? Standards { get; private set; }
    public CertificateInspectionType InspectionType { get; private set; }
    public LoadTestKind LoadTest { get; private set; } = LoadTestKind.None;
    public InspectionResult Result { get; private set; } = InspectionResult.NotSet;
    public string? StickerNo { get; private set; }
    public Guid? StickerId { get; private set; }

    public CertificateState State { get; private set; } = CertificateState.Draft;

    /// <summary>JSON document — the dynamic checklist tied to the equipment type template.</summary>
    public string? ChecklistJson { get; private set; }
    public string? FindingsJson { get; private set; }
    public string? PhotosJson { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private readonly List<CertificateStateTransition> _transitions = new();
    public IReadOnlyCollection<CertificateStateTransition> Transitions => _transitions.AsReadOnly();

    private InspectionCertificate() { }

    public InspectionCertificate(
        Guid id,
        string certificateNo,
        Guid clientId,
        Guid equipmentId,
        Guid? jobOrderId,
        DateOnly inspectionDate,
        DateOnly reportIssueDate,
        CertificateInspectionType type) : base(id)
    {
        if (string.IsNullOrWhiteSpace(certificateNo))
            throw new ArgumentException("Certificate number required", nameof(certificateNo));

        CertificateNo = certificateNo.Trim();
        ClientId = clientId;
        EquipmentId = equipmentId;
        JobOrderId = jobOrderId;
        InspectionDate = inspectionDate;
        ReportIssueDate = reportIssueDate;
        InspectionType = type;
    }

    public void UpdateInspectionData(
        string? standards,
        LoadTestKind loadTest,
        InspectionResult result,
        DateOnly? nextDueDate,
        string? stickerNo)
    {
        EnsureMutable();
        Standards = standards?.Trim();
        LoadTest = loadTest;
        Result = result;
        NextDueDate = nextDueDate;
        StickerNo = stickerNo?.Trim();
    }

    public void UpdateChecklist(string? checklistJson) { EnsureMutable(); ChecklistJson = checklistJson; }
    public void UpdateFindings(string? findingsJson) { EnsureMutable(); FindingsJson = findingsJson; }
    public void UpdatePhotos(string? photosJson) { EnsureMutable(); PhotosJson = photosJson; }

    /// <summary>Called by the sticker auto-issue hook when this cert hits Approved.</summary>
    public void LinkSticker(Guid stickerId, string stickerNo)
    {
        StickerId = stickerId;
        StickerNo = stickerNo;
    }

    /// <summary>
    /// Internal use by <c>CertificateStateMachine</c>. Records the transition and updates state.
    /// </summary>
    public void ApplyTransition(
        CertificateState target,
        string actorUserId,
        string actorRole,
        string? comments,
        DateTime atUtc,
        Guid transitionId)
    {
        _transitions.Add(new CertificateStateTransition(
            transitionId, Id, State, target, actorUserId, actorRole, comments, atUtc));
        State = target;
    }

    private void EnsureMutable()
    {
        if (State != CertificateState.Draft && State != CertificateState.Rejected)
            throw new InvalidOperationException(
                $"Certificate cannot be edited in state {State}. Only Draft and Rejected are mutable.");
    }
}
