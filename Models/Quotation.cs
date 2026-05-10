using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Quotation
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string QuoteNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public DateTime QuoteDate { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(30);

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    [StringLength(300)]
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }

    public int? ConvertedSaleId { get; set; }

    public List<QuotationItem> Items { get; set; } = new();
}

public class QuotationItem
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [StringLength(160)]
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public enum QuotationStatus { Draft, Sent, Accepted, Rejected, Expired, Converted }
