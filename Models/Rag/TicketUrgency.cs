namespace MoneyPenny.Models.Rag;

public class TicketUrgency
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public bool IsUrgent { get; set; }
    public string? Reason { get; set; }
    public double? Confidence { get; set; }
    public string? ClassifierVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
