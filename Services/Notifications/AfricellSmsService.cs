using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services.Notifications;

/// <summary>
/// Africell Sierra Leone SMS gateway. Every send (success, demo, or failure) is
/// recorded in <see cref="Models.SmsLog"/> for audit.
///
/// Configuration (appsettings.json or Settings page):
///   SMS:Provider    -> "Africell"
///   SMS:ApiUrl      -> aggregator endpoint
///   SMS:ApiKey      -> bearer token
///   SMS:SenderId    -> sender ID, e.g. "SaloneSales"
///
/// Without ApiKey configured we run in "demo mode" — log to console + DB,
/// return success.
/// </summary>
public class AfricellSmsService : INotificationService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AfricellSmsService> _log;
    private readonly IDbContextFactory<SalesDbContext> _factory;

    public AfricellSmsService(HttpClient http, IConfiguration config, ILogger<AfricellSmsService> log,
                              IDbContextFactory<SalesDbContext> factory)
    { _http = http; _config = config; _log = log; _factory = factory; }

    public async Task<NotificationResult> SendSmsAsync(string phone, string message, CancellationToken ct = default)
    {
        var apiUrl = _config["SMS:ApiUrl"];
        var apiKey = _config["SMS:ApiKey"];
        var sender = _config["SMS:SenderId"] ?? "SaloneSales";

        var log = new SmsLog
        {
            Phone = NormalisePhone(phone),
            Message = Truncate(message, 500),
            Provider = "Africell",
            SentAt = DateTime.UtcNow
        };

        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
        {
            _log.LogInformation("SMS (demo) -> {Phone}: {Message}", phone, message);
            log.Status = SmsStatus.DemoMode;
            log.StatusDetail = "ApiKey not configured — message would be sent in production.";
            log.ProviderReference = $"demo-{Guid.NewGuid():N}";
            await PersistAsync(log);
            return new NotificationResult(true, "demo", log.ProviderReference, null);
        }

        try
        {
            var payload = new { to = log.Phone, from = sender, message };
            var req = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            { Content = JsonContent.Create(payload) };
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Africell SMS failed {Status}: {Body}", resp.StatusCode, body);
                log.Status = SmsStatus.Failed;
                log.StatusDetail = Truncate($"{resp.StatusCode}: {body}", 500);
                await PersistAsync(log);
                return new NotificationResult(false, "Africell", null, log.StatusDetail);
            }

            log.Status = SmsStatus.Sent;
            log.ProviderReference = Truncate(body, 120);
            await PersistAsync(log);
            return new NotificationResult(true, "Africell", log.ProviderReference, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Africell SMS error");
            log.Status = SmsStatus.Failed;
            log.StatusDetail = Truncate(ex.Message, 500);
            await PersistAsync(log);
            return new NotificationResult(false, "Africell", null, ex.Message);
        }
    }

    public Task<NotificationResult> SendWhatsAppAsync(string phone, string message, string? documentUrl = null, CancellationToken ct = default)
    {
        // wa.me fallback for the deep-link case. The full Business API path lives in WhatsAppBusinessService.
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var encoded = Uri.EscapeDataString(message);
        var link = $"https://wa.me/{digits}?text={encoded}";
        _log.LogInformation("WhatsApp (demo deep-link): {Link}", link);
        return Task.FromResult(new NotificationResult(true, "wa.me", link, null));
    }

    private async Task PersistAsync(SmsLog log)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            db.SmsLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not persist SmsLog");
        }
    }

    private static string NormalisePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0")) digits = "232" + digits[1..];
        return digits;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
