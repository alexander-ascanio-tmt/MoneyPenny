using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Tickets;

public class TicketCommentSignalsService : ITicketCommentSignalsService
{
    public const string ManualSource = "manual";

    private readonly VectorDbContext _vectorDb;

    public TicketCommentSignalsService(VectorDbContext vectorDb)
    {
        _vectorDb = vectorDb;
    }

    public async Task<TicketCommentSignals?> GetAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _vectorDb.TicketCommentSignals
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);
    }

    public async Task SaveAsync(
        int ticketId,
        string? ticketNumber,
        bool hasMessageBox,
        bool hasAttachment,
        CancellationToken cancellationToken = default,
        string? messageBoxDetail = null,
        string? attachmentDetail = null,
        bool replaceDetails = false)
    {
        var existing = await _vectorDb.TicketCommentSignals
            .FirstOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);

        var now = DateTime.UtcNow;
        var trimmedNumber = string.IsNullOrWhiteSpace(ticketNumber) ? null : ticketNumber.Trim();
        var trimmedMessageBoxDetail = TrimOrNull(messageBoxDetail, 4000);
        var trimmedAttachmentDetail = TrimOrNull(attachmentDetail, 500);

        if (existing is null)
        {
            _vectorDb.TicketCommentSignals.Add(new TicketCommentSignals
            {
                TicketId = ticketId,
                TicketNumber = trimmedNumber,
                HasMessageBox = hasMessageBox,
                HasAttachment = hasAttachment,
                MessageBoxDetail = hasMessageBox ? trimmedMessageBoxDetail : null,
                AttachmentDetail = hasAttachment ? trimmedAttachmentDetail : null,
                Source = ManualSource,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.TicketNumber = trimmedNumber ?? existing.TicketNumber;
            existing.HasMessageBox = hasMessageBox;
            existing.HasAttachment = hasAttachment;
            existing.Source = ManualSource;
            existing.UpdatedAt = now;

            if (replaceDetails)
            {
                existing.MessageBoxDetail = hasMessageBox ? trimmedMessageBoxDetail : null;
                existing.AttachmentDetail = hasAttachment ? trimmedAttachmentDetail : null;
            }
            else
            {
                if (!hasMessageBox)
                {
                    existing.MessageBoxDetail = null;
                }

                if (!hasAttachment)
                {
                    existing.AttachmentDetail = null;
                }
            }
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
