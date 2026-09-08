using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Tickets;

public interface ITicketProcessedCommentService
{
    Task<TicketProcessedComment?> GetAsync(int ticketId, CancellationToken cancellationToken = default);

    Task SaveAsync(
        int ticketId,
        int ticketActionId,
        string? ticketNumber,
        string processedText,
        int imagesDetected,
        int imagesExtracted,
        string? imageExtractionWarning,
        CancellationToken cancellationToken = default);
}
