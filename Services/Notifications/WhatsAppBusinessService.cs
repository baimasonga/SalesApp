using System.Net.Http.Json;
using System.Text.Json;

namespace SalesApp.Services.Notifications;

/// <summary>
/// Meta (Facebook) WhatsApp Business Cloud API integration.
/// https://developers.facebook.com/docs/whatsapp/cloud-api
///
/// Configuration (appsettings.json):
///   WhatsApp:AccessToken     -> permanent system-user token
///   WhatsApp:PhoneNumberId   -> Cloud API phone number ID
///   WhatsApp:ApiVersion      -> e.g. "v20.0" (defaults to v20.0)
///
/// When no token is configured the service falls back to a wa.me deep link so
/// the cashier can still hand-deliver the message from their phone.
/// </summary>
public class WhatsAppBusinessService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppBusinessService> _log;

    public WhatsAppBusinessService(HttpClient http, IConfiguration config, ILogger<WhatsAppBusinessService> log)
    { _http = http; _config = config; _log = log; }

    public async Task<NotificationResult> SendTextAsync(string phone, string message, CancellationToken ct = default)
    {
        var token = _config["WhatsApp:AccessToken"];
        var phoneNumberId = _config["WhatsApp:PhoneNumberId"];
        var apiVersion = _config["WhatsApp:ApiVersion"] ?? "v20.0";

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(phoneNumberId))
        {
            // Fallback: generate a wa.me deep link
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            var encoded = Uri.EscapeDataString(message);
            var link = $"https://wa.me/{digits}?text={encoded}";
            _log.LogInformation("WhatsApp (demo deep-link): {Link}", link);
            return new NotificationResult(true, "wa.me", link, null);
        }

        try
        {
            var url = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages";
            var payload = new
            {
                messaging_product = "whatsapp",
                to = NormalisePhone(phone),
                type = "text",
                text = new { body = message, preview_url = false }
            };
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = JsonContent.Create(payload) };
            req.Headers.Add("Authorization", $"Bearer {token}");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("WhatsApp send failed {Status}: {Body}", resp.StatusCode, body);
                return new NotificationResult(false, "WhatsApp", null, $"{resp.StatusCode}: {body}");
            }

            // Parse Meta response: { messages: [{ id: "..." }] }
            string? messageId = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array)
                {
                    var first = msgs.EnumerateArray().FirstOrDefault();
                    if (first.TryGetProperty("id", out var idProp)) messageId = idProp.GetString();
                }
            }
            catch { /* ignore parse errors, message was sent */ }
            return new NotificationResult(true, "WhatsApp", messageId ?? "ok", null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WhatsApp send error");
            return new NotificationResult(false, "WhatsApp", null, ex.Message);
        }
    }

    private static string NormalisePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0")) digits = "232" + digits[1..];
        return digits;
    }
}
