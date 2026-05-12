using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Layaway
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string LayawayNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }

    public LayawayStatus Status { get; set; } = LayawayStatus.Active;

    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance => TotalAmount - AmountPaid;

    [StringLength(160)]
    public string? Description { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public List<LayawayPayment> Payments { get; set; } = new();
}

public class LayawayPayment
{
    public int Id { get; set; }
    public int LayawayId { get; set; }
    public Layaway? Layaway { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [StringLength(60)]
    public string? Reference { get; set; }

    [StringLength(80)]
    public string? ReceivedBy { get; set; }
}

public enum LayawayStatus { Active, Completed, Cancelled, Refunded }
