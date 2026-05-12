using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SalesApp.Services;

/// <summary>
/// Demo-mode AuthenticationHandler that always succeeds, synthesizing a
/// ClaimsPrincipal from CurrentUserService. Used when Auth:Enabled = false
/// so that [Authorize] policies enforce role-based access using the in-memory
/// user — without requiring cookies, password DB, or Identity tables.
/// </summary>
public class DemoAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly CurrentUserService _user;

    public DemoAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        CurrentUserService user) : base(options, logger, encoder)
    {
        _user = user;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _user.UserName ?? "Demo User"),
            new(ClaimTypes.Role, _user.Role ?? "Cashier"),
        };
        // Admin implies Manager — emit both so policies that require Manager pass too.
        if (string.Equals(_user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            claims.Add(new Claim(ClaimTypes.Role, "Manager"));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
