using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Tickets;

public class TicketUrgencyService : ITicketUrgencyService
{
    public const string ManualClassifierVersion = "manual";

    private readonly VectorDbContext _vectorDb;

    public TicketUrgencyService(VectorDbContext vectorDb)
    {
        _vectorDb = vectorDb;
    }

    public async Task<TicketUrgency?> GetAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _vectorDb.TicketUrgencies
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TicketId == ticketId, cancellationToken);
    }

    public async Task SaveAsync(
        int ticketId,
        string? ticketNumber,
        bool isUrgent,
        string? reason = null,
        double? confidence = null,
        string? classifierVersion = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _vectorDb.TicketUrgencies
            .FirstOrDefaultAsync(u => u.TicketId == ticketId, cancellationToken);

        var now = DateTime.UtcNow;
        var trimmedNumber = string.IsNullOrWhiteSpace(ticketNumber) ? null : ticketNumber.Trim();
        var trimmedReason = TrimOrNull(reason, 500);
        var version = string.IsNullOrWhiteSpace(classifierVersion)
            ? ManualClassifierVersion
            : classifierVersion.Trim();

        if (existing is null)
        {
            _vectorDb.TicketUrgencies.Add(new TicketUrgency
            {
                TicketId = ticketId,
                TicketNumber = trimmedNumber,
                IsUrgent = isUrgent,
                Reason = isUrgent ? trimmedReason : null,
                Confidence = confidence,
                ClassifierVersion = version,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.TicketNumber = trimmedNumber ?? existing.TicketNumber;
            existing.IsUrgent = isUrgent;
            existing.Reason = isUrgent ? trimmedReason : null;
            existing.Confidence = confidence;
            existing.ClassifierVersion = version;
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
