using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SalesApp.Services;

/// <summary>
/// Demo-mode auth state provider used when ASP.NET Core Identity is disabled
/// (<c>Auth:Enabled = false</c> in appsettings). Synthesizes a ClaimsPrincipal
/// from the in-memory <see cref="CurrentUserService"/> so [Authorize] checks
/// behave consistently — strict in production, permissive (but role-aware) in demo.
/// </summary>
public class DemoAuthStateProvider : AuthenticationStateProvider
{
    private readonly CurrentUserService _user;

    public DemoAuthStateProvider(CurrentUserService user)
    {
        _user = user;
        _user.Changed += () => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _user.UserName ?? "Demo User"),
            new(ClaimTypes.Role, _user.Role ?? "Cashier"),
        };
        // Manager and Admin are super-sets, common in this app — emit Manager role
        // for Admin so policies that require "Manager,Admin" work for admins too.
        if (string.Equals(_user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            claims.Add(new Claim(ClaimTypes.Role, "Manager"));

        var identity = new ClaimsIdentity(claims, authenticationType: "Demo");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }
}
