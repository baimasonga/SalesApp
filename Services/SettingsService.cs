using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

/// <summary>
/// Reads/writes system settings. Caches values in memory; cache invalidates
/// whenever a write occurs through this service.
/// </summary>
public class SettingsService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private static readonly ConcurrentDictionary<string, string?> Cache = new();
    private static volatile bool _loaded;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(IDbContextFactory<SalesDbContext> factory) { _factory = factory; }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;
            await using var db = await _factory.CreateDbContextAsync();
            var rows = await db.Settings.ToListAsync();
            foreach (var s in rows) Cache[s.Key] = s.Value;
            _loaded = true;
        }
        finally { _lock.Release(); }
    }

    public async Task<string?> GetAsync(string key)
    {
        await EnsureLoadedAsync();
        return Cache.TryGetValue(key, out var v) ? v : null;
    }

    public async Task<T> GetAsync<T>(string key, T fallback)
    {
        var raw = await GetAsync(key);
        if (string.IsNullOrEmpty(raw)) return fallback;
        try
        {
            var t = typeof(T);
            if (t == typeof(string))  return (T)(object)raw;
            if (t == typeof(decimal)) return (T)(object)decimal.Parse(raw, CultureInfo.InvariantCulture);
            if (t == typeof(int))     return (T)(object)int.Parse(raw, CultureInfo.InvariantCulture);
            if (t == typeof(bool))    return (T)(object)bool.Parse(raw);
            return (T)Convert.ChangeType(raw, t, CultureInfo.InvariantCulture);
        }
        catch { return fallback; }
    }

    public async Task SetAsync(string key, string? value, string? description = null, string category = "General")
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Settings.FindAsync(key);
        if (existing == null)
        {
            db.Settings.Add(new Setting
            {
                Key = key, Value = value,
                Description = description, Category = category,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            if (!string.IsNullOrEmpty(description)) existing.Description = description;
            if (!string.IsNullOrEmpty(category)) existing.Category = category;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        Cache[key] = value;
    }

    public async Task<List<Setting>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Settings.OrderBy(s => s.Category).ThenBy(s => s.Key).ToListAsync();
    }

    public static void InvalidateCache() { Cache.Clear(); _loaded = false; }
}
