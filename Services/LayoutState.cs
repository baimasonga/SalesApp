namespace SalesApp.Services;

/// <summary>
/// Lightweight state shared between pages and the chrome (topbar/sidebar) so
/// pages can set the topbar title/subtitle without duplicating markup.
/// </summary>
public record Crumb(string Label, string? Href = null);

public class LayoutState
{
    public string Title { get; private set; } = "Dashboard";
    public string? Subtitle { get; private set; }
    public IReadOnlyList<Crumb> Breadcrumbs { get; private set; } = Array.Empty<Crumb>();

    public event Action? Changed;

    public void Set(string title, string? subtitle = null, params Crumb[] breadcrumbs)
    {
        if (Title == title && Subtitle == subtitle && Breadcrumbs.SequenceEqual(breadcrumbs)) return;
        Title = title;
        Subtitle = subtitle;
        Breadcrumbs = breadcrumbs;
        Changed?.Invoke();
    }
}
