using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesApp.Components;
using SalesApp.Data;
using SalesApp.Data.Identity;
using SalesApp.Services;
using SalesApp.Services.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ----- Database
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connStr = builder.Configuration.GetConnectionString("Default")
              ?? @"Server=.\SQLEXPRESS;Database=SalesAppDb;Trusted_Connection=True;TrustServerCertificate=True;";

void ConfigureDb(DbContextOptionsBuilder opt)
{
    if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        opt.UseInMemoryDatabase("SalesAppDb");
    else
        opt.UseSqlServer(connStr);
}

builder.Services.AddDbContextFactory<SalesDbContext>(ConfigureDb);

// ----- Optional ASP.NET Core Identity (Auth:Enabled = "true" in appsettings)
var authEnabled = builder.Configuration.GetValue<bool>("Auth:Enabled");
if (authEnabled)
{
    builder.Services.AddDbContext<IdentityAppDbContext>(opt =>
    {
        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            opt.UseInMemoryDatabase("IdentityDb");
        else
            opt.UseSqlServer(connStr);
    });
    builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<IdentityAppDbContext>();
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("ManagerOrAdmin", p => p.RequireRole("Manager", "Admin"))
        .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
}

// ----- App services
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<LayoutState>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<NavDrawerState>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<CrmService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<StockTransferService>();
builder.Services.AddScoped<QuotationService>();
builder.Services.AddScoped<TargetService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<LayawayService>();

// Notifications (SMS + Mobile Money)
builder.Services.AddHttpClient<INotificationService, AfricellSmsService>();
builder.Services.AddScoped<MobileMoneyService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// ----- Health & readiness probes
app.MapGet("/health", () => Results.Json(new { status = "ok", time = DateTime.UtcNow }));
app.MapGet("/health/ready", async (IDbContextFactory<SalesDbContext> factory) =>
{
    try
    {
        await using var db = await factory.CreateDbContextAsync();
        var canConnect = await db.Database.CanConnectAsync();
        var stores = await db.Stores.CountAsync();
        return Results.Json(new
        {
            status = canConnect ? "ready" : "degraded",
            database = canConnect ? "connected" : "disconnected",
            seededStores = stores,
            time = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "error", error = ex.Message }, statusCode: 503);
    }
});

// ----- CSV exports
app.MapGet("/export/sales.csv", async (ExportService ex, DateTime? from, DateTime? to, int? storeId) =>
    Results.File(await ex.SalesCsvAsync(from, to, storeId), "text/csv", $"sales_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv"));

app.MapGet("/export/nra.csv", async (ExportService ex, DateTime from, DateTime to) =>
    Results.File(await ex.NraTaxCsvAsync(from, to), "text/csv", $"nra_tax_{from:yyyyMMdd}_{to:yyyyMMdd}.csv"));

app.MapGet("/export/inventory.csv", async (ExportService ex) =>
    Results.File(await ex.InventoryCsvAsync(), "text/csv", $"inventory_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv"));

// ----- Mobile Money webhook (Orange Money / Afrimoney aggregator POSTs here)
// Verify HMAC signature in production using MoMo:WebhookSecret.
app.MapPost("/webhook/momo", async (HttpContext ctx, MobileMoneyService momo, IConfiguration cfg) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var raw = await reader.ReadToEndAsync();

    // Optional signature check
    var secret = cfg["MoMo:WebhookSecret"];
    if (!string.IsNullOrEmpty(secret))
    {
        var sig = ctx.Request.Headers["X-Signature"].ToString();
        var expected = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(secret),
            System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        if (!string.Equals(sig, expected, StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();
    }

    try
    {
        var doc = JsonDocument.Parse(raw).RootElement;
        var msg = new MobileMoneyService.MoMoConfirmation(
            InvoiceNumber: doc.GetProperty("merchantReference").GetString() ?? "",
            OperatorTxnId: doc.GetProperty("transactionId").GetString() ?? "",
            Amount: doc.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m,
            Operator: doc.TryGetProperty("operator", out var o) ? o.GetString() ?? "MoMo" : "MoMo",
            PayerPhone: doc.TryGetProperty("payerPhone", out var p) ? p.GetString() ?? "" : ""
        );
        var ok = await momo.ConfirmAsync(msg);
        return ok ? Results.Ok(new { confirmed = true }) : Results.NotFound(new { error = "invoice not found" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Test SMS endpoint (dev only - use a query string)
app.MapPost("/api/test/sms", async (INotificationService sms, string phone, string message) =>
{
    var r = await sms.SendSmsAsync(phone, message);
    return Results.Json(r);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ----- DB init / seeding
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SalesDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    try { await DbSeeder.SeedAsync(db); }
    catch (Exception ex)
    {
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogWarning(ex, "Database seeding skipped (provider: {Provider})", provider);
    }

    if (authEnabled)
    {
        try
        {
            var idCtx = scope.ServiceProvider.GetRequiredService<IdentityAppDbContext>();
            await idCtx.Database.EnsureCreatedAsync();
            await IdentitySeeder.SeedAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Identity seeding skipped");
        }
    }
}

app.Run();

static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Manager", "Cashier" })
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));

        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userMgr.FindByEmailAsync("admin@demo.sl") == null)
        {
            var admin = new ApplicationUser { UserName = "admin@demo.sl", Email = "admin@demo.sl", FullName = "Admin User", EmailConfirmed = true };
            await userMgr.CreateAsync(admin, "Admin@1234");
            await userMgr.AddToRoleAsync(admin, "Admin");
        }
    }
}
