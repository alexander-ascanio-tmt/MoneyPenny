namespace MoneyPenny.Services.Rag.Prompts;

public sealed class RagResolvedPrompt
{
    public required string SystemPrompt { get; init; }
    public required string UserPromptTemplate { get; init; }
    public required string GenerationQuestion { get; init; }
    public required string PromptVersion { get; init; }
    public required string TemplateCode { get; init; }
}

public sealed class RagPromptResolveRequest
{
    public int? TicketId { get; init; }
    public bool? IsUrgent { get; init; }
    public string? IntentCode { get; init; }
}
