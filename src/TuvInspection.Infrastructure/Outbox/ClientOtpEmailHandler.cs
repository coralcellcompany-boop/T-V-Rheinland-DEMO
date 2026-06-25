using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Email;
using TuvInspection.Application.Common.Outbox;

namespace TuvInspection.Infrastructure.Outbox;

/// <summary>
/// Outbox payload enqueued when the Blue Sticker OTP service sends a one-time code to the
/// client for signing the inspection report. Consumed by the outbox processor background service.
/// </summary>
public sealed record ClientOtpEmail(
    Guid ReportId,
    string ToEmail,
    string Code,
    DateTime ExpiresAtUtc,
    DateTime AtUtc);

public sealed class ClientOtpEmailHandler : IOutboxMessageHandler<ClientOtpEmail>
{
    private readonly IEmailSender _email;
    private readonly ILogger<ClientOtpEmailHandler> _log;

    public ClientOtpEmailHandler(IEmailSender email, ILogger<ClientOtpEmailHandler> log)
    {
        _email = email;
        _log = log;
    }

    public async Task Handle(ClientOtpEmail payload, CancellationToken ct)
    {
        var minutes = (int)Math.Round((payload.ExpiresAtUtc - payload.AtUtc).TotalMinutes);
        var html = $@"
<p>Your one-time code to sign the Blue Sticker inspection report is: <strong>{payload.Code}</strong>.</p>
<p>It expires in {minutes} minutes (at {payload.ExpiresAtUtc:HH:mm} UTC). If you did not request this, ignore this email.</p>
<p>TÜV Rheinland Arabia LLC</p>";

        await _email.Send(new EmailMessage(
            To: payload.ToEmail,
            Subject: "TÜV Rheinland Arabia — Inspection signature code",
            HtmlBody: html), ct);

        _log.LogInformation("Sent OTP email to {Email} for report {ReportId}.",
            payload.ToEmail, payload.ReportId);
    }
}
