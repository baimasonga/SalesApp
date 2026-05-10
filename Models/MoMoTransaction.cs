using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

/// <summary>
/// Log of every Mobile Money webhook the system receives, valid or not.
/// Acts as an idempotency table — duplicate <see cref="TransactionId"/> values
/// are rejected without re-applying the payment.
/// </summary>
public class MoMoTransaction
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string TransactionId { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Operator { get; set; }  // OrangeMoney, Afrimoney, etc.

    [StringLength(30)]
    public string? MerchantReference { get; set; }  // Maps to Sale.InvoiceNumber

    [StringLength(30)]
    public string? PayerPhone { get; set; }

    public decimal Amount { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public MoMoStatus Status { get; set; } = MoMoStatus.Pending;

    [StringLength(300)]
    public string? StatusReason { get; set; }

    public int? AppliedToSaleId { get; set; }
}

public enum MoMoStatus { Pending, Applied, Duplicate, InvalidSignature, NoMatch, Failed }
