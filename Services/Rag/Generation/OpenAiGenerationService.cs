using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using MoneyPenny.Services.Rag.Prompts;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Generation;

public class OpenAiGenerationService : IGenerationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRagPromptResolver _promptResolver;
    private readonly RagOptions _options;
    private readonly ILogger<OpenAiGenerationService> _logger;

    public OpenAiGenerationService(
        IHttpClientFactory httpClientFactory,
        IRagPromptResolver promptResolver,
        IOptions<RagOptions> options,
        ILogger<OpenAiGenerationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _promptResolver = promptResolver;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GenerateAnswerResult> GenerateAnswerAsync(
        GenerateAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ArgumentException("La pregunta no puede estar vacía.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CurrentTicketFirstComment))
        {
            throw new ArgumentException(
                "El comentario #1 indexado del ticket actual es obligatorio para generar la respuesta.",
                nameof(request));
        }

        var resolved = await _promptResolver.ResolveAsync(
            new RagPromptResolveRequest
            {
                TicketId = request.TicketId,
                IsUrgent = request.IsUrgent,
                IntentCode = request.IntentCode
            },
            cancellationToken);

        var generationQuestion = resolved.GenerationQuestion;

        var userPrompt = resolved.UserPromptTemplate
            .Replace("{{ticketNumber}}", string.IsNullOrWhiteSpace(request.CurrentTicketNumber) ? "N/D" : request.CurrentTicketNumber.Trim(), StringComparison.Ordinal)
            .Replace("{{currentTicketComment}}", request.CurrentTicketFirstComment.Trim(), StringComparison.Ordinal)
            .Replace("{{context}}", string.IsNullOrWhiteSpace(request.Context) ? "(Sin tickets similares recuperados)" : request.Context, StringComparison.Ordinal)
            .Replace("{{question}}", generationQuestion, StringComparison.Ordinal);

        var payload = new OpenAiChatRequest
        {
            Model = _options.ChatModel,
            Messages =
            [
                new OpenAiChatMessage { Role = "system", Content = resolved.SystemPrompt },
                new OpenAiChatMessage { Role = "user", Content = userPrompt }
            ],
            Temperature = 0.2
        };

        var client = _httpClientFactory.CreateClient(OpenAiEmbeddingService.HttpClientName);
        using var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "OpenAI chat respondió {StatusCode}: {Error}",
                (int)response.StatusCode,
                errorBody);

            throw new InvalidOperationException(
                $"No se pudo obtener la respuesta de OpenAI ({(int)response.StatusCode}). " +
                $"Verifica ExternalApis:OpenAI:ApiKey y el modelo '{_options.ChatModel}'. Detalle: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("OpenAI devolvió una respuesta vacía.");

        var answer = result.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("OpenAI no devolvió contenido en la respuesta.");
        }

        _logger.LogInformation(
            "Respuesta generada con modelo {Model}, prompt {PromptVersion} ({Length} caracteres).",
            _options.ChatModel,
            resolved.PromptVersion,
            answer.Length);

        return new GenerateAnswerResult
        {
            Answer = answer,
            PromptVersion = resolved.PromptVersion,
            TemplateCode = resolved.TemplateCode,
            GenerationQuestion = generationQuestion
        };
    }

    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAiChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChatChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChatChoice
    {
        [JsonPropertyName("message")]
        public OpenAiChatMessage? Message { get; set; }
    }
}
