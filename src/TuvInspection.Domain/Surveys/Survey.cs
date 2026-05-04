using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Surveys;

public enum SurveyStatus
{
    Draft = 0,
    Submitted = 1,
    ConvertedToJobOrder = 2
}

/// <summary>
/// Pre-visit site survey per SRS §5.5.6. Lightweight — used to scope a future Job Order.
/// </summary>
public class Survey : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public string SurveyNo { get; private set; } = default!;       // SUR2026-NNNN
    public Guid ClientId { get; private set; }
    public DateOnly Date { get; private set; }
    public string? Site { get; private set; }
    public string? GpsLatLng { get; private set; }
    public int EstimatedEquipmentCount { get; private set; }
    public string? AccessNotes { get; private set; }
    public string? SafetyNotes { get; private set; }
    public string? Recommendation { get; private set; }
    public string? SurveyorUserId { get; private set; }
    public SurveyStatus Status { get; private set; } = SurveyStatus.Draft;
    public Guid? ConvertedJobOrderId { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private Survey() { }

    public Survey(Guid id, string surveyNo, Guid clientId, DateOnly date) : base(id)
    {
        if (string.IsNullOrWhiteSpace(surveyNo)) throw new ArgumentException("Survey number required.");
        SurveyNo = surveyNo.Trim();
        ClientId = clientId;
        Date = date;
    }

    public void UpdateDetails(string? site, string? gps, int estimated,
        string? access, string? safety, string? recommendation, string? surveyorUserId)
    {
        EnsureMutable();
        Site = site?.Trim();
        GpsLatLng = gps?.Trim();
        EstimatedEquipmentCount = Math.Max(0, estimated);
        AccessNotes = access?.Trim();
        SafetyNotes = safety?.Trim();
        Recommendation = recommendation?.Trim();
        SurveyorUserId = surveyorUserId;
    }

    public void Submit() { EnsureMutable(); Status = SurveyStatus.Submitted; }
    public void MarkConverted(Guid jobOrderId)
    {
        if (Status != SurveyStatus.Submitted) throw new InvalidOperationException(
            "Only Submitted surveys can be converted to a Job Order.");
        Status = SurveyStatus.ConvertedToJobOrder;
        ConvertedJobOrderId = jobOrderId;
    }

    private void EnsureMutable()
    {
        if (Status != SurveyStatus.Draft) throw new InvalidOperationException(
            $"Survey cannot be edited in state {Status}.");
    }
}
