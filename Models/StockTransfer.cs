using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class StockTransfer
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string TransferNumber { get; set; } = string.Empty;

    public int FromStoreId { get; set; }
    public Store? FromStore { get; set; }

    public int ToStoreId { get; set; }
    public Store? ToStore { get; set; }

    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    public StockTransferStatus Status { get; set; } = StockTransferStatus.Pending;

    [StringLength(80)]
    public string? RequestedBy { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public List<StockTransferItem> Items { get; set; } = new();
}

public class StockTransferItem
{
    public int Id { get; set; }
    public int StockTransferId { get; set; }
    public StockTransfer? StockTransfer { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [StringLength(160)]
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

public enum StockTransferStatus { Pending, Completed, Cancelled }
