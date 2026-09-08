using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Tickets;

public interface ITicketCommentSignalsService
{
    Task<TicketCommentSignals?> GetAsync(int ticketId, CancellationToken cancellationToken = default);

    Task SaveAsync(
        int ticketId,
        string? ticketNumber,
        bool hasMessageBox,
        bool hasAttachment,
        CancellationToken cancellationToken = default,
        string? messageBoxDetail = null,
        string? attachmentDetail = null,
        bool replaceDetails = false);
}
