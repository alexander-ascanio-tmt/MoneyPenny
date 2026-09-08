using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Tickets;

public class TicketProcessedCommentService : ITicketProcessedCommentService
{
    private readonly VectorDbContext _vectorDb;

    public TicketProcessedCommentService(VectorDbContext vectorDb)
    {
        _vectorDb = vectorDb;
    }

    public async Task<TicketProcessedComment?> GetAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _vectorDb.TicketProcessedComments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TicketId == ticketId, cancellationToken);
    }

    public async Task SaveAsync(
        int ticketId,
        int ticketActionId,
        string? ticketNumber,
        string processedText,
        int imagesDetected,
        int imagesExtracted,
        string? imageExtractionWarning,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processedText))
        {
            throw new ArgumentException("El texto procesado no puede estar vacío.", nameof(processedText));
        }

        var existing = await _vectorDb.TicketProcessedComments
            .FirstOrDefaultAsync(c => c.TicketId == ticketId, cancellationToken);

        var now = DateTime.UtcNow;
        var trimmedNumber = string.IsNullOrWhiteSpace(ticketNumber) ? null : ticketNumber.Trim();
        var trimmedText = processedText.Trim();
        var trimmedWarning = TrimOrNull(imageExtractionWarning, 1000);

        if (existing is null)
        {
            _vectorDb.TicketProcessedComments.Add(new TicketProcessedComment
            {
                TicketId = ticketId,
                TicketActionId = ticketActionId,
                TicketNumber = trimmedNumber,
                ProcessedText = trimmedText,
                ImagesDetected = imagesDetected,
                ImagesExtracted = imagesExtracted,
                ImageExtractionWarning = trimmedWarning,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.TicketActionId = ticketActionId;
            existing.TicketNumber = trimmedNumber ?? existing.TicketNumber;
            existing.ProcessedText = trimmedText;
            existing.ImagesDetected = imagesDetected;
            existing.ImagesExtracted = imagesExtracted;
            existing.ImageExtractionWarning = trimmedWarning;
            existing.UpdatedAt = now;
        }

        await _vectorDb.SaveChangesAsync(cancellationToken);
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
