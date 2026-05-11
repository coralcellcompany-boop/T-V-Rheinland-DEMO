using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Email;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Identity;

namespace TuvInspection.Infrastructure.Outbox;

/// <summary>
/// Outbox payload enqueued when an inspector submits a certificate. The handler fans the
/// notification out to every active TechReviewer so their inbox lights up without anyone
/// having to refresh the approval queue page.
/// </summary>
public sealed record CertificateSubmittedNotifyEmail(
    Guid CertificateId,
    string CertificateNo,
    Guid ClientId,
    string ClientName,
    string EquipmentIdNo,
    DateTime AtUtc);

public sealed class TechReviewerNotifyEmailHandler : IOutboxMessageHandler<CertificateSubmittedNotifyEmail>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly ILogger<TechReviewerNotifyEmailHandler> _log;

    public TechReviewerNotifyEmailHandler(UserManager<ApplicationUser> users, IEmailSender email,
        ILogger<TechReviewerNotifyEmailHandler> log)
    {
        _users = users;
        _email = email;
        _log = log;
    }

    public async Task Handle(CertificateSubmittedNotifyEmail payload, CancellationToken ct)
    {
        var reviewers = await _users.GetUsersInRoleAsync(Roles.TechReviewer);
        var recipients = reviewers
            .Where(r => r.IsActive && !string.IsNullOrWhiteSpace(r.Email))
            .ToList();

        if (recipients.Count == 0)
        {
            _log.LogWarning(
                "Cert {Cert} submitted but no active TechReviewer has an email — review queue alert skipped.",
                payload.CertificateNo);
            return;
        }

        var html = $@"
<p>A new inspection certificate is awaiting Tech Review.</p>
<ul>
  <li><strong>Certificate:</strong> {payload.CertificateNo}</li>
  <li><strong>Client:</strong> {payload.ClientName}</li>
  <li><strong>Equipment ID:</strong> {payload.EquipmentIdNo}</li>
  <li><strong>Submitted (UTC):</strong> {payload.AtUtc:yyyy-MM-dd HH:mm}</li>
</ul>
<p>Open the approval queue in the TÜV Rheinland Arabia Inspection portal to begin review.</p>";

        foreach (var r in recipients)
        {
            var greeting = r.FullName ?? r.UserName ?? "Reviewer";
            await _email.Send(new EmailMessage(
                To: r.Email!,
                Subject: $"Review pending: {payload.CertificateNo}",
                HtmlBody: $"<p>Hello {greeting},</p>" + html), ct);
        }

        _log.LogInformation("Notified {Count} TechReviewer(s) about submitted cert {Cert}.",
            recipients.Count, payload.CertificateNo);
    }
}
