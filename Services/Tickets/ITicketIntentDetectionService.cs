namespace MoneyPenny.Services.Tickets;



public interface ITicketIntentDetectionService

{

    Task<TicketIntentDetectionResult> DetectAndSaveAsync(

        int ticketId,

        string? ticketNumber,

        string? firstCommentHtml,

        string? messageBoxDetail,

        CancellationToken cancellationToken = default);

}



public sealed class TicketIntentDetectionResult

{

    public int? IntentId { get; init; }

    public string? IntentCode { get; init; }

    public string? Reason { get; init; }

    public double? Confidence { get; init; }

}

