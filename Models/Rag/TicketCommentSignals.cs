namespace MoneyPenny.Models.Rag;

public class TicketCommentSignals
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public bool HasMessageBox { get; set; }
    public bool HasAttachment { get; set; }
    public string? MessageBoxDetail { get; set; }
    public string? AttachmentDetail { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
