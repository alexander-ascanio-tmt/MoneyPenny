namespace MoneyPenny.Services.Rag.Generation;

public sealed class GenerateAnswerResult
{
    public required string Answer { get; init; }
    public required string PromptVersion { get; init; }
    public required string TemplateCode { get; init; }
    public required string GenerationQuestion { get; init; }
}

public sealed class GenerateAnswerRequest
{
    public required string Question { get; init; }
    public required string Context { get; init; }
    public string? CurrentTicketNumber { get; init; }
    public required string CurrentTicketFirstComment { get; init; }
    public int? TicketId { get; init; }
    public bool? IsUrgent { get; init; }
    public string? IntentCode { get; init; }
}
