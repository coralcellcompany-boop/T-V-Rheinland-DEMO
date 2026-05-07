using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Email;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Infrastructure.Persistence;
using TuvInspection.Infrastructure.Stickers;

namespace TuvInspection.Infrastructure.Outbox;

/// <summary>
/// Notifies the inspector when their sticker request was approved or rejected. Lands in MailHog
/// in dev (http://localhost:8025).
/// </summary>
public sealed class StickerRequestDecidedEmailHandler : IOutboxMessageHandler<StickerRequestDecidedEmail>
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<StickerRequestDecidedEmailHandler> _log;

    public StickerRequestDecidedEmailHandler(AppDbContext db, IEmailSender email,
        ILogger<StickerRequestDecidedEmailHandler> log)
    {
        _db = db;
        _email = email;
        _log = log;
    }

    public async Task Handle(StickerRequestDecidedEmail payload, CancellationToken ct)
    {
        var inspector = await _db.Users.AsNoTracking()
            .Where(u => u.Id == payload.InspectorUserId)
            .Select(u => new { u.Email, u.FullName, u.UserName })
            .FirstOrDefaultAsync(ct);

        if (inspector?.Email is null)
        {
            _log.LogWarning(
                "Sticker request {Req} decided but inspector {InspectorId} has no email — skipping.",
                payload.RequestNo, payload.InspectorUserId);
            return;
        }

        var greeting = inspector.FullName ?? inspector.UserName ?? "Inspector";

        string subject;
        string html;
        if (payload.Approved)
        {
            subject = $"Sticker request {payload.RequestNo} approved";
            var allocLine = payload.AllocatedCount == payload.RequestedQuantity
                ? $"All <strong>{payload.AllocatedCount}</strong> sticker(s) have been assigned to you."
                : $"<strong>{payload.AllocatedCount}</strong> of <strong>{payload.RequestedQuantity}</strong> sticker(s) were assigned. The rest will follow once new stock is procured.";
            var comments = string.IsNullOrWhiteSpace(payload.Comments)
                ? string.Empty
                : $"<p><em>Reviewer comments:</em> {payload.Comments}</p>";

            html = $@"
<p>Hello {greeting},</p>
<p>Your sticker request <strong>{payload.RequestNo}</strong> has been approved.</p>
<p>{allocLine}</p>
{comments}
<p>You can view your stickers in the TÜV Rheinland Arabia Inspection portal.</p>
<p>Thank you,<br/>TÜV Rheinland Arabia LLC</p>";
        }
        else
        {
            subject = $"Sticker request {payload.RequestNo} rejected";
            var reason = string.IsNullOrWhiteSpace(payload.Comments)
                ? string.Empty
                : $"<p><em>Reason:</em> {payload.Comments}</p>";

            html = $@"
<p>Hello {greeting},</p>
<p>Your sticker request <strong>{payload.RequestNo}</strong> has been rejected.</p>
{reason}
<p>If you believe this was a mistake or need to revise the request, please contact your coordinator.</p>
<p>Thank you,<br/>TÜV Rheinland Arabia LLC</p>";
        }

        await _email.Send(new EmailMessage(
            To: inspector.Email,
            Subject: subject,
            HtmlBody: html), ct);

        _log.LogInformation("Sent sticker-request-decided email to {Email} for {Req} (approved={Approved}).",
            inspector.Email, payload.RequestNo, payload.Approved);
    }
}
