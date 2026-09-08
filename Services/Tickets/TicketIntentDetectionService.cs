using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Rag;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Tickets;

public sealed class TicketIntentDetectionService : ITicketIntentDetectionService
{
    public const string FallbackVersion = "fallback-otro";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly VectorDbContext _vectorDb;
    private readonly ITicketIntentService _intentService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RagOptions _ragOptions;
    private readonly ILogger<TicketIntentDetectionService> _logger;

    public TicketIntentDetectionService(
        VectorDbContext vectorDb,
        ITicketIntentService intentService,
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> ragOptions,
        ILogger<TicketIntentDetectionService> logger)
    {
        _vectorDb = vectorDb;
        _intentService = intentService;
        _httpClientFactory = httpClientFactory;
        _ragOptions = ragOptions.Value;
        _logger = logger;
    }

    public async Task<TicketIntentDetectionResult> DetectAndSaveAsync(
        int ticketId,
        string? ticketNumber,
        string? firstCommentHtml,
        string? messageBoxDetail,
        CancellationToken cancellationToken = default)
    {
        var plainComment = TicketHtmlHelper.ToPlainText(firstCommentHtml);
        if (string.IsNullOrWhiteSpace(plainComment))
        {
            return new TicketIntentDetectionResult();
        }

        var intents = await _vectorDb.TicketIntents
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        if (intents.Count == 0)
        {
            return new TicketIntentDetectionResult();
        }

        var config = await _vectorDb.IntentDetectionConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);

        IntentClassification classification;
        string classifierVersion;

        try
        {
            classification = await ClassifyWithGptAsync(
                plainComment,
                messageBoxDetail,
                config?.PromptText,
                intents,
                cancellationToken);
            classifierVersion = $"gpt-{_ragOptions.ChatModel}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clasificación GPT de intención falló para ticket {TicketId}.", ticketId);
            classification = ClassifyWithFallback(plainComment, intents);
            classifierVersion = FallbackVersion;
        }

        var intent = ResolveIntent(classification.IntentCode, intents);
        if (intent is null)
        {
            return new TicketIntentDetectionResult
            {
                IntentCode = classification.IntentCode,
                Reason = classification.Reason,
                Confidence = classification.Confidence
            };
        }

        await _intentService.AssignAsync(
            ticketId,
            ticketNumber,
            intent.Id,
            classification.Confidence,
            classifierVersion,
            cancellationToken);

        return new TicketIntentDetectionResult
        {
            IntentId = intent.Id,
            IntentCode = intent.Code,
            Reason = classification.Reason,
            Confidence = classification.Confidence
        };
    }

    private static IntentClassification ClassifyWithFallback(string plainComment, IReadOnlyList<TicketIntent> intents)
    {
        var lower = plainComment.ToLowerInvariant();
        string? code = null;

        if (lower.Contains("error", StringComparison.Ordinal)
            || lower.Contains("fallo", StringComparison.Ordinal)
            || lower.Contains("falla", StringComparison.Ordinal)
            || lower.Contains("incorrecto", StringComparison.Ordinal))
        {
            code = "error";
        }
        else if (lower.Contains("cómo", StringComparison.Ordinal)
                 || lower.Contains("como ", StringComparison.Ordinal)
                 || lower.Contains("pasos", StringComparison.Ordinal)
                 || lower.Contains("documentación", StringComparison.Ordinal)
                 || lower.Contains("documentacion", StringComparison.Ordinal))
        {
            code = "howto";
        }

        if (code is not null && ResolveIntent(code, intents) is not null)
        {
            return new IntentClassification(code, $"Coincidencia por palabras clave ({code}).", 0.55);
        }

        var otro = ResolveIntent("otro", intents) ?? intents.LastOrDefault();
        return new IntentClassification(
            otro?.Code ?? "otro",
            "No se pudo clasificar con certeza; se asignó otro.",
            0.4);
    }

    private async Task<IntentClassification> ClassifyWithGptAsync(
        string plainComment,
        string? messageBoxDetail,
        string? businessPrompt,
        IReadOnlyList<TicketIntent> intents,
        CancellationToken cancellationToken)
    {
        var catalog = string.Join(
            "\n",
            intents.Select(i =>
                $"- {i.Code}: {i.Name}" +
                (string.IsNullOrWhiteSpace(i.Description) ? string.Empty : $" — {i.Description}")));

        var userPrompt = $"""
            Criterios de negocio:
            {(string.IsNullOrWhiteSpace(businessPrompt) ? "(Sin criterios adicionales)" : businessPrompt.Trim())}

            Catálogo de intenciones (usa exactamente uno de los códigos):
            {catalog}

            Comentario #1 del cliente:
            {plainComment.Trim()}
            """;

        if (!string.IsNullOrWhiteSpace(messageBoxDetail))
        {
            userPrompt += $"""

                Texto extraído de captura (MessageBox):
                {messageBoxDetail.Trim()}
                """;
        }

        var payload = new OpenAiChatRequest
        {
            Model = _ragOptions.ChatModel,
            Messages =
            [
                new OpenAiChatMessage
                {
                    Role = "system",
                    Content =
                        """
                        Eres un clasificador de intención de tickets de soporte Telematel.
                        Responde ÚNICAMENTE con JSON válido sin markdown:
                        {"intentCode":"codigo","reason":"motivo breve en español","confidence":0.0-1.0}
                        El intentCode debe ser uno de los códigos del catálogo.
                        Si no encaja claramente en howto ni en error, usa otro.
                        """
                },
                new OpenAiChatMessage { Role = "user", Content = userPrompt }
            ],
            Temperature = 0.1
        };

        var client = _httpClientFactory.CreateClient(OpenAiEmbeddingService.HttpClientName);
        using var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"OpenAI respondió {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("OpenAI devolvió una respuesta vacía.");

        var content = result.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI no devolvió contenido en la respuesta.");
        }

        var json = ExtractJsonObject(content);
        var parsed = JsonSerializer.Deserialize<IntentGptResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("No se pudo interpretar la respuesta JSON de intención.");

        var code = (parsed.IntentCode ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("OpenAI no devolvió intentCode.");
        }

        return new IntentClassification(
            code,
            string.IsNullOrWhiteSpace(parsed.Reason) ? null : parsed.Reason.Trim(),
            parsed.Confidence);
    }

    private static TicketIntent? ResolveIntent(string? code, IReadOnlyList<TicketIntent> intents)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToLowerInvariant();
        return intents.FirstOrDefault(i =>
            string.Equals(i.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return content;
        }

        return content[start..(end + 1)];
    }

    private sealed record IntentClassification(string IntentCode, string? Reason, double? Confidence);

    private sealed class IntentGptResponse
    {
        public string? IntentCode { get; set; }
        public string? Reason { get; set; }
        public double? Confidence { get; set; }
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
