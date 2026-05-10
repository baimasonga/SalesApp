namespace SalesApp.Services.Notifications;

public interface INotificationService
{
    /// <summary>Send an SMS to an SL phone number.</summary>
    Task<NotificationResult> SendSmsAsync(string phone, string message, CancellationToken ct = default);

    /// <summary>Send a WhatsApp message (text or document URL).</summary>
    Task<NotificationResult> SendWhatsAppAsync(string phone, string message, string? documentUrl = null, CancellationToken ct = default);
}

public record NotificationResult(bool Success, string Provider, string? MessageId, string? Error);
