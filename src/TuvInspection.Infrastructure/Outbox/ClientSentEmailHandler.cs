using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Email;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Infrastructure.Certificates;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Outbox;

/// <summary>
/// Handles ClientSentCertificateEmail outbox payloads — formats and sends an email to the
/// client contact. In dev this lands in MailHog at http://localhost:8025.
/// </summary>
public sealed class ClientSentEmailHandler : IOutboxMessageHandler<ClientSentCertificateEmail>
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<ClientSentEmailHandler> _log;

    public ClientSentEmailHandler(AppDbContext db, IEmailSender email, ILogger<ClientSentEmailHandler> log)
    {
        _db = db;
        _email = email;
        _log = log;
    }

    public async Task Handle(ClientSentCertificateEmail payload, CancellationToken ct)
    {
        var client = await _db.Clients.IgnoreQueryFilters()
            .Where(c => c.Id == payload.ClientId)
            .Select(c => new { c.Name, c.ContactName, c.ContactEmail })
            .FirstOrDefaultAsync(ct);

        if (client?.ContactEmail is null)
        {
            _log.LogWarning(
                "Cert {Cert} sent to client {ClientId} but client has no contact email — skipping.",
                payload.CertificateNo, payload.ClientId);
            return;
        }

        var html = $@"
<p>Hello {client.ContactName ?? client.Name},</p>
<p>Inspection certificate <strong>{payload.CertificateNo}</strong> has been issued for your equipment
and is ready for your review.</p>
<p>Please log in to the TÜV Rheinland Arabia Inspection portal to accept or raise an issue.</p>
<p>Thank you,<br/>TÜV Rheinland Arabia LLC</p>";

        await _email.Send(new EmailMessage(
            To: client.ContactEmail,
            Subject: $"Inspection certificate {payload.CertificateNo} ready for your review",
            HtmlBody: html), ct);

        _log.LogInformation("Sent client-acceptance email to {Email} for {Cert}.",
            client.ContactEmail, payload.CertificateNo);
    }
}
