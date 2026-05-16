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
