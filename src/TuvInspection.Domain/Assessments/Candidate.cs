using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Assessments;

/// <summary>
/// Operator/candidate registry per SRS §5.4.2. One person → many assessments → 1+ cards.
/// </summary>
public class Candidate : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public Guid ClientId { get; private set; }
    public string FullName { get; private set; } = default!;
    public string IdentificationNumber { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? EmployeeNo { get; private set; }
    public string? Nationality { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? PhotoKey { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private Candidate() { }

    public Candidate(Guid id, Guid clientId, string fullName, string idNumber) : base(id)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Full name required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(idNumber)) throw new ArgumentException("Identification number required.", nameof(idNumber));
        ClientId = clientId;
        FullName = fullName.Trim();
        IdentificationNumber = idNumber.Trim();
    }

    public void UpdateProfile(string fullName, string idNumber, string? phone, string? email,
        string? employeeNo, string? nationality, DateOnly? dob)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Full name required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(idNumber)) throw new ArgumentException("Identification number required.", nameof(idNumber));
        FullName = fullName.Trim();
        IdentificationNumber = idNumber.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();
        EmployeeNo = employeeNo?.Trim();
        Nationality = nationality?.Trim();
        DateOfBirth = dob;
    }

    public void SetPhoto(string? key) => PhotoKey = key;
    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
