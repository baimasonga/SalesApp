using Microsoft.EntityFrameworkCore;
using SalesApp.Components;
using SalesApp.Data;
using SalesApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database. Default = LocalDB / SQL Express. Set "Database:Provider" to "InMemory"
// in appsettings.json to run without a SQL Server instance.
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connStr = builder.Configuration.GetConnectionString("Default")
              ?? @"Server=.\SQLEXPRESS;Database=SalesAppDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContextFactory<SalesDbContext>(opt =>
{
    if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        opt.UseInMemoryDatabase("SalesAppDb");
    else
        opt.UseSqlServer(connStr);
});

builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<CrmService>();
builder.Services.AddScoped<AnalyticsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure DB exists & seed sample data
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SalesDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    try
    {
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database seeding skipped (provider: {Provider}). " +
            "If using SQL Express, ensure the instance is running, or switch Database:Provider to 'InMemory' in appsettings.json.", provider);
    }
}

app.Run();
