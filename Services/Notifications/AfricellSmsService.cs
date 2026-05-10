using System.Net.Http.Json;

namespace SalesApp.Services.Notifications;

/// <summary>
/// Africell Sierra Leone SMS gateway integration.
///
/// In production:
///   - Set SMS:Provider          = "Africell"
///   - Set SMS:ApiUrl            (Africell aggregator URL)
///   - Set SMS:ApiKey            (account API key)
///   - Set SMS:SenderId          (e.g. "SaloneSales")
///
/// In demo mode (no ApiKey configured) this falls through to LogOnlyNotificationService.
/// Africell aggregator API contracts vary by reseller — adapt the JSON shape below to match
/// your contract (Routesms, Bulksms, Hellio, etc.).
/// </summary>
public class AfricellSmsService : INotificationService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AfricellSmsService> _log;

    public AfricellSmsService(HttpClient http, IConfiguration config, ILogger<AfricellSmsService> log)
    { _http = http; _config = config; _log = log; }

    public async Task<NotificationResult> SendSmsAsync(string phone, string message, CancellationToken ct = default)
    {
        var apiUrl = _config["SMS:ApiUrl"];
        var apiKey = _config["SMS:ApiKey"];
        var sender = _config["SMS:SenderId"] ?? "SaloneSales";
        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
        {
            _log.LogInformation("SMS (demo) -> {Phone}: {Message}", phone, message);
            return new NotificationResult(true, "demo", $"demo-{Guid.NewGuid():N}", null);
        }

        try
        {
            var payload = new { to = NormalisePhone(phone), from = sender, message };
            var req = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            { Content = JsonContent.Create(payload) };
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Africell SMS failed {Status}: {Body}", resp.StatusCode, body);
                return new NotificationResult(false, "Africell", null, $"{resp.StatusCode}: {body}");
            }
            return new NotificationResult(true, "Africell", body, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Africell SMS error");
            return new NotificationResult(false, "Africell", null, ex.Message);
        }
    }

    public Task<NotificationResult> SendWhatsAppAsync(string phone, string message, string? documentUrl = null, CancellationToken ct = default)
    {
        // WhatsApp Business API integration goes here (Meta Cloud API or Twilio).
        // For the demo, generate a wa.me deep link the caller can use as a fallback.
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var encoded = Uri.EscapeDataString(message);
        var link = $"https://wa.me/{digits}?text={encoded}";
        _log.LogInformation("WhatsApp (demo deep-link): {Link}", link);
        return Task.FromResult(new NotificationResult(true, "wa.me", link, null));
    }

    private static string NormalisePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        // Convert local 0XX to international 232XX if it begins with a 0
        if (digits.StartsWith("0")) digits = "232" + digits[1..];
        return digits;
    }
}
