using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

/// <summary>
/// A saved filter combo for a list page (e.g. /sales). Per-user named views
/// like "Bo store last 7 days, Mobile Money only" that users can quickly
/// re-apply from a dropdown.
/// </summary>
public class SavedView
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Page { get; set; } = string.Empty;  // e.g. "sales", "customers"

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(80)]
    public string OwnerUserName { get; set; } = "";   // who saved it

    /// <summary>JSON-encoded filter state, schema defined per page.</summary>
    [StringLength(2000)]
    public string FiltersJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
