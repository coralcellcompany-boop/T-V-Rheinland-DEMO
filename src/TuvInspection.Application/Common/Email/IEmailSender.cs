namespace TuvInspection.Application.Common.Email;

/// <summary>
/// Sends transactional emails. In production this is the SMTP gateway; in dev it's MailHog.
/// Always invoked via the outbox processor — never directly from a request handler.
/// </summary>
public interface IEmailSender
{
    Task Send(EmailMessage message, CancellationToken ct);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null,
    IReadOnlyCollection<EmailAttachment>? Attachments = null);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
