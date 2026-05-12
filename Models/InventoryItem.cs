using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class InventoryItem
{
    public int Id { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityOnHand { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; } = 5;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Concurrency token. Two cashiers selling the last unit at the same time
    /// will collide on save; the second SaveChanges throws DbUpdateConcurrencyException.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
