using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Tickets;

public interface ITicketUrgencyDetectionService
{
    Task<TicketUrgencyDetectionResult> DetectAndSaveAsync(
        int ticketId,
        string? ticketNumber,
        string? firstCommentHtml,
        string? messageBoxDetail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compara el comentario ya filtrado con las frases activas de /Urgency.
    /// </summary>
    Task<TicketUrgencyDetectionResult> DetectFromPhrasesAndSaveAsync(
        int ticketId,
        string? ticketNumber,
        string filteredCommentText,
        CancellationToken cancellationToken = default);
}

public sealed class TicketUrgencyDetectionResult
{
    public bool IsUrgent { get; init; }
    public string? Reason { get; init; }
    public double? Confidence { get; init; }
}
