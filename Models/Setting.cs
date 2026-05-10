using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

/// <summary>
/// Key/value store for runtime configuration. Lets admins change tax rates,
/// loyalty rules, business identity, etc. without recompiling.
/// </summary>
public class Setting
{
    [Key, StringLength(80)]
    public string Key { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Value { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    [StringLength(60)]
    public string Category { get; set; } = "General";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Centralized list of well-known setting keys (and defaults) used in business logic.</summary>
public static class SettingKeys
{
    // Business identity
    public const string BusinessName     = "Business.Name";
    public const string BusinessTin      = "Business.NraTin";
    public const string BusinessAddress  = "Business.Address";
    public const string BusinessPhone    = "Business.Phone";
    public const string BusinessEmail    = "Business.Email";
    public const string DefaultCurrency  = "Business.Currency";

    // Tax & policies
    public const string GstRate              = "Tax.GstRate";              // decimal, e.g. 0.15
    public const string DefaultDiscountCap   = "Sales.DefaultDiscountCap"; // decimal
    public const string RefundWindowDays     = "Sales.RefundWindowDays";   // int
    public const string LowStockThreshold    = "Inventory.DefaultReorderLevel"; // int

    // Loyalty
    public const string LoyaltyPerCurrency = "Loyalty.PointsPerNLe"; // decimal: how much spend earns 1 point

    // Receipt
    public const string ReceiptHeader  = "Receipt.HeaderText";
    public const string ReceiptFooter  = "Receipt.FooterText";

    // Audit
    public const string AuditRetentionDays = "Audit.RetentionDays"; // int

    // Integrations — runtime overrides for appsettings.json
    public const string SmsApiUrl     = "SMS.ApiUrl";
    public const string SmsApiKey     = "SMS.ApiKey";
    public const string SmsSenderId   = "SMS.SenderId";
    public const string MoMoWebhookSecret = "MoMo.WebhookSecret";
    public const string WhatsAppAccessToken  = "WhatsApp.AccessToken";
    public const string WhatsAppPhoneNumberId = "WhatsApp.PhoneNumberId";
}
