using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

/// <summary>
/// Every SMS the system attempts to send (success or failure) is recorded here
/// so admins can audit delivery, retry, or troubleshoot gateway issues.
/// </summary>
public class SmsLog
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [StringLength(40)]
    public string Provider { get; set; } = "Africell";

    public SmsStatus Status { get; set; } = SmsStatus.Pending;

    [StringLength(500)]
    public string? StatusDetail { get; set; }

    [StringLength(120)]
    public string? ProviderReference { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public enum SmsStatus { Pending, Sent, Failed, DemoMode }
