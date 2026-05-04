using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.JobRequests;

public enum JobRequestStatus
{
    New = 0,
    UnderReview = 1,
    Accepted = 2,
    Rejected = 3,
    Converted = 4
}

/// <summary>
/// Inbound queue entry from a client (or coordinator-keyed). Once accepted it becomes a Job Order.
/// </summary>
public class JobRequest : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public string RequestNo { get; private set; } = default!;       // JR2026-NNNN
    public Guid ClientId { get; private set; }
    public ServiceType Service { get; private set; }
    public DateOnly RequestedFrom { get; private set; }
    public DateOnly RequestedTo { get; private set; }
    public string? Site { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ScopeNotes { get; private set; }
    public string? PoReference { get; private set; }
    public JobRequestStatus Status { get; private set; } = JobRequestStatus.New;
    public Guid? ConvertedJobOrderId { get; private set; }
    public string? RejectionReason { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private JobRequest() { }

    public JobRequest(Guid id, string requestNo, Guid clientId, ServiceType service,
        DateOnly from, DateOnly to) : base(id)
    {
        if (string.IsNullOrWhiteSpace(requestNo)) throw new ArgumentException("Request number required.", nameof(requestNo));
        if (to < from) throw new ArgumentException("'To' must be on or after 'From'.");
        RequestNo = requestNo.Trim();
        ClientId = clientId;
        Service = service;
        RequestedFrom = from;
        RequestedTo = to;
    }

    public void UpdateContact(string? name, string? phone, string? email)
    {
        ContactName = name?.Trim(); ContactPhone = phone?.Trim(); ContactEmail = email?.Trim();
    }
    public void UpdateScope(string? site, string? notes, string? poRef)
    {
        Site = site?.Trim(); ScopeNotes = notes?.Trim(); PoReference = poRef?.Trim();
    }

    public void BeginReview()
    {
        if (Status != JobRequestStatus.New) throw new InvalidOperationException(
            $"Only New requests can enter review. Current state: {Status}.");
        Status = JobRequestStatus.UnderReview;
    }
    public void Accept()
    {
        if (Status is JobRequestStatus.Converted or JobRequestStatus.Rejected)
            throw new InvalidOperationException($"Cannot accept a request in state {Status}.");
        Status = JobRequestStatus.Accepted;
    }
    public void Reject(string reason)
    {
        if (Status is JobRequestStatus.Converted or JobRequestStatus.Rejected)
            throw new InvalidOperationException($"Cannot reject a request in state {Status}.");
        Status = JobRequestStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
    public void MarkConverted(Guid jobOrderId)
    {
        if (Status != JobRequestStatus.Accepted)
            throw new InvalidOperationException("Request must be Accepted before conversion.");
        Status = JobRequestStatus.Converted;
        ConvertedJobOrderId = jobOrderId;
    }
}
