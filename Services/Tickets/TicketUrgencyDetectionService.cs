using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Helpers;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Tickets;

public sealed class TicketUrgencyDetectionService : ITicketUrgencyDetectionService
{
    public const string RulesFallbackVersion = "rules-v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly VectorDbContext _vectorDb;
    private readonly ITicketUrgencyService _urgencyService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RagOptions _ragOptions;
    private readonly ILogger<TicketUrgencyDetectionService> _logger;

    public TicketUrgencyDetectionService(
        VectorDbContext vectorDb,
        ITicketUrgencyService urgencyService,
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> ragOptions,
        ILogger<TicketUrgencyDetectionService> logger)
    {
        _vectorDb = vectorDb;
        _urgencyService = urgencyService;
        _httpClientFactory = httpClientFactory;
        _ragOptions = ragOptions.Value;
        _logger = logger;
    }

    public async Task<TicketUrgencyDetectionResult> DetectAndSaveAsync(
        int ticketId,
        string? ticketNumber,
        string? firstCommentHtml,
        string? messageBoxDetail,
        CancellationToken cancellationToken = default)
    {
        var plainComment = TicketHtmlHelper.ToPlainText(firstCommentHtml);
        if (string.IsNullOrWhiteSpace(plainComment))
        {
            var emptyResult = new TicketUrgencyDetectionResult
            {
                IsUrgent = false,
                Reason = null,
                Confidence = null
            };
            await _urgencyService.SaveAsync(
                ticketId,
                ticketNumber,
                false,
                classifierVersion: RulesFallbackVersion,
                cancellationToken: cancellationToken);
            return emptyResult;
        }

        var rules = await _vectorDb.UrgencyRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Phrase)
            .ToListAsync(cancellationToken);

        var config = await _vectorDb.UrgencyDetectionConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);

        var matchedRules = MatchRules(plainComment, rules);
        TicketUrgencyClassification? classification = null;
        string classifierVersion;

        try
        {
            classification = await ClassifyWithGptAsync(
                plainComment,
                messageBoxDetail,
                config?.PromptText,
                rules,
                matchedRules,
                cancellationToken);
            classifierVersion = $"gpt-{_ragOptions.ChatModel}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clasificación GPT de urgencia falló para ticket {TicketId}.", ticketId);
            classification = ClassifyWithRulesOnly(plainComment, matchedRules);
            classifierVersion = RulesFallbackVersion;
        }

        await _urgencyService.SaveAsync(
            ticketId,
            ticketNumber,
            classification.IsUrgent,
            classification.Reason,
            classification.Confidence,
            classifierVersion,
            cancellationToken);

        return new TicketUrgencyDetectionResult
        {
            IsUrgent = classification.IsUrgent,
            Reason = classification.Reason,
            Confidence = classification.Confidence
        };
    }

    public async Task<TicketUrgencyDetectionResult> DetectFromPhrasesAndSaveAsync(
        int ticketId,
        string? ticketNumber,
        string filteredCommentText,
        CancellationToken cancellationToken = default)
    {
        var plainComment = filteredCommentText.Trim();
        if (string.IsNullOrWhiteSpace(plainComment))
        {
            var emptyResult = new TicketUrgencyDetectionResult
            {
                IsUrgent = false,
                Reason = null,
                Confidence = null
            };
            await _urgencyService.SaveAsync(
                ticketId,
                ticketNumber,
                false,
                classifierVersion: RulesFallbackVersion,
                cancellationToken: cancellationToken);
            return emptyResult;
        }

        var rules = await _vectorDb.UrgencyRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Phrase)
            .ToListAsync(cancellationToken);

        var matchedRules = MatchRules(plainComment, rules);
        var classification = ClassifyWithRulesOnly(plainComment, matchedRules);

        await _urgencyService.SaveAsync(
            ticketId,
            ticketNumber,
            classification.IsUrgent,
            classification.Reason,
            classification.Confidence,
            RulesFallbackVersion,
            cancellationToken);

        return new TicketUrgencyDetectionResult
        {
            IsUrgent = classification.IsUrgent,
            Reason = classification.Reason,
            Confidence = classification.Confidence
        };
    }

    private static List<string> MatchRules(string plainComment, IReadOnlyList<Models.Rag.UrgencyRule> rules)
    {
        var matches = new List<string>();
        foreach (var rule in rules)
        {
            if (plainComment.Contains(rule.Phrase, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(rule.Phrase);
            }
        }

        return matches;
    }

    private static TicketUrgencyClassification ClassifyWithRulesOnly(
        string plainComment,
        IReadOnlyList<string> matchedRules)
    {
        if (matchedRules.Count == 0)
        {
            return new TicketUrgencyClassification(false, null, null);
        }

        return new TicketUrgencyClassification(
            true,
            $"Coincide con frase(s) de urgencia: {string.Join(", ", matchedRules)}.",
            0.6);
    }

    private async Task<TicketUrgencyClassification> ClassifyWithGptAsync(
        string plainComment,
        string? messageBoxDetail,
        string? businessPrompt,
        IReadOnlyList<Models.Rag.UrgencyRule> rules,
        IReadOnlyList<string> matchedRules,
        CancellationToken cancellationToken)
    {
        var rulesList = rules.Count == 0
            ? "(Sin frases configuradas)"
            : string.Join("\n", rules.Select(r => $"- {r.Phrase}"));

        var matchedList = matchedRules.Count == 0
            ? "(Ninguna)"
            : string.Join(", ", matchedRules);

        var userPrompt = $"""
            Criterios de negocio:
            {(string.IsNullOrWhiteSpace(businessPrompt) ? "(Sin criterios adicionales)" : businessPrompt.Trim())}

            Frases de referencia (pueden coincidir parcialmente):
            {rulesList}

            Frases que coinciden en este comentario:
            {matchedList}

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
                        Eres un clasificador de urgencia de tickets de soporte Telematel.
                        Responde ÚNICAMENTE con JSON válido sin markdown:
                        {"isUrgent":true|false,"reason":"motivo breve en español","confidence":0.0-1.0}
                        URGENTE = bloqueo operativo, no puede trabajar/facturar, datos críticos o parada de producción.
                        NORMAL = consulta, mejora o error sin bloqueo inmediato.
                        Si hay duda, isUrgent debe ser false.
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
        var parsed = JsonSerializer.Deserialize<UrgencyGptResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("No se pudo interpretar la respuesta JSON de urgencia.");

        return new TicketUrgencyClassification(
            parsed.IsUrgent,
            string.IsNullOrWhiteSpace(parsed.Reason) ? null : parsed.Reason.Trim(),
            parsed.Confidence);
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

    private sealed record TicketUrgencyClassification(bool IsUrgent, string? Reason, double? Confidence);

    private sealed class UrgencyGptResponse
    {
        public bool IsUrgent { get; set; }
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
