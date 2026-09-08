namespace MoneyPenny.Services.Rag.Generation;

public interface IGenerationService
{
    Task<GenerateAnswerResult> GenerateAnswerAsync(
        GenerateAnswerRequest request,
        CancellationToken cancellationToken = default);
}
