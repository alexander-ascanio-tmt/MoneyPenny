namespace MoneyPenny.Models.Rag;



public class TicketIntentAssignment

{

    public int Id { get; set; }

    public int TicketId { get; set; }

    public string? TicketNumber { get; set; }

    public int IntentId { get; set; }

    public double? Confidence { get; set; }

    public string? ClassifierVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }



    public TicketIntent Intent { get; set; } = null!;

}

