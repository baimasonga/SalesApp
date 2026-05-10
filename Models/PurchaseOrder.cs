using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class PurchaseOrder
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string PoNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public DateTime? ReceivedDate { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [StringLength(300)]
    public string? Notes { get; set; }

    public decimal Total { get; set; }

    public List<PurchaseOrderItem> Items { get; set; } = new();
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [StringLength(160)]
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public enum PurchaseOrderStatus { Draft, Submitted, Received, Cancelled }
