using Microsoft.EntityFrameworkCore;

using MoneyPenny.Data;

using MoneyPenny.Models.Rag;

using MoneyPenny.ViewModels.Tickets;



namespace MoneyPenny.Services.Tickets;



public class TicketIntentService : ITicketIntentService

{

    public const string ManualClassifierVersion = "manual";



    private readonly VectorDbContext _vectorDb;



    public TicketIntentService(VectorDbContext vectorDb)

    {

        _vectorDb = vectorDb;

    }



    public async Task<IReadOnlyList<TicketIntentOptionViewModel>> GetActiveIntentsAsync(

        CancellationToken cancellationToken = default)

    {

        return await _vectorDb.TicketIntents

            .AsNoTracking()

            .Where(i => i.IsActive)

            .OrderBy(i => i.SortOrder)

            .ThenBy(i => i.Name)

            .Select(i => new TicketIntentOptionViewModel

            {

                Id = i.Id,

                Code = i.Code,

                Name = i.Name

            })

            .ToListAsync(cancellationToken);

    }



    public async Task<int?> GetAssignedIntentIdAsync(

        int ticketId,

        CancellationToken cancellationToken = default)

    {

        return await _vectorDb.TicketIntentAssignments

            .AsNoTracking()

            .Where(a => a.TicketId == ticketId)

            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)

            .Select(a => (int?)a.IntentId)

            .FirstOrDefaultAsync(cancellationToken);

    }



    public async Task AssignAsync(
        int ticketId,
        string? ticketNumber,
        int? intentId,
        double? confidence = null,
        string? classifierVersion = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _vectorDb.TicketIntentAssignments
            .Where(a => a.TicketId == ticketId)
            .ToListAsync(cancellationToken);

        if (intentId is null or <= 0)
        {
            if (existing.Count > 0)
            {
                _vectorDb.TicketIntentAssignments.RemoveRange(existing);
                await _vectorDb.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var intentExists = await _vectorDb.TicketIntents
            .AnyAsync(i => i.Id == intentId && i.IsActive, cancellationToken);
        if (!intentExists)
        {
            throw new InvalidOperationException("La intención indicada no existe o está inactiva.");
        }

        var keep = existing.FirstOrDefault(a => a.IntentId == intentId.Value);
        var toRemove = existing.Where(a => a.IntentId != intentId.Value).ToList();
        if (toRemove.Count > 0)
        {
            _vectorDb.TicketIntentAssignments.RemoveRange(toRemove);
        }

        var now = DateTime.UtcNow;
        var version = string.IsNullOrWhiteSpace(classifierVersion)
            ? ManualClassifierVersion
            : classifierVersion.Trim();

        if (keep is null)
        {
            _vectorDb.TicketIntentAssignments.Add(new TicketIntentAssignment
            {
                TicketId = ticketId,
                TicketNumber = string.IsNullOrWhiteSpace(ticketNumber) ? null : ticketNumber.Trim(),
                IntentId = intentId.Value,
                Confidence = confidence,
                ClassifierVersion = version,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            keep.TicketNumber = string.IsNullOrWhiteSpace(ticketNumber) ? keep.TicketNumber : ticketNumber.Trim();
            keep.Confidence = confidence ?? keep.Confidence;
            keep.ClassifierVersion = version;
            keep.UpdatedAt = now;
        }

        await _vectorDb.SaveChangesAsync(cancellationToken);
    }

}

