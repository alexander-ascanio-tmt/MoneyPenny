using MoneyPenny.Services.Rag.Prompts;
using MoneyPenny.ViewModels.Rag;
using MoneyPenny.ViewModels.Shared;

namespace MoneyPenny.ViewModels.Tickets;

public class TicketRagViewModel
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? TeamSupportId { get; init; }
    public TicketActionViewModel? FirstComment { get; init; }
    public bool IsFirstCommentIndexed { get; init; }
    public bool HasGeneratedContext { get; init; }
    public string? IndexedFirstCommentContent { get; init; }
    public string? ProcessedFirstCommentContent { get; set; }
    public string? ProcessedCommentImageWarning { get; set; }
    public IReadOnlyList<TicketIntentOptionViewModel> Intents { get; set; } = [];
    public int? SelectedIntentId { get; set; }
    public bool HasMessageBox { get; set; }
    public bool HasAttachment { get; set; }
    public string? MessageBoxDetail { get; set; }
    public string? AttachmentDetail { get; set; }
    public bool IsUrgent { get; set; }
    public string? UrgencyReason { get; set; }
    public IReadOnlyList<RagContextItemViewModel> ContextItems { get; set; } = [];
    public string? ErrorMessage { get; init; }

    public bool HasGptAnswer { get; set; }
    public bool GptAnswerFromHistory { get; set; }
    public DateTime? GptAnswerSavedAt { get; set; }
    public string Answer { get; set; } = string.Empty;
    public int? GptQueryLogId { get; set; }
    public short? GptRating { get; set; }
    public string? GptTeamSupportActionId { get; set; }
    public bool GptTeamSupportActionInserted { get; set; }
    public string? GptTeamSupportActionWarning { get; set; }
    public TokenUsageEstimateViewModel? GptEstimate { get; set; }
    public TokenUsageEstimateViewModel? LastRunEstimate { get; set; }
    public ResponseGroundingReportViewModel? GroundingReport { get; set; }
    public GptTeamSupportActionViewModel? InsertedTeamSupportAction { get; set; }
    public string? GptPromptTemplateCode { get; set; }
    public bool GptIsAgentResponse => string.Equals(
        GptPromptTemplateCode,
        RagPromptResolver.AgentTemplateCode,
        StringComparison.OrdinalIgnoreCase);
    public bool FocusGpt { get; set; }
}