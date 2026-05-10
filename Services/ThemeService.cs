namespace SalesApp.Services;

public class ThemeService
{
    public string Theme { get; private set; } = "light"; // light | dark
    public event Action? Changed;

    public void Toggle() => Set(Theme == "dark" ? "light" : "dark");

    public void Set(string theme)
    {
        if (Theme == theme) return;
        Theme = theme;
        Changed?.Invoke();
    }
}
