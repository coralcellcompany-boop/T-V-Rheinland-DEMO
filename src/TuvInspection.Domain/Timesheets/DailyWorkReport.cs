using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Timesheets;

public enum DwrStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// Daily Work Report (timesheet) per SRS §5.5.4. One row per inspector per day per job.
/// </summary>
public class DailyWorkReport : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public string DwrNo { get; private set; } = default!;          // DWR2026-NNNNN
    public Guid JobOrderId { get; private set; }
    public Guid ClientId { get; private set; }
    public string InspectorId { get; private set; } = default!;
    public DateOnly Date { get; private set; }
    public TimeOnly TimeFrom { get; private set; }
    public TimeOnly TimeTo { get; private set; }
    public string? Location { get; private set; }
    public int EquipmentInspected { get; private set; }
    public int OperatorsAssessed { get; private set; }
    public string? Notes { get; private set; }
    public DwrStatus Status { get; private set; } = DwrStatus.Draft;
    public string? RejectionReason { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private DailyWorkReport() { }

    public DailyWorkReport(Guid id, string dwrNo, Guid jobOrderId, Guid clientId,
        string inspectorId, DateOnly date, TimeOnly from, TimeOnly to) : base(id)
    {
        if (string.IsNullOrWhiteSpace(dwrNo)) throw new ArgumentException("DWR number required.", nameof(dwrNo));
        if (string.IsNullOrWhiteSpace(inspectorId)) throw new ArgumentException("Inspector required.", nameof(inspectorId));
        if (to < from) throw new ArgumentException("Time-to must be on or after time-from.");
        DwrNo = dwrNo.Trim();
        JobOrderId = jobOrderId;
        ClientId = clientId;
        InspectorId = inspectorId;
        Date = date;
        TimeFrom = from;
        TimeTo = to;
    }

    public TimeSpan HoursWorked => TimeTo - TimeFrom;

    public void UpdateDetails(string? location, int equipmentCount, int operatorsCount, string? notes)
    {
        EnsureMutable();
        Location = location?.Trim();
        EquipmentInspected = Math.Max(0, equipmentCount);
        OperatorsAssessed = Math.Max(0, operatorsCount);
        Notes = notes?.Trim();
    }

    public void Submit()
    {
        if (Status != DwrStatus.Draft && Status != DwrStatus.Rejected)
            throw new InvalidOperationException($"Cannot submit a DWR in state {Status}.");
        Status = DwrStatus.Submitted;
    }
    public void Approve()
    {
        if (Status != DwrStatus.Submitted) throw new InvalidOperationException(
            $"Only Submitted DWRs can be approved. Current state: {Status}.");
        Status = DwrStatus.Approved;
    }
    public void Reject(string reason)
    {
        if (Status != DwrStatus.Submitted) throw new InvalidOperationException(
            $"Only Submitted DWRs can be rejected. Current state: {Status}.");
        Status = DwrStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    private void EnsureMutable()
    {
        if (Status is not (DwrStatus.Draft or DwrStatus.Rejected))
            throw new InvalidOperationException(
                $"DWR cannot be edited in state {Status}.");
    }
}
