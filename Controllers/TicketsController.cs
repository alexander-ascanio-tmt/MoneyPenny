using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyPenny.Helpers;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Models.Rag;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Options;
using MoneyPenny.Services.Cv;
using MoneyPenny.Services.Ocr;
using MoneyPenny.Services.Rag;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Pricing;
using MoneyPenny.Services.Rag.Validation;
using MoneyPenny.Services.Rag.Prompts;
using MoneyPenny.Services.TeamSupport;
using MoneyPenny.Services.Tickets;
using MoneyPenny.ViewModels.Rag;
using MoneyPenny.ViewModels.Shared;
using MoneyPenny.ViewModels.Tickets;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private readonly ITicketService _ticketService;
    private readonly ITicketIntentService _ticketIntentService;
    private readonly ITicketCommentSignalsService _ticketCommentSignalsService;
    private readonly ITicketCommentSignalDetectionService _ticketCommentSignalDetectionService;
    private readonly ITicketUrgencyService _ticketUrgencyService;
    private readonly ITicketUrgencyDetectionService _ticketUrgencyDetectionService;
    private readonly ITicketIntentDetectionService _ticketIntentDetectionService;
    private readonly ITicketProcessedCommentService _ticketProcessedCommentService;
    private readonly ITicketIngestionService _ingestionService;
    private readonly ICommentContentService _commentContentService;
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly ICommentImageOcrService _commentImageOcrService;
    private readonly ICommentImageMessageBoxService _commentImageMessageBoxService;
    private readonly IImageTextExtractionService _imageTextExtractionService;
    private readonly IRagOrchestrator _ragOrchestrator;
    private readonly IRagTokenEstimateService _tokenEstimateService;
    private readonly IResponseGroundingChecker _groundingChecker;
    private readonly IVectorRepository _vectorRepository;
    private readonly IRagAskResultCache _ragAskResultCache;
    private readonly ITeamSupportActionApiClient _teamSupportActionApiClient;
    private readonly TeamSupportApiOptions _teamSupportOptions;
    private readonly RagOptions _ragOptions;

    public TicketsController(
        ITicketService ticketService,
        ITicketIntentService ticketIntentService,
        ITicketCommentSignalsService ticketCommentSignalsService,
        ITicketCommentSignalDetectionService ticketCommentSignalDetectionService,
        ITicketUrgencyService ticketUrgencyService,
        ITicketUrgencyDetectionService ticketUrgencyDetectionService,
        ITicketIntentDetectionService ticketIntentDetectionService,
        ITicketProcessedCommentService ticketProcessedCommentService,
        ITicketIngestionService ingestionService,
        ICommentContentService commentContentService,
        ITeamSupportAttachmentService attachmentService,
        ICommentImageOcrService commentImageOcrService,
        ICommentImageMessageBoxService commentImageMessageBoxService,
        IImageTextExtractionService imageTextExtractionService,
        IRagOrchestrator ragOrchestrator,
        IRagTokenEstimateService tokenEstimateService,
        IResponseGroundingChecker groundingChecker,
        IVectorRepository vectorRepository,
        IRagAskResultCache ragAskResultCache,
        ITeamSupportActionApiClient teamSupportActionApiClient,
        IOptions<TeamSupportApiOptions> teamSupportOptions,
        IOptions<RagOptions> ragOptions)
    {
        _ticketService = ticketService;
        _ticketIntentService = ticketIntentService;
        _ticketCommentSignalsService = ticketCommentSignalsService;
        _ticketCommentSignalDetectionService = ticketCommentSignalDetectionService;
        _ticketUrgencyService = ticketUrgencyService;
        _ticketUrgencyDetectionService = ticketUrgencyDetectionService;
        _ticketIntentDetectionService = ticketIntentDetectionService;
        _ticketProcessedCommentService = ticketProcessedCommentService;
        _ingestionService = ingestionService;
        _commentContentService = commentContentService;
        _attachmentService = attachmentService;
        _commentImageOcrService = commentImageOcrService;
        _commentImageMessageBoxService = commentImageMessageBoxService;
        _imageTextExtractionService = imageTextExtractionService;
        _ragOrchestrator = ragOrchestrator;
        _tokenEstimateService = tokenEstimateService;
        _groundingChecker = groundingChecker;
        _vectorRepository = vectorRepository;
        _ragAskResultCache = ragAskResultCache;
        _teamSupportActionApiClient = teamSupportActionApiClient;
        _teamSupportOptions = teamSupportOptions.Value;
        _ragOptions = ragOptions.Value;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? group,
        string? groupName,
        string? customerName,
        string? showUnknownCompany,
        string? customer,
        string? product,
        string? estado,
        string? priority,
        string? indexed,
        string? rag,
        string? hasActions,
        string? isKnowledgeBase,
        string? limit,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken)
    {
        var filters = new TicketFilters
        {
            Search = search,
            StatusText = status,
            GroupName = string.IsNullOrWhiteSpace(groupName) ? group : groupName,
            CustomerName = customerName,
            ShowUnknownCompany = string.IsNullOrWhiteSpace(showUnknownCompany) ? "false" : showUnknownCompany,
            Customer = customer,
            Product = product,
            Status = estado,
            Priority = priority,
            Indexed = indexed,
            Rag = rag,
            HasActions = hasActions,
            IsKnowledgeBase = isKnowledgeBase,
            ResultLimit = string.IsNullOrWhiteSpace(limit) ? "100" : limit,
            SortBy = sortBy,
            SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir
        };

        var model = await _ticketService.GetListAsync(filters, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _ticketService.GetDetailAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpGet("/Tickets/Details/Rag/{id:int}")]
    public async Task<IActionResult> Rag(
        int id,
        string? gptResult = null,
        bool focusGpt = false,
        CancellationToken cancellationToken = default)
    {
        var model = await _ticketService.GetRagDetailAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        model.FocusGpt = focusGpt;
        try
        {
            model.Intents = await _ticketIntentService.GetActiveIntentsAsync(cancellationToken);
            model.SelectedIntentId = await _ticketIntentService.GetAssignedIntentIdAsync(id, cancellationToken);
        }
        catch
        {
            model.Intents = [];
            model.SelectedIntentId = null;
        }

        try
        {
            var signals = await _ticketCommentSignalsService.GetAsync(id, cancellationToken);
            if (signals is not null)
            {
                model.HasMessageBox = signals.HasMessageBox;
                model.HasAttachment = signals.HasAttachment;
                model.MessageBoxDetail = signals.MessageBoxDetail;
                model.AttachmentDetail = signals.AttachmentDetail;
            }
        }
        catch
        {
            model.HasMessageBox = false;
            model.HasAttachment = false;
            model.MessageBoxDetail = null;
            model.AttachmentDetail = null;
        }

        try
        {
            var urgency = await _ticketUrgencyService.GetAsync(id, cancellationToken);
            if (urgency is not null)
            {
                model.IsUrgent = urgency.IsUrgent;
                model.UrgencyReason = urgency.Reason;
            }
        }
        catch
        {
            model.IsUrgent = false;
            model.UrgencyReason = null;
        }

        try
        {
            var processedComment = await _ticketProcessedCommentService.GetAsync(id, cancellationToken);
            if (processedComment is not null)
            {
                model.ProcessedFirstCommentContent = processedComment.ProcessedText;
                model.ProcessedCommentImageWarning = processedComment.ImageExtractionWarning;
            }
        }
        catch
        {
            model.ProcessedFirstCommentContent = null;
            model.ProcessedCommentImageWarning = null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";

        if (!string.IsNullOrWhiteSpace(gptResult))
        {
            var cached = _ragAskResultCache.Get(userId, gptResult);
            if (cached?.Response is not null)
            {
                ApplyGptResponseToModel(model, cached.Response, fromHistory: false);
            }
        }

        if (string.IsNullOrWhiteSpace(model.ErrorMessage)
            && model.FirstComment is not null
            && model.IsFirstCommentIndexed
            && model.HasGeneratedContext)
        {
            try
            {
                var ragResponse = await _ragOrchestrator.ProcessTicketAsync(
                    new AskTicketViewModel
                    {
                        TicketId = id,
                        TicketNumber = model.Number,
                        GenerateGptAnswer = false,
                        KnowledgeBaseOnly = false,
                        SkipQueryLog = true
                    },
                    userId,
                    cancellationToken);

                model.ContextItems = ragResponse.ContextItems;
                ApplyGptEstimate(model, ragResponse);
            }
            catch
            {
                model.ContextItems = [];
            }
        }

        if (!model.HasGptAnswer)
        {
            await TryApplyStoredGptAnswerAsync(model, cancellationToken);
        }

        if (model.HasGptAnswer)
        {
            await EnsureGroundingContextAsync(model, userId, cancellationToken);
            ApplyGroundingReport(model);
            await TryLoadInsertedTeamSupportActionAsync(model, cancellationToken);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignIntent(
        int ticketId,
        string? ticketNumber,
        int? intentId,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return BadRequest(new { success = false, error = "TicketId inválido." });
        }

        try
        {
            await _ticketIntentService.AssignAsync(
                ticketId,
                ticketNumber,
                intentId,
                cancellationToken: cancellationToken);
            return Json(new { success = true, intentId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessComment(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return BadRequest(new { success = false, error = "TicketId inválido." });
        }

        var ticket = await _ticketService.GetRagDetailAsync(ticketId, cancellationToken);
        if (ticket?.FirstComment is null || string.IsNullOrWhiteSpace(ticket.FirstComment.Content))
        {
            return BadRequest(new { success = false, error = "El ticket no tiene comentario #1 con contenido." });
        }

        var commentContent = await _commentContentService.ToIndexableContentAsync(
            ticket.FirstComment.Content,
            new CommentContentRequest
            {
                ProcessImages = true,
                ImageCacheMode = ImageExtractionCacheMode.UseAndRefresh,
                RefreshImageTextCache = true,
                TicketId = ticketId,
                TicketActionId = ticket.FirstComment.Id,
                TeamSupportActionId = ticket.FirstComment.TeamSupportActionId,
                TeamSupportTicketId = ticket.TeamSupportId
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(commentContent.Text))
        {
            return BadRequest(new { success = false, error = "Tras procesar el comentario no quedó texto utilizable." });
        }

        await DetectCommentClassificationAsync(
            ticketId,
            ticket.Number,
            ticket.FirstComment,
            ticket.TeamSupportId,
            commentContent.Text,
            cancellationToken);

        try
        {
            await _ticketProcessedCommentService.SaveAsync(
                ticketId,
                ticket.FirstComment.Id,
                ticket.Number,
                commentContent.Text,
                commentContent.ImagesDetected,
                commentContent.ImagesExtracted,
                commentContent.ImageExtractionWarning,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = $"El comentario se procesó pero no se pudo guardar: {ex.Message}" });
        }

        TicketCommentSignals? signals = null;
        int? intentId = null;
        bool isUrgent = false;
        string? urgencyReason = null;

        try
        {
            signals = await _ticketCommentSignalsService.GetAsync(ticketId, cancellationToken);
        }
        catch
        {
            // Sin mensaje en UI si falla la lectura de señales.
        }

        try
        {
            intentId = await _ticketIntentService.GetAssignedIntentIdAsync(ticketId, cancellationToken);
        }
        catch
        {
            // Sin mensaje en UI si falla la lectura de intención.
        }

        try
        {
            var urgency = await _ticketUrgencyService.GetAsync(ticketId, cancellationToken);
            if (urgency is not null)
            {
                isUrgent = urgency.IsUrgent;
                urgencyReason = urgency.Reason;
            }
        }
        catch
        {
            // Sin mensaje en UI si falla la lectura de urgencia.
        }

        return Json(new
        {
            success = true,
            filteredText = commentContent.Text,
            imagesDetected = commentContent.ImagesDetected,
            imagesExtracted = commentContent.ImagesExtracted,
            warning = commentContent.ImageExtractionWarning,
            hasMessageBox = signals?.HasMessageBox ?? false,
            hasAttachment = signals?.HasAttachment ?? false,
            messageBoxDetail = signals?.MessageBoxDetail,
            attachmentDetail = signals?.AttachmentDetail,
            intentId,
            isUrgent,
            urgencyReason
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSignals(
        int ticketId,
        string? ticketNumber,
        bool hasMessageBox,
        bool hasAttachment,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return BadRequest(new { success = false, error = "TicketId inválido." });
        }

        try
        {
            await _ticketCommentSignalsService.SaveAsync(
                ticketId,
                ticketNumber,
                hasMessageBox,
                hasAttachment,
                cancellationToken);
            return Json(new { success = true, hasMessageBox, hasAttachment });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUrgency(
        int ticketId,
        string? ticketNumber,
        bool isUrgent,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return BadRequest(new { success = false, error = "TicketId inválido." });
        }

        try
        {
            await _ticketUrgencyService.SaveAsync(
                ticketId,
                ticketNumber,
                isUrgent,
                classifierVersion: TicketUrgencyService.ManualClassifierVersion,
                cancellationToken: cancellationToken);
            return Json(new { success = true, isUrgent });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConsultGptAnswer(
        int ticketId,
        string? ticketNumber,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return NotFound();
        }

        var ticket = await _ticketService.GetRagDetailAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!ticket.IsFirstCommentIndexed)
        {
            TempData["WarningMessage"] =
                "El comentario #1 de este ticket aún no está indexado. Usa «Indexar comentario» antes de generar la respuesta GPT.";
            return RedirectToAction(nameof(Rag), new { id = ticketId });
        }

        if (!ticket.HasGeneratedContext)
        {
            TempData["WarningMessage"] =
                "Genera el contexto recuperado antes de generar la respuesta GPT.";
            return RedirectToAction(nameof(Rag), new { id = ticketId });
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            ticketNumber = ticket.Number;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
        try
        {
            var response = await _ragOrchestrator.ProcessTicketAsync(
                new AskTicketViewModel
                {
                    TicketId = ticketId,
                    TicketNumber = ticketNumber,
                    GenerateGptAnswer = true,
                    KnowledgeBaseOnly = false
                },
                userId,
                cancellationToken);

            var contextText = response.GptContextText
                ?? string.Join("\n---\n", response.ContextItems.Select(c => c.Content));
            var contextLoadEstimate = _tokenEstimateService.EstimateRagContextLoad(response.FirstComment?.Content);
            var gptEstimate = _tokenEstimateService.EstimateRagGptAnswer(
                contextText,
                response.FirstComment?.Content);
            var combinedEstimate = _tokenEstimateService.Combine(contextLoadEstimate, gptEstimate);

            response.GptEstimate = TokenUsageEstimateViewModel.FromEstimate(
                combinedEstimate,
                "Consumo estimado de la consulta");

            if (response.HasGptAnswer)
            {
                response.LastRunEstimate = TokenUsageEstimateViewModel.FromEstimate(
                    gptEstimate,
                    "Consumo estimado de la respuesta GPT");

                response.GroundingReport = _groundingChecker.Evaluate(new ResponseGroundingRequest
                {
                    Answer = response.Answer,
                    FirstCommentContent = response.FirstComment?.Content,
                    ContextItems = response.ContextItems,
                    TicketNumber = response.TicketNumber,
                    KnowledgeBaseSolutionText = response.KnowledgeBaseSolution?.Text
                });
            }

            var cacheKey = _ragAskResultCache.Store(userId, new RagAskCachedResult { Response = response });
            return RedirectToAction(nameof(Rag), new { id = ticketId, gptResult = cacheKey });
        }
        catch (Exception ex)
        {
            TempData["WarningMessage"] = $"No se pudo generar la respuesta GPT: {ex.Message}";
            return RedirectToAction(nameof(Rag), new { id = ticketId });
        }
    }

    private static void ApplyGptResponseToModel(
        TicketRagViewModel model,
        RagResponseViewModel response,
        bool fromHistory)
    {
        model.HasGptAnswer = response.HasGptAnswer;
        model.Answer = response.Answer ?? string.Empty;
        model.GptAnswerFromHistory = fromHistory || response.GptAnswerFromHistory;
        model.GptAnswerSavedAt = response.GptAnswerSavedAt;
        model.GptQueryLogId = response.GptQueryLogId;
        model.GptRating = response.GptRating;
        model.GptTeamSupportActionId = response.GptTeamSupportActionId;
        model.GptTeamSupportActionInserted = response.GptTeamSupportActionInserted;
        model.GptTeamSupportActionWarning = response.GptTeamSupportActionWarning;
        model.GptEstimate = response.GptEstimate;
        model.LastRunEstimate = response.LastRunEstimate;
        model.GroundingReport = response.GroundingReport;
        model.InsertedTeamSupportAction = response.InsertedTeamSupportAction;
        model.GptPromptTemplateCode = response.GptPromptTemplateCode;
        if (response.ContextItems.Count > 0 && model.HasGeneratedContext)
        {
            model.ContextItems = response.ContextItems;
        }
    }

    private void ApplyGptEstimate(TicketRagViewModel model, RagResponseViewModel ragResponse)
    {
        if (model.GptEstimate is not null)
        {
            return;
        }

        var contextText = ragResponse.GptContextText
            ?? string.Join("\n---\n", ragResponse.ContextItems.Select(c => c.Content));
        var contextLoadEstimate = _tokenEstimateService.EstimateRagContextLoad(
            model.IndexedFirstCommentContent ?? model.FirstComment?.Content);
        var gptEstimate = _tokenEstimateService.EstimateRagGptAnswer(
            contextText,
            model.IndexedFirstCommentContent ?? model.FirstComment?.Content);
        var combinedEstimate = _tokenEstimateService.Combine(contextLoadEstimate, gptEstimate);
        model.GptEstimate = TokenUsageEstimateViewModel.FromEstimate(
            combinedEstimate,
            "Estimación al pulsar «Generar respuesta GPT»");
    }

    private async Task TryApplyStoredGptAnswerAsync(
        TicketRagViewModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _vectorRepository.GetLatestQueryLogByTicketAsync(
                model.Id,
                RagResponseType.Gpt,
                cancellationToken);

            if (log is null || string.IsNullOrWhiteSpace(log.Answer))
            {
                return;
            }

            model.Answer = log.Answer;
            model.HasGptAnswer = true;
            model.GptQueryLogId = log.Id;
            model.GptRating = log.Rating;
            model.GptAnswerFromHistory = true;
            model.GptAnswerSavedAt = log.CreatedAt;
            model.GptTeamSupportActionId = log.TeamSupportActionId;
            model.GptTeamSupportActionInserted = !string.IsNullOrWhiteSpace(log.TeamSupportActionId);
            model.GptPromptTemplateCode = ParsePromptTemplateCode(log.PromptVersion);
        }
        catch
        {
            // ignore history load failures
        }
    }

    private async Task EnsureGroundingContextAsync(
        TicketRagViewModel model,
        string userId,
        CancellationToken cancellationToken)
    {
        if (model.ContextItems.Any() || model.GroundingReport is not null || !model.IsFirstCommentIndexed)
        {
            return;
        }

        try
        {
            var ragResponse = await _ragOrchestrator.ProcessTicketAsync(
                new AskTicketViewModel
                {
                    TicketId = model.Id,
                    TicketNumber = model.Number,
                    GenerateGptAnswer = false,
                    KnowledgeBaseOnly = false,
                    SkipQueryLog = true
                },
                userId,
                cancellationToken);

            // Solo para comprobación técnica; no mostrar en UI si el contexto no se generó con el botón.
            if (model.HasGeneratedContext)
            {
                model.ContextItems = ragResponse.ContextItems;
            }
            else
            {
                model.GroundingReport = _groundingChecker.Evaluate(new ResponseGroundingRequest
                {
                    Answer = model.Answer,
                    FirstCommentContent = model.IndexedFirstCommentContent ?? model.FirstComment?.Content,
                    ContextItems = ragResponse.ContextItems,
                    TicketNumber = model.Number
                });
            }
        }
        catch
        {
            // grounding optional
        }
    }

    private void ApplyGroundingReport(TicketRagViewModel model)
    {
        if (model.GroundingReport is not null
            || !model.HasGptAnswer
            || string.IsNullOrWhiteSpace(model.Answer))
        {
            return;
        }

        model.GroundingReport = _groundingChecker.Evaluate(new ResponseGroundingRequest
        {
            Answer = model.Answer,
            FirstCommentContent = model.IndexedFirstCommentContent ?? model.FirstComment?.Content,
            ContextItems = model.ContextItems,
            TicketNumber = model.Number
        });
    }

    private async Task TryLoadInsertedTeamSupportActionAsync(
        TicketRagViewModel model,
        CancellationToken cancellationToken)
    {
        if (!model.HasGptAnswer || string.IsNullOrWhiteSpace(model.GptTeamSupportActionId))
        {
            return;
        }

        if (model.InsertedTeamSupportAction?.LoadedFromApi == true)
        {
            return;
        }

        var teamSupportTicketId = !string.IsNullOrWhiteSpace(model.TeamSupportId)
            ? model.TeamSupportId
            : model.Number;

        if (string.IsNullOrWhiteSpace(teamSupportTicketId))
        {
            model.InsertedTeamSupportAction = new GptTeamSupportActionViewModel
            {
                ActionId = model.GptTeamSupportActionId,
                LoadError = "No se pudo resolver el identificador del ticket en TeamSupport."
            };
            return;
        }

        var actionInfo = await _teamSupportActionApiClient.GetTicketActionAsync(
            teamSupportTicketId,
            model.GptTeamSupportActionId,
            cancellationToken);

        model.InsertedTeamSupportAction = MapTeamSupportAction(actionInfo, model.GptTeamSupportActionId);
        if (model.InsertedTeamSupportAction is not null && model.GptQueryLogId is > 0)
        {
            model.InsertedTeamSupportAction.QueryLogId = model.GptQueryLogId;
            model.InsertedTeamSupportAction.Rating = model.GptRating;
        }
    }

    private static GptTeamSupportActionViewModel MapTeamSupportAction(
        TeamSupportActionInfo actionInfo,
        string? fallbackActionId)
    {
        if (!actionInfo.Found)
        {
            return new GptTeamSupportActionViewModel
            {
                ActionId = fallbackActionId,
                LoadError = actionInfo.ErrorMessage ?? "No se pudo cargar el comentario desde TeamSupport."
            };
        }

        return new GptTeamSupportActionViewModel
        {
            ActionId = actionInfo.ActionId ?? fallbackActionId,
            DescriptionHtml = actionInfo.DescriptionHtml,
            CreatorName = actionInfo.CreatorName,
            CreatedAt = actionInfo.CreatedAt,
            IsPrivate = actionInfo.IsPrivate,
            Source = actionInfo.Source,
            LoadedFromApi = true
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateContext(
        int id,
        string? ticketNumber,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var ticket = await _ticketService.GetRagDetailAsync(id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!ticket.IsFirstCommentIndexed)
        {
            TempData["WarningMessage"] =
                "El comentario #1 de este ticket aún no está indexado. Usa «Indexar comentario» para vectorizarlo antes de generar el contexto.";
            return RedirectToAction(nameof(Rag), new { id });
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            ticketNumber = ticket.Number;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
        try
        {
            var ragResponse = await _ragOrchestrator.ProcessTicketAsync(
                new AskTicketViewModel
                {
                    TicketId = id,
                    TicketNumber = ticketNumber,
                    GenerateGptAnswer = false,
                    KnowledgeBaseOnly = false,
                    SkipQueryLog = false
                },
                userId,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(ragResponse.ErrorMessage))
            {
                TempData["WarningMessage"] = ragResponse.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] =
                    ragResponse.ContextItems.Count > 0
                        ? $"Contexto generado: {ragResponse.ContextItems.Count} ticket(s) similar(es)."
                        : "Contexto generado, pero no se encontraron tickets similares.";
            }
        }
        catch (Exception ex)
        {
            TempData["WarningMessage"] = $"No se pudo generar el contexto: {ex.Message}";
        }

        return RedirectToAction(nameof(Rag), new { id });
    }

    public async Task<IActionResult> Attachment(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) || !_attachmentService.IsAllowedAttachmentUrl(url))
        {
            return BadRequest();
        }

        var download = await _attachmentService.DownloadAsync(url, cancellationToken);
        if (download is null)
        {
            if (string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentCookie)
                && string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentBearerToken))
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    "No hay credenciales de TeamSupport configuradas para descargar adjuntos.");
            }

            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=300";

        if (download.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return File(download.Content, download.ContentType);
        }

        return File(download.Content, download.ContentType, download.FileName);
    }

    public async Task<IActionResult> ResolveActionAttachments(
        string teamSupportActionId,
        string? teamSupportTicketId,
        string? content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teamSupportActionId))
        {
            return BadRequest();
        }

        var attachments = await _attachmentService.ResolveAttachmentsAsync(
            teamSupportActionId,
            teamSupportTicketId,
            content,
            cancellationToken);

        return Json(attachments.Select(item => new
        {
            originalUrl = item.OriginalUrl,
            fileName = item.FileName,
            isImage = item.IsImage,
            proxyUrl = Url.Action("Attachment", "Tickets", new { url = item.OriginalUrl })
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtractImageText(
        [FromForm] string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { error = "La URL de la imagen es obligatoria." });
        }

        var result = await _commentImageOcrService.ExtractTextFromUrlAsync(url, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage ?? "No se pudo extraer el texto." });
        }

        return Json(new { text = result.Text });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtractImageTextVision(
        [FromForm] string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { error = "La URL de la imagen es obligatoria." });
        }

        if (!_attachmentService.IsAllowedAttachmentUrl(url))
        {
            return BadRequest(new { error = "La URL de la imagen no está permitida." });
        }

        if (!_ragOptions.EnableImageTextExtraction)
        {
            return BadRequest(new { error = "La extracción con OpenAI Vision está deshabilitada en la configuración." });
        }

        var result = await _imageTextExtractionService.ExtractAsync([url], cancellationToken);
        var text = result.Texts.FirstOrDefault()?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            var error = string.IsNullOrWhiteSpace(result.Warning)
                ? "No se pudo extraer texto de la imagen con OpenAI Vision."
                : result.Warning;
            return BadRequest(new { error });
        }

        return Json(new { text, warning = result.Warning });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetectMessageBox(
        [FromForm] string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { error = "La URL de la imagen es obligatoria." });
        }

        var result = await _commentImageMessageBoxService.DetectFromUrlAsync(url, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage ?? "No se pudo analizar la imagen." });
        }

        return Json(new
        {
            detected = result.Detected,
            confidence = result.Confidence,
            summary = result.Summary,
            titleText = result.TitleText,
            messageText = result.MessageText,
            elements = result.Elements.Select(item => new
            {
                type = item.Type,
                x = item.X,
                y = item.Y,
                width = item.Width,
                height = item.Height,
                score = item.Score
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetectMessageBoxVision(
        [FromForm] string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { error = "La URL de la imagen es obligatoria." });
        }

        if (!_ragOptions.EnableImageTextExtraction)
        {
            return BadRequest(new { error = "La extracción con OpenAI Vision está deshabilitada en la configuración." });
        }

        var result = await _commentImageMessageBoxService.DetectWithVisionFromUrlAsync(url, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage ?? "No se pudo analizar la imagen." });
        }

        return Json(new
        {
            detected = result.Detected,
            confidence = result.Confidence,
            summary = result.Summary,
            titleText = result.TitleText,
            messageText = result.MessageText,
            elements = result.Elements.Select(item => new
            {
                type = item.Type,
                x = item.X,
                y = item.Y,
                width = item.Width,
                height = item.Height,
                score = item.Score
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IndexTicket(
        int id,
        bool processImages = true,
        string? returnTo = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(returnTo, nameof(Rag), StringComparison.OrdinalIgnoreCase))
        {
            processImages = false;
            var processedComment = await _ticketProcessedCommentService.GetAsync(id, cancellationToken);
            if (processedComment is null || string.IsNullOrWhiteSpace(processedComment.ProcessedText))
            {
                TempData["WarningMessage"] =
                    "Procesa el comentario antes de indexarlo. Usa «Procesar comentario» en el apartado correspondiente.";
                return RedirectToAction(nameof(Rag), new { id });
            }

            var indexText = TicketHtmlHelper.StripExtractedImageText(processedComment.ProcessedText);
            if (string.IsNullOrWhiteSpace(indexText))
            {
                TempData["WarningMessage"] =
                    "Tras preparar el comentario procesado no quedó contenido indexable. Revisa el apartado «Comentario procesado».";
                return RedirectToAction(nameof(Rag), new { id });
            }

            var result = await _ingestionService.IndexTicketAsync(
                id,
                processImages: false,
                processedFirstCommentTextForIndex: indexText,
                cancellationToken: cancellationToken);

            TempData["SuccessMessage"] = $"Ticket {id} indexado correctamente ({result.ChunkCount} fragmentos).";
            return RedirectToAction(nameof(Rag), new { id });
        }

        var defaultResult = await _ingestionService.IndexTicketAsync(id, processImages, cancellationToken: cancellationToken);

        if (defaultResult.ProcessImages && defaultResult.ImagesDetected > 0 && defaultResult.ImagesExtracted == 0)
        {
            if (string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentCookie)
                && string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentBearerToken))
            {
                TempData["WarningMessage"] =
                    $"Ticket {id} indexado, pero no se pudo extraer texto de {defaultResult.ImagesDetected} imagen(es). " +
                    "Configura ExternalApis:TeamSupport:AttachmentCookie en appsettings.Development.json.";
            }
            else
            {
                var detail = string.IsNullOrWhiteSpace(defaultResult.ImageExtractionWarning)
                    ? "La cookie de TeamSupport puede haber caducado: renueva la sesión en el navegador, copia de nuevo las cookies y reinicia la app."
                    : defaultResult.ImageExtractionWarning;

                TempData["WarningMessage"] =
                    $"Ticket {id} indexado, pero no se pudo extraer texto de {defaultResult.ImagesDetected} imagen(es). {detail}";
            }
        }
        else if (defaultResult.ProcessImages && defaultResult.ImagesExtracted > 0)
        {
            TempData["SuccessMessage"] =
                $"Ticket {id} indexado con texto extraído de {defaultResult.ImagesExtracted} imagen(es).";
        }
        else if (!defaultResult.ProcessImages && defaultResult.ImagesDetected > 0)
        {
            TempData["SuccessMessage"] =
                $"Ticket {id} indexado sin procesar {defaultResult.ImagesDetected} imagen(es) del comentario.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Ticket {id} indexado correctamente ({defaultResult.ChunkCount} fragmentos).";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private static string? ParsePromptTemplateCode(string? promptVersion)
    {
        if (string.IsNullOrWhiteSpace(promptVersion))
        {
            return null;
        }

        var separator = promptVersion.IndexOf('+');
        return separator > 0
            ? promptVersion[..separator]
            : promptVersion;
    }

    private async Task DetectCommentClassificationAsync(
        int ticketId,
        string? ticketNumber,
        TicketActionViewModel firstComment,
        string? teamSupportTicketId,
        string filteredCommentText,
        CancellationToken cancellationToken)
    {
        try
        {
            await _ticketCommentSignalDetectionService.DetectAndSaveAsync(
                ticketId,
                ticketNumber,
                firstComment,
                teamSupportTicketId,
                cancellationToken);
        }
        catch
        {
            // Sin mensaje en UI si falla la detección automática.
        }

        TicketCommentSignals? detectedSignals = null;
        try
        {
            detectedSignals = await _ticketCommentSignalsService.GetAsync(ticketId, cancellationToken);
        }
        catch
        {
            // Sin mensaje en UI si falla la lectura de señales.
        }

        try
        {
            await _ticketIntentDetectionService.DetectAndSaveAsync(
                ticketId,
                ticketNumber,
                firstComment.Content,
                detectedSignals?.MessageBoxDetail,
                cancellationToken);
        }
        catch
        {
            // Sin mensaje en UI si falla la detección de intención.
        }

        try
        {
            await _ticketUrgencyDetectionService.DetectFromPhrasesAndSaveAsync(
                ticketId,
                ticketNumber,
                filteredCommentText,
                cancellationToken);
        }
        catch
        {
            // Sin mensaje en UI si falla la detección de urgencia.
        }
    }
}
