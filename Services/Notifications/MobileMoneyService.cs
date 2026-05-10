using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services.Notifications;

/// <summary>
/// Mobile money (Orange Money / Afrimoney) integration.
///
/// Two flows are supported:
///   1) Manual: cashier types the txn ref into the sale (already wired in NewSale).
///   2) Webhook: the operator POSTs a confirmation to /webhook/momo with a payload
///      including the merchant reference (the invoice number) and operator txn id.
///      ConfirmAsync below matches the operator txn id back to the sale.
///
/// To enable, set:
///   - MoMo:WebhookSecret  (verify HMAC signature on incoming webhooks)
///   - MoMo:ApiUrl, MoMo:MerchantId, MoMo:ApiKey  (for outbound payment requests)
/// </summary>
public class MobileMoneyService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;
    private readonly ILogger<MobileMoneyService> _log;

    public MobileMoneyService(IDbContextFactory<SalesDbContext> factory, AuditService audit, ILogger<MobileMoneyService> log)
    { _factory = factory; _audit = audit; _log = log; }

    public record MoMoConfirmation(string InvoiceNumber, string OperatorTxnId, decimal Amount, string Operator, string PayerPhone);

    public async Task<bool> ConfirmAsync(MoMoConfirmation msg, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var sale = await db.Sales.FirstOrDefaultAsync(s => s.InvoiceNumber == msg.InvoiceNumber, ct);
        if (sale == null)
        {
            _log.LogWarning("MoMo confirmation for unknown invoice {Invoice}", msg.InvoiceNumber);
            return false;
        }
        if (Math.Abs(sale.Total - msg.Amount) > 0.01m)
        {
            _log.LogWarning("MoMo amount mismatch for {Invoice}: sale={SaleTotal} momo={MomoAmount}",
                msg.InvoiceNumber, sale.Total, msg.Amount);
        }
        sale.PaymentMethod = PaymentMethod.MobileMoney;
        sale.TransactionReference = msg.OperatorTxnId;
        sale.Notes = $"{sale.Notes} | Confirmed via {msg.Operator} from {msg.PayerPhone}".Trim('|', ' ');
        await db.SaveChangesAsync(ct);
        await _audit.LogAsync("MoMoConfirmed", "Sale", sale.Id, $"{msg.Operator} txn {msg.OperatorTxnId}");
        return true;
    }
}
