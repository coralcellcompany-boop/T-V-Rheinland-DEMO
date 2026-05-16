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
