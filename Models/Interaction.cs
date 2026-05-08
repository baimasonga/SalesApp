using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Interaction
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public InteractionType Type { get; set; } = InteractionType.Note;

    [Required, StringLength(160)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Details { get; set; }

    public DateTime When { get; set; } = DateTime.UtcNow;

    public DateTime? FollowUpDate { get; set; }

    [StringLength(80)]
    public string? Owner { get; set; }
}

public enum InteractionType { Call, Email, Meeting, Visit, WhatsApp, Note, Complaint, Quote }
