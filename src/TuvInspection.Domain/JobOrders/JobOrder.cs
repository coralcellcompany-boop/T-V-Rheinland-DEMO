using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.JobOrders;

public enum JobOrderStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

/// <summary>
/// Lightweight Job Order for MVP — full Job Management module deferred. Used only to link
/// certificates to a scheduled work assignment for reporting/dashboard purposes.
/// </summary>
public class JobOrder : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public string JobOrderNo { get; private set; } = default!;     // JOD2026-NNNN
    public Guid ClientId { get; private set; }
    public ServiceType Service { get; private set; }
    public DateOnly DateFrom { get; private set; }
    public DateOnly DateTo { get; private set; }
    public string? Location { get; private set; }
    public JobOrderStatus Status { get; private set; } = JobOrderStatus.Open;

    private readonly List<string> _assignedInspectorIds = new();
    public IReadOnlyCollection<string> AssignedInspectorIds => _assignedInspectorIds.AsReadOnly();

    /// <summary>Storage keys of uploaded attachments (PDF / images) — see <c>IDocumentStore</c>.</summary>
    private readonly List<string> _attachmentKeys = new();
    public IReadOnlyCollection<string> AttachmentKeys => _attachmentKeys.AsReadOnly();

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private JobOrder() { }

    public JobOrder(
        Guid id,
        string jobOrderNo,
        Guid clientId,
        ServiceType service,
        DateOnly dateFrom,
        DateOnly dateTo) : base(id)
    {
        if (string.IsNullOrWhiteSpace(jobOrderNo))
            throw new ArgumentException("Job order number required", nameof(jobOrderNo));
        if (dateTo < dateFrom)
            throw new ArgumentException("DateTo must be on or after DateFrom");

        JobOrderNo = jobOrderNo.Trim();
        ClientId = clientId;
        Service = service;
        DateFrom = dateFrom;
        DateTo = dateTo;
    }

    public void AssignInspector(string userId)
    {
        if (!_assignedInspectorIds.Contains(userId))
            _assignedInspectorIds.Add(userId);
    }

    public void UnassignInspector(string userId) => _assignedInspectorIds.Remove(userId);

    public void AddAttachment(string key)
    {
        if (!string.IsNullOrWhiteSpace(key) && !_attachmentKeys.Contains(key))
            _attachmentKeys.Add(key.Trim());
    }

    public void RemoveAttachment(string key) => _attachmentKeys.Remove(key);

    public void SetAttachments(IEnumerable<string> keys)
    {
        _attachmentKeys.Clear();
        foreach (var k in keys) AddAttachment(k);
    }

    public void UpdateLocation(string? location) => Location = location?.Trim();
    public void Begin() => Status = JobOrderStatus.InProgress;
    public void Complete() => Status = JobOrderStatus.Completed;
    public void Cancel() => Status = JobOrderStatus.Cancelled;
}
