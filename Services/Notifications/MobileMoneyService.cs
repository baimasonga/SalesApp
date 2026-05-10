using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services.Notifications;

/// <summary>
/// Mobile money (Orange Money / Afrimoney) integration with idempotency.
///
/// Every incoming webhook is recorded in <see cref="MoMoTransaction"/>:
///   - duplicate operator txn IDs return Duplicate status (no re-apply)
///   - unknown invoice numbers return NoMatch
///   - successful matches apply payment + audit + return Applied
///
/// Configuration:
///   MoMo:WebhookSecret  -> HMAC verification (in Program.cs minimal-API endpoint)
///   MoMo:ApiUrl, MoMo:MerchantId, MoMo:ApiKey  -> outbound (future use)
/// </summary>
public class MobileMoneyService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;
    private readonly ILogger<MobileMoneyService> _log;

    public MobileMoneyService(IDbContextFactory<SalesDbContext> factory, AuditService audit, ILogger<MobileMoneyService> log)
    { _factory = factory; _audit = audit; _log = log; }

    public record MoMoConfirmation(string InvoiceNumber, string OperatorTxnId, decimal Amount, string Operator, string PayerPhone);

    public async Task<MoMoStatus> ConfirmAsync(MoMoConfirmation msg, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        // Idempotency check first
        var existing = await db.MoMoTransactions.FirstOrDefaultAsync(m => m.TransactionId == msg.OperatorTxnId, ct);
        if (existing != null)
        {
            _log.LogInformation("MoMo duplicate txn {Id} — already {Status}", msg.OperatorTxnId, existing.Status);
            return MoMoStatus.Duplicate;
        }

        var log = new MoMoTransaction
        {
            TransactionId = msg.OperatorTxnId,
            Operator = msg.Operator,
            MerchantReference = msg.InvoiceNumber,
            PayerPhone = msg.PayerPhone,
            Amount = msg.Amount,
            ReceivedAt = DateTime.UtcNow,
        };

        var sale = await db.Sales.FirstOrDefaultAsync(s => s.InvoiceNumber == msg.InvoiceNumber, ct);
        if (sale == null)
        {
            _log.LogWarning("MoMo confirmation for unknown invoice {Invoice}", msg.InvoiceNumber);
            log.Status = MoMoStatus.NoMatch;
            log.StatusReason = $"No sale found with InvoiceNumber={msg.InvoiceNumber}";
            db.MoMoTransactions.Add(log);
            await db.SaveChangesAsync(ct);
            return MoMoStatus.NoMatch;
        }

        if (Math.Abs(sale.Total - msg.Amount) > 0.01m)
            log.StatusReason = $"Amount mismatch: sale={sale.Total:N2}, paid={msg.Amount:N2}";

        sale.PaymentMethod = PaymentMethod.MobileMoney;
        sale.TransactionReference = msg.OperatorTxnId;
        sale.Notes = $"{sale.Notes} | Confirmed via {msg.Operator} from {msg.PayerPhone}".Trim('|', ' ');

        log.Status = MoMoStatus.Applied;
        log.AppliedToSaleId = sale.Id;
        db.MoMoTransactions.Add(log);

        await db.SaveChangesAsync(ct);
        await _audit.LogAsync("MoMoConfirmed", "Sale", sale.Id, $"{msg.Operator} txn {msg.OperatorTxnId}");
        return MoMoStatus.Applied;
    }

    /// <summary>Records a webhook that failed HMAC verification so admins can spot attacks.</summary>
    public async Task LogInvalidSignatureAsync(string invoice, string txnId, string reason)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            db.MoMoTransactions.Add(new MoMoTransaction
            {
                TransactionId = string.IsNullOrEmpty(txnId) ? $"unsigned-{Guid.NewGuid():N}" : txnId,
                MerchantReference = invoice,
                Status = MoMoStatus.InvalidSignature,
                StatusReason = reason,
                ReceivedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _log.LogError(ex, "Failed to log invalid signature MoMo webhook"); }
    }

    public async Task<List<MoMoTransaction>> GetRecentAsync(int take = 50)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MoMoTransactions.OrderByDescending(m => m.ReceivedAt).Take(take).ToListAsync();
    }
}
