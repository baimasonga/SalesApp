using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

/// <summary>
/// Validates the commercial license. Two modes:
///   • Trial: 30-day grace from first launch, full features, banner shown.
///   • Activated: HMAC-signed key carrying tier + expiry; validated locally
///     against the build-time signing secret. No phone-home required.
///
/// Key format (base64-URL):
///     SALONE-{base64(payload)}.{base64(hmac)}
///   where payload = "{licensedTo}|{expiresUnix}|{tier}|{features}"
/// </summary>
public class LicenseService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<LicenseService> _log;

    // Default signing secret — REPLACE in production by setting License:SigningSecret
    // in appsettings.Production.json or via env var. Keys signed with one secret
    // won't validate against a different one — that's the whole point.
    private const string DefaultSecret = "salone-sales-default-signing-secret-CHANGE-IN-PRODUCTION";

    public LicenseService(IDbContextFactory<SalesDbContext> factory, IConfiguration config, ILogger<LicenseService> log)
    { _factory = factory; _config = config; _log = log; }

    string Secret => _config["License:SigningSecret"] ?? DefaultSecret;

    public async Task<License> GetCurrentAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var lic = await db.Licenses.FirstOrDefaultAsync();
        if (lic == null)
        {
            // First run — create a 30-day trial automatically.
            lic = new License
            {
                LicensedTo = "Trial",
                Tier = LicenseTier.Trial,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Features = "core,sms,whatsapp,momo",
                MachineFingerprint = ComputeMachineFingerprint(),
            };
            db.Licenses.Add(lic);
            await db.SaveChangesAsync();
        }
        return lic;
    }

    public async Task<LicenseStatus> EvaluateAsync()
    {
        var lic = await GetCurrentAsync();
        var now = DateTime.UtcNow;
        var daysLeft = (int)Math.Ceiling((lic.ExpiresAt - now).TotalDays);
        if (lic.ExpiresAt < now)
            return new LicenseStatus(IsValid: false, IsTrial: lic.Tier == LicenseTier.Trial,
                LicensedTo: lic.LicensedTo, Tier: lic.Tier, ExpiresAt: lic.ExpiresAt, DaysRemaining: 0,
                Message: $"License expired on {lic.ExpiresAt:dd MMM yyyy}. Renew via /license.");
        return new LicenseStatus(IsValid: true, IsTrial: lic.Tier == LicenseTier.Trial,
            LicensedTo: lic.LicensedTo, Tier: lic.Tier, ExpiresAt: lic.ExpiresAt, DaysRemaining: daysLeft,
            Message: lic.Tier == LicenseTier.Trial
                ? $"Trial — {daysLeft} day(s) remaining."
                : $"Licensed to {lic.LicensedTo} ({lic.Tier}).");
    }

    /// <summary>Apply a paid key. Throws if signature/expiry invalid.</summary>
    public async Task<License> ActivateAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("License key is required.");
        var parsed = ParseAndVerifyKey(key) ?? throw new InvalidOperationException("Invalid or tampered license key.");

        if (parsed.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException($"License expired on {parsed.ExpiresAt:dd MMM yyyy}.");

        await using var db = await _factory.CreateDbContextAsync();
        var lic = await db.Licenses.FirstOrDefaultAsync() ?? new License();
        lic.Key = key;
        lic.LicensedTo = parsed.LicensedTo;
        lic.Tier = parsed.Tier;
        lic.Features = parsed.Features;
        lic.ExpiresAt = parsed.ExpiresAt;
        lic.IssuedAt = DateTime.UtcNow;
        lic.MachineFingerprint = ComputeMachineFingerprint();
        if (lic.Id == 0) db.Licenses.Add(lic); else db.Licenses.Update(lic);
        await db.SaveChangesAsync();
        _log.LogInformation("License activated for {Who} until {Until}", lic.LicensedTo, lic.ExpiresAt);
        return lic;
    }

    public record LicenseStatus(bool IsValid, bool IsTrial, string LicensedTo,
        LicenseTier Tier, DateTime ExpiresAt, int DaysRemaining, string Message);

    private record KeyPayload(string LicensedTo, DateTime ExpiresAt, LicenseTier Tier, string Features);

    KeyPayload? ParseAndVerifyKey(string key)
    {
        try
        {
            // Strip prefix
            var raw = key.Trim();
            if (raw.StartsWith("SALONE-", StringComparison.OrdinalIgnoreCase))
                raw = raw["SALONE-".Length..];

            var parts = raw.Split('.');
            if (parts.Length != 2) return null;

            var payloadBytes = FromBase64Url(parts[0]);
            var sigBytes = FromBase64Url(parts[1]);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
            var expected = hmac.ComputeHash(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(expected, sigBytes)) return null;

            var payloadStr = Encoding.UTF8.GetString(payloadBytes);
            var fields = payloadStr.Split('|');
            if (fields.Length < 4) return null;
            var licensedTo = fields[0];
            var expiresUnix = long.Parse(fields[1]);
            var tier = Enum.Parse<LicenseTier>(fields[2], ignoreCase: true);
            var features = fields[3];
            return new KeyPayload(licensedTo, DateTimeOffset.FromUnixTimeSeconds(expiresUnix).UtcDateTime, tier, features);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse license key");
            return null;
        }
    }

    /// <summary>Generate a key. Use this offline to mint keys you sell to customers.</summary>
    public string MintKey(string licensedTo, DateTime expiresUtc, LicenseTier tier, string features)
    {
        var unix = new DateTimeOffset(expiresUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var payload = $"{licensedTo}|{unix}|{tier}|{features}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = hmac.ComputeHash(payloadBytes);
        return $"SALONE-{ToBase64Url(payloadBytes)}.{ToBase64Url(sig)}";
    }

    string ComputeMachineFingerprint()
    {
        try
        {
            // Stable per machine: NIC MAC of the first up adapter + machine name
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                                  && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                  && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
            var mac = nic?.GetPhysicalAddress()?.ToString() ?? "no-mac";
            var raw = $"{Environment.MachineName}::{mac}";
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))[..16];
        }
        catch { return "unknown"; }
    }

    public string MachineFingerprint() => ComputeMachineFingerprint();

    static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
