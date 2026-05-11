using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Email;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Identity;

namespace TuvInspection.Infrastructure.Outbox;

/// <summary>
/// Outbox payload enqueued by the certificate auto-issue path when Unallocated sticker stock
/// drops to or below the configured low-stock threshold. The handler emails every Manager so
/// fresh stickers can be procured before the next approval fails.
/// </summary>
public sealed record StickerLowStockAlertEmail(
    int UnallocatedCount,
    int Threshold,
    string TriggerCertificateNo,
    DateTime AtUtc);

public sealed class LowStockAlertEmailHandler : IOutboxMessageHandler<StickerLowStockAlertEmail>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<LowStockAlertEmailHandler> _log;

    public LowStockAlertEmailHandler(UserManager<ApplicationUser> users, IEmailSender email,
        IConfiguration config, ILogger<LowStockAlertEmailHandler> log)
    {
        _users = users;
        _email = email;
        _config = config;
        _log = log;
    }

    public async Task Handle(StickerLowStockAlertEmail payload, CancellationToken ct)
    {
        // Recipients = every active Manager. Configurable extra address is appended for
        // contracts where a procurement officer outside the role tree also wants the alert.
        var managers = await _users.GetUsersInRoleAsync(Roles.Manager);
        var emails = managers
            .Where(m => m.IsActive && !string.IsNullOrWhiteSpace(m.Email))
            .Select(m => m.Email!)
            .ToList();

        var extra = _config.GetValue<string>("Stickers:LowStockAlertEmail");
        if (!string.IsNullOrWhiteSpace(extra)) emails.Add(extra);

        if (emails.Count == 0)
        {
            _log.LogWarning("Sticker low-stock alert raised but no Manager has an email configured.");
            return;
        }

        var html = $@"
<p>Blue Sticker stock is running low.</p>
<ul>
  <li><strong>Unallocated:</strong> {payload.UnallocatedCount}</li>
  <li><strong>Threshold:</strong> {payload.Threshold}</li>
  <li><strong>Triggered by approval of:</strong> {payload.TriggerCertificateNo}</li>
  <li><strong>At (UTC):</strong> {payload.AtUtc:yyyy-MM-dd HH:mm}</li>
</ul>
<p>Procure a fresh batch from the Stickers → Stock page before the next Blue Sticker certificate is approved.</p>
<p>TÜV Rheinland Arabia LLC</p>";

        foreach (var to in emails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await _email.Send(new EmailMessage(
                To: to,
                Subject: $"Blue Sticker stock low — {payload.UnallocatedCount} remaining",
                HtmlBody: html), ct);
        }

        _log.LogInformation("Sent low-stock alert to {Count} recipient(s) (stock={Stock}, threshold={Threshold}).",
            emails.Count, payload.UnallocatedCount, payload.Threshold);
    }
}
