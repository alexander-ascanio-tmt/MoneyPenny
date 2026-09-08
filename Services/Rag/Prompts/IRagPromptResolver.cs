namespace MoneyPenny.Services.Rag.Prompts;

public interface IRagPromptResolver
{
    Task<RagResolvedPrompt> ResolveAsync(
        RagPromptResolveRequest request,
        CancellationToken cancellationToken = default);
}
