using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [StringLength(80)]
    public string UserName { get; set; } = "System";

    [StringLength(40)]
    public string Action { get; set; } = string.Empty; // Created, Updated, Deleted, Cancelled, Refunded

    [StringLength(40)]
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    [StringLength(500)]
    public string? Details { get; set; }
}
