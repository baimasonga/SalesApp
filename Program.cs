using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesApp.Components;
using SalesApp.Data;
using SalesApp.Data.Identity;
using SalesApp.Services;
using SalesApp.Services.Notifications;
using SalesApp.Models;
using Serilog;

// ----- Serilog: rolling daily file + console
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/salone-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Health checks: DB connectivity + always-ready liveness
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SalesApp.Data.SalesDbContext>("database", tags: new[] { "ready" });

// Rate limiting (.NET 8 built-in)
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // MoMo webhook: 60 requests / minute per IP (operator should not need more)
    opt.AddPolicy("momo-webhook", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    // SMS test endpoint: 10 / minute / IP
    opt.AddPolicy("sms-test", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ----- Database
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connStr = builder.Configuration.GetConnectionString("Default")
              ?? @"Server=.\SQLEXPRESS;Database=SalesAppDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddDbContextFactory<SalesDbContext>((sp, opt) =>
{
    if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        opt.UseInMemoryDatabase("SalesAppDb");
    else
        opt.UseSqlServer(connStr);
    // Auto-audit every SaveChanges via interceptor
    opt.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
}, ServiceLifetime.Scoped);

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
else
{
    // Demo mode: synthesize an AuthenticationState from CurrentUserService so
    // [Authorize] checks and role-based policies work uniformly. The "Sign in"
    // page sets the demo role; policies enforce based on that role.
    builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, DemoAuthStateProvider>();
    builder.Services.AddAuthentication("Demo")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DemoAuthHandler>("Demo", null);
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("ManagerOrAdmin", p => p.RequireRole("Manager", "Admin"))
        .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    builder.Services.AddCascadingAuthenticationState();
}

// ----- App services
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<LayoutState>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<NavDrawerState>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<TenantContext>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new TenantContext
    {
        Enabled = cfg.GetValue<bool>("Tenancy:Enabled"),
        TenantId = cfg.GetValue<int?>("Tenancy:DefaultTenantId") ?? 1,
        TenantName = cfg["Tenancy:DefaultTenantName"] ?? "Default"
    };
});
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<SavedViewService>();
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
builder.Services.AddHttpClient<WhatsAppBusinessService>();
builder.Services.AddScoped<MobileMoneyService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Authentication + authorization run in both modes.
// In Identity mode they enforce real users; in demo mode they use the
// DemoAuthStateProvider that synthesizes a ClaimsPrincipal from CurrentUserService.
app.UseAuthentication();
app.UseAuthorization();

// ----- Health & readiness probes (standardized ASP.NET Core HealthCheck endpoints)
// /health/live  → simple liveness, always returns 200 if the process is running
// /health/ready → readiness, returns 200 only if DB is reachable
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // no checks; just confirm the process is up
});
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        });
        await ctx.Response.WriteAsync(body);
    }
});

// Legacy ad-hoc probe (back-compat for anything calling /health/ready directly)
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

app.MapGet("/export/audit.csv", async (ExportService ex, string? action, string? entityType, string? user, DateTime? from, DateTime? to) =>
    Results.File(await ex.AuditCsvAsync(action, entityType, user, from, to), "text/csv", $"audit_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv"));

// ----- Mobile Money webhook (Orange Money / Afrimoney aggregator POSTs here)
// Verify HMAC signature in production using MoMo:WebhookSecret.
app.MapPost("/webhook/momo", async (HttpContext ctx, MobileMoneyService momo, IConfiguration cfg) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var raw = await reader.ReadToEndAsync();

    // Parse body so we have invoice/txn id available even when signature fails
    string invoiceNum = "", txnId = "";
    try
    {
        var parsed = JsonDocument.Parse(raw).RootElement;
        invoiceNum = parsed.TryGetProperty("merchantReference", out var mr) ? mr.GetString() ?? "" : "";
        txnId      = parsed.TryGetProperty("transactionId", out var ti) ? ti.GetString() ?? "" : "";
    }
    catch { /* logged below */ }

    // Optional signature check
    var secret = cfg["MoMo:WebhookSecret"];
    if (!string.IsNullOrEmpty(secret))
    {
        var sig = ctx.Request.Headers["X-Signature"].ToString();
        var expected = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(secret),
            System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        if (!string.Equals(sig, expected, StringComparison.OrdinalIgnoreCase))
        {
            await momo.LogInvalidSignatureAsync(invoiceNum, txnId, "HMAC mismatch");
            return Results.Unauthorized();
        }
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
        var status = await momo.ConfirmAsync(msg);
        return status switch
        {
            MoMoStatus.Applied    => Results.Ok(new      { confirmed = true,  status = "applied" }),
            MoMoStatus.Duplicate  => Results.Ok(new      { confirmed = false, status = "duplicate" }),
            MoMoStatus.NoMatch    => Results.NotFound(new{ confirmed = false, status = "no-match", error = "invoice not found" }),
            _                     => Results.BadRequest(new { confirmed = false, status = status.ToString() })
        };
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireRateLimiting("momo-webhook");

// Test SMS endpoint (dev only - use a query string)
app.MapPost("/api/test/sms", async (INotificationService sms, string phone, string message) =>
{
    var r = await sms.SendSmsAsync(phone, message);
    return Results.Json(r);
}).RequireRateLimiting("sms-test");

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

try
{
    Log.Information("Salone Sales starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Salone Sales terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

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
