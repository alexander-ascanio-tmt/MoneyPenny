using MoneyPenny.ViewModels.Tickets;



namespace MoneyPenny.Services.Tickets;



public interface ITicketIntentService

{

    Task<IReadOnlyList<TicketIntentOptionViewModel>> GetActiveIntentsAsync(

        CancellationToken cancellationToken = default);



    Task<int?> GetAssignedIntentIdAsync(

        int ticketId,

        CancellationToken cancellationToken = default);



    Task AssignAsync(
        int ticketId,
        string? ticketNumber,
        int? intentId,
        double? confidence = null,
        string? classifierVersion = null,
        CancellationToken cancellationToken = default);

}

