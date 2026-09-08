using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Tickets;

public interface ITicketUrgencyService
{
    Task<TicketUrgency?> GetAsync(int ticketId, CancellationToken cancellationToken = default);

    Task SaveAsync(
        int ticketId,
        string? ticketNumber,
        bool isUrgent,
        string? reason = null,
        double? confidence = null,
        string? classifierVersion = null,
        CancellationToken cancellationToken = default);
}
