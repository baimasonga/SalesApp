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
}
