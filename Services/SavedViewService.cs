using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class SavedViewService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly CurrentUserService _user;

    public SavedViewService(IDbContextFactory<SalesDbContext> factory, CurrentUserService user)
    { _factory = factory; _user = user; }

    public async Task<List<SavedView>> GetForPageAsync(string page)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.SavedViews
            .Where(v => v.Page == page && (v.OwnerUserName == _user.UserName || v.OwnerUserName == ""))
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<SavedView> SaveAsync(string page, string name, string filtersJson)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.SavedViews
            .FirstOrDefaultAsync(v => v.Page == page && v.Name == name && v.OwnerUserName == _user.UserName);
        if (existing != null)
        {
            existing.FiltersJson = filtersJson;
            existing.CreatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }
        var v = new SavedView
        {
            Page = page,
            Name = name,
            OwnerUserName = _user.UserName,
            FiltersJson = filtersJson,
            CreatedAt = DateTime.UtcNow
        };
        db.SavedViews.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var v = await db.SavedViews.FindAsync(id);
        if (v == null) return;
        if (v.OwnerUserName != _user.UserName && v.OwnerUserName != "")
            throw new InvalidOperationException("You can only delete your own saved views.");
        db.SavedViews.Remove(v);
        await db.SaveChangesAsync();
    }
}
