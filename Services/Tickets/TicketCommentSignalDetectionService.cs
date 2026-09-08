using MoneyPenny.Helpers;
using MoneyPenny.Services.Cv;
using MoneyPenny.Services.TeamSupport;
using MoneyPenny.ViewModels.Tickets;

namespace MoneyPenny.Services.Tickets;

public interface ITicketCommentSignalDetectionService
{
    Task<TicketCommentSignalDetectionResult> DetectAndSaveAsync(
        int ticketId,
        string? ticketNumber,
        TicketActionViewModel firstComment,
        string? teamSupportTicketId,
        CancellationToken cancellationToken = default);
}

public sealed class TicketCommentSignalDetectionResult
{
    public bool HasMessageBox { get; init; }
    public bool HasAttachment { get; init; }
    public int ImagesChecked { get; init; }
    public string? MessageBoxSummary { get; init; }
}

public sealed class TicketCommentSignalDetectionService : ITicketCommentSignalDetectionService
{
    private readonly ICommentImageMessageBoxService _messageBoxService;
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly ITicketCommentSignalsService _signalsService;
    private readonly ILogger<TicketCommentSignalDetectionService> _logger;

    public TicketCommentSignalDetectionService(
        ICommentImageMessageBoxService messageBoxService,
        ITeamSupportAttachmentService attachmentService,
        ITicketCommentSignalsService signalsService,
        ILogger<TicketCommentSignalDetectionService> logger)
    {
        _messageBoxService = messageBoxService;
        _attachmentService = attachmentService;
        _signalsService = signalsService;
        _logger = logger;
    }

    public async Task<TicketCommentSignalDetectionResult> DetectAndSaveAsync(
        int ticketId,
        string? ticketNumber,
        TicketActionViewModel firstComment,
        string? teamSupportTicketId,
        CancellationToken cancellationToken = default)
    {
        var attachments = firstComment.Attachments;
        if (attachments.Count == 0
            && !string.IsNullOrWhiteSpace(firstComment.TeamSupportActionId)
            && (firstComment.PendingAttachmentResolution
                || TicketHtmlHelper.ContentMentionsAttachment(firstComment.Content)))
        {
            try
            {
                var resolved = await _attachmentService.ResolveAttachmentsAsync(
                    firstComment.TeamSupportActionId,
                    teamSupportTicketId,
                    firstComment.Content,
                    cancellationToken);
                attachments = resolved
                    .Select(item => new TicketAttachmentViewModel
                    {
                        OriginalUrl = item.OriginalUrl,
                        FileName = item.FileName,
                        IsImage = item.IsImage
                    })
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudieron resolver adjuntos del comentario #1 del ticket {TicketId}.",
                    ticketId);
            }
        }

        var hasAttachment = attachments.Count > 0
            || TicketHtmlHelper.ContentMentionsAttachment(firstComment.Content);

        var imageUrls = CommentImageHelper.GetDisplayableImageUrls(
            firstComment.Content,
            attachments.Select(item => new CommentImageHelper.CommentImageSource(
                item.OriginalUrl,
                item.FileName,
                item.IsImage)));

        var hasMessageBox = false;
        string? messageBoxSummary = null;
        var imagesChecked = 0;

        foreach (var imageUrl in imageUrls)
        {
            imagesChecked++;
            try
            {
                var detection = await _messageBoxService.DetectFromUrlAsync(imageUrl, cancellationToken);
                if (!detection.Success)
                {
                    continue;
                }

                if (detection.Detected)
                {
                    hasMessageBox = true;
                    messageBoxSummary = detection.Summary;
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Error OpenCV MessageBox en imagen del ticket {TicketId}.",
                    ticketId);
            }
        }

        await _signalsService.SaveAsync(
            ticketId,
            ticketNumber,
            hasMessageBox,
            hasAttachment,
            cancellationToken,
            messageBoxDetail: hasMessageBox
                ? (messageBoxSummary ?? "MessageBox detectada con OpenCV.")
                : null,
            attachmentDetail: hasAttachment
                ? "Adjunto detectado en el comentario #1."
                : null,
            replaceDetails: true);

        return new TicketCommentSignalDetectionResult
        {
            HasMessageBox = hasMessageBox,
            HasAttachment = hasAttachment,
            ImagesChecked = imagesChecked,
            MessageBoxSummary = messageBoxSummary
        };
    }
}
