namespace MoneyPenny.Models.Rag;

public class TicketProcessedComment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int TicketActionId { get; set; }
    public string? TicketNumber { get; set; }
    public string ProcessedText { get; set; } = string.Empty;
    public int ImagesDetected { get; set; }
    public int ImagesExtracted { get; set; }
    public string? ImageExtractionWarning { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
