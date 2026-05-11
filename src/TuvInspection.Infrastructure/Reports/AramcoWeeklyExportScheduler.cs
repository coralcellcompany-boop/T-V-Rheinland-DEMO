using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Email;
using TuvInspection.Application.Common.Time;

namespace TuvInspection.Infrastructure.Reports;

/// <summary>
/// Once a week (Monday 08:00 UTC by default) generates the Aramco Contractor Cranes Tracking
/// xlsx for the prior Mon–Sun window and emails it to the configured Aramco focal address.
/// Coordinator no longer has to remember to push the report on Monday morning.
///
/// Settings in <c>appsettings.json</c>:
///   "Aramco": {
///     "WeeklyReportEmail": "aramco-focal@example.com",
///     "WeeklyReportDayOfWeek": "Monday",   // optional, defaults Monday
///     "WeeklyReportHourUtc": 8             // optional, defaults 08:00 UTC
///   }
///
/// Disable by leaving <c>WeeklyReportEmail</c> empty — the scheduler logs once and exits.
/// </summary>
public sealed class AramcoWeeklyExportScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<AramcoWeeklyExportScheduler> _log;

    private DateOnly _lastSentForWeek = DateOnly.MinValue;

    public AramcoWeeklyExportScheduler(IServiceScopeFactory scopes, IConfiguration config,
        ILogger<AramcoWeeklyExportScheduler> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var to = _config.GetValue<string>("Aramco:WeeklyReportEmail");
        if (string.IsNullOrWhiteSpace(to))
        {
            _log.LogInformation(
                "Aramco weekly export scheduler is idle — Aramco:WeeklyReportEmail is not configured.");
            return;
        }

        var targetDow = ParseDayOfWeek(_config.GetValue<string>("Aramco:WeeklyReportDayOfWeek"))
            ?? DayOfWeek.Monday;
        var targetHourUtc = Math.Clamp(_config.GetValue<int?>("Aramco:WeeklyReportHourUtc") ?? 8, 0, 23);

        _log.LogInformation(
            "Aramco weekly export scheduler started: send on {Day} {Hour}:00 UTC to {To}.",
            targetDow, targetHourUtc, to);

        // Hourly tick is granular enough — the window is "a day" not "a second".
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                var now = clock.UtcNow;
                var today = DateOnly.FromDateTime(now);

                if (now.DayOfWeek == targetDow && now.Hour >= targetHourUtc && _lastSentForWeek != today)
                {
                    await SendOne(scope.ServiceProvider, to!, today, stoppingToken);
                    _lastSentForWeek = today;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Aramco weekly export tick failed.");
            }

            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task SendOne(IServiceProvider sp, string to, DateOnly today, CancellationToken ct)
    {
        var exporter = sp.GetRequiredService<AramcoWeeklyExporter>();
        var emailSender = sp.GetRequiredService<IEmailSender>();

        // Report the week that JUST FINISHED, not the one we're inside. On Monday this is the
        // previous Mon–Sun. Pass yesterday as the cutoff and let the exporter resolve the window.
        var cutoff = today.AddDays(-1);
        var (bytes, fileName) = await exporter.GenerateSystem(cutoff, ct);

        var html = $@"
<p>Hello,</p>
<p>Attached is the Aramco Contractor Cranes Tracking weekly submission. The window ends on
<strong>{cutoff:dd MMM yyyy}</strong>.</p>
<p>Generated automatically by the TÜV Rheinland Arabia Inspection portal.</p>";

        await emailSender.Send(new EmailMessage(
            To: to,
            Subject: $"Aramco Contractor Cranes Tracking — week ending {cutoff:yyyy-MM-dd}",
            HtmlBody: html,
            Attachments: new[]
            {
                new EmailAttachment(fileName,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes),
            }), ct);

        _log.LogInformation("Sent Aramco weekly export to {To} (week ending {Cutoff}, file {File}).",
            to, cutoff, fileName);
    }

    private static DayOfWeek? ParseDayOfWeek(string? raw) =>
        Enum.TryParse<DayOfWeek>(raw, ignoreCase: true, out var d) ? d : null;
}
