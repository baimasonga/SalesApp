using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Sale
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public DateTime SaleDate { get; set; } = DateTime.UtcNow;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    [StringLength(80)]
    public string? CashierName { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    public List<SaleItem> Items { get; set; } = new();
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [StringLength(160)]
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public enum PaymentMethod { Cash, MobileMoney, BankTransfer, Card, Credit }
public enum SaleStatus { Pending, Completed, Cancelled, Refunded }
