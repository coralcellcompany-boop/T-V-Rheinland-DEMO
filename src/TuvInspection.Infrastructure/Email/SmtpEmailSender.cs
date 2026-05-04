using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TuvInspection.Application.Common.Email;

namespace TuvInspection.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;          // MailHog default
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = false;
    public string FromAddress { get; set; } = "no-reply@tuv-arabia.local";
    public string FromName { get; set; } = "TÜV Rheinland Arabia";
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options) => _options = options.Value;

    public async Task Send(EmailMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrEmpty(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);
        if (message.Attachments is not null)
        {
            foreach (var att in message.Attachments)
            {
                var stream = new MemoryStream(att.Content);
                mail.Attachments.Add(new Attachment(stream, att.FileName, att.ContentType));
            }
        }

        await client.SendMailAsync(mail, ct);
    }
}
