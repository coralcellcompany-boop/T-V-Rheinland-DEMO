namespace TuvInspection.Domain.Certificates;

/// <summary>
/// Lifecycle states for an InspectionCertificate per SRS §5.3.5.
/// State transitions are managed via Stateless in the Application layer.
/// </summary>
public enum CertificateState
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    AwaitingApproval = 3,
    Approved = 4,
    ClientSent = 5,
    ClientAccepted = 6,
    ClientRejected = 7,
    Rejected = 8,
    Voided = 9,
    Expired = 10,
    Archived = 11
}

public enum CertificateInspectionType
{
    PeriodicInspection = 0,    // P.I.
    ReInspection = 1,          // Re.I.
    InitialInspection = 2      // I.I.
}

public enum LoadTestKind
{
    None = 0,
    Mechanical = 1,    // M
    Witnessed = 2,     // W
    Performed = 3      // P
}

public enum InspectionResult
{
    NotSet = 0,
    Pass = 1,
    Fail = 2,
    FailWithObservations = 3
}
