namespace SalesApp.Services;

/// <summary>
/// Lightweight current-user abstraction. The Salone Sales demo uses a
/// session-scoped username so audit logs and "Cashier" fields work without
/// pulling in full ASP.NET Core Identity. Swap with a real auth provider when
/// migrating to SQL Express + Identity in production.
/// </summary>
public class CurrentUserService
{
    public string UserName { get; private set; } = "Demo User";
    public string Role { get; private set; } = "Manager"; // Cashier, Manager, Admin

    public void SignIn(string userName, string role)
    {
        UserName = string.IsNullOrWhiteSpace(userName) ? "Demo User" : userName;
        Role = string.IsNullOrWhiteSpace(role) ? "Cashier" : role;
    }

    public bool IsManager => Role is "Manager" or "Admin";
    public bool IsAdmin => Role == "Admin";
}
