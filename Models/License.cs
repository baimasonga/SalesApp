using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

/// <summary>
/// Commercial license record. A single row per install. Created with default
/// "trial" values on first run; updated when the user enters a paid key.
/// </summary>
public class License
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string LicensedTo { get; set; } = "Trial";

    [StringLength(500)]
    public string? Key { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    /// <summary>Comma-separated feature flags (e.g. "multi-tenant,whatsapp,sms").</summary>
    [StringLength(300)]
    public string Features { get; set; } = "core";

    /// <summary>Hash of the machine that owns this license (MAC + Machine name).</summary>
    [StringLength(120)]
    public string? MachineFingerprint { get; set; }

    public LicenseTier Tier { get; set; } = LicenseTier.Trial;
}

public enum LicenseTier
{
    Trial,       // 30-day evaluation
    Starter,     // single store
    Business,    // multi-store
    Enterprise   // multi-tenant + all add-ons
}
