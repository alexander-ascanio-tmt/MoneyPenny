using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models.Rag;
using MoneyPenny.Options;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Prompts;

public sealed class RagPromptResolver : IRagPromptResolver
{
    public const string DefaultTemplateCode = "default";
    public const string AgentTemplateCode = "agent";
    public const string UrgentProfileCode = "urgent";
    public const string NormalProfileCode = "normal";

    private readonly VectorDbContext _vectorDb;
    private readonly RagOptions _ragOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RagPromptResolver> _logger;

    public RagPromptResolver(
        VectorDbContext vectorDb,
        IOptions<RagOptions> ragOptions,
        IWebHostEnvironment environment,
        ILogger<RagPromptResolver> logger)
    {
        _vectorDb = vectorDb;
        _ragOptions = ragOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<RagResolvedPrompt> ResolveAsync(
        RagPromptResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var isUrgent = request.IsUrgent;
        var intentCode = NormalizeCode(request.IntentCode);

        if (request.TicketId is > 0)
        {
            if (isUrgent is null)
            {
                isUrgent = await _vectorDb.TicketUrgencies
                    .AsNoTracking()
                    .Where(u => u.TicketId == request.TicketId)
                    .Select(u => (bool?)u.IsUrgent)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (intentCode is null)
            {
                intentCode = await (
                    from assignment in _vectorDb.TicketIntentAssignments.AsNoTracking()
                    join intent in _vectorDb.TicketIntents.AsNoTracking() on assignment.IntentId equals intent.Id
                    where assignment.TicketId == request.TicketId
                    orderby assignment.UpdatedAt ?? assignment.CreatedAt descending
                    select intent.Code
                ).FirstOrDefaultAsync(cancellationToken);
                intentCode = NormalizeCode(intentCode);
            }
        }

        var template = await SelectTemplateAsync(isUrgent, intentCode, cancellationToken)
            ?? await GetDefaultTemplateAsync(cancellationToken);

        if (template is null)
        {
            _logger.LogWarning("No hay plantilla RAG en BD; usando archivos de Prompts/ como fallback.");
            return await BuildFallbackFromFilesAsync(isUrgent, intentCode, cancellationToken);
        }

        var urgencyProfileCode = isUrgent == true ? UrgentProfileCode : NormalProfileCode;
        var urgencyInstructions = await _vectorDb.UrgencyProfiles
            .AsNoTracking()
            .Where(p => p.Code == urgencyProfileCode)
            .Select(p => p.ResponseInstructions)
            .FirstOrDefaultAsync(cancellationToken);

        var systemPrompt = BuildSystemPrompt(
            template.SystemPrompt,
            urgencyInstructions,
            isUrgent);

        var promptVersion = BuildPromptVersion(template.Code, isUrgent, intentCode);

        return new RagResolvedPrompt
        {
            SystemPrompt = systemPrompt,
            UserPromptTemplate = template.UserPromptTemplate,
            GenerationQuestion = string.IsNullOrWhiteSpace(template.GenerationQuestion)
                ? RagOrchestrator.DefaultGenerationQuestion
                : template.GenerationQuestion.Trim(),
            PromptVersion = promptVersion,
            TemplateCode = template.Code
        };
    }

    private async Task<RagPromptTemplate?> SelectTemplateAsync(
        bool? isUrgent,
        string? intentCode,
        CancellationToken cancellationToken)
    {
        var rules = await _vectorDb.RagPromptSelectionRules
            .AsNoTracking()
            .Include(r => r.PromptTemplate)
            .Where(r => r.IsActive && r.PromptTemplate.IsActive)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            if (rule.IsUrgent.HasValue && rule.IsUrgent != isUrgent)
            {
                continue;
            }

            var ruleIntent = NormalizeCode(rule.IntentCode);
            if (ruleIntent is not null
                && !string.Equals(ruleIntent, intentCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return rule.PromptTemplate;
        }

        return null;
    }

    private async Task<RagPromptTemplate?> GetDefaultTemplateAsync(CancellationToken cancellationToken)
    {
        return await _vectorDb.RagPromptTemplates
            .AsNoTracking()
            .Where(t => t.IsActive && t.Code == DefaultTemplateCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<RagResolvedPrompt> BuildFallbackFromFilesAsync(
        bool? isUrgent,
        string? intentCode,
        CancellationToken cancellationToken)
    {
        var systemPrompt = await ReadPromptFileAsync(_ragOptions.SystemPromptFile, cancellationToken);
        var userTemplate = await ReadPromptFileAsync(_ragOptions.TicketQaPromptFile, cancellationToken);

        return new RagResolvedPrompt
        {
            SystemPrompt = systemPrompt,
            UserPromptTemplate = userTemplate,
            GenerationQuestion = RagOrchestrator.DefaultGenerationQuestion,
            PromptVersion = BuildPromptVersion("file-fallback", isUrgent, intentCode),
            TemplateCode = "file-fallback"
        };
    }

    private async Task<string> ReadPromptFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_environment.ContentRootPath, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"No se encontró el archivo de prompt: {fullPath}");
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    private static string BuildSystemPrompt(
        string baseSystemPrompt,
        string? urgencyInstructions,
        bool? isUrgent)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseSystemPrompt))
        {
            parts.Add(baseSystemPrompt.Trim());
        }

        if (!string.IsNullOrWhiteSpace(urgencyInstructions))
        {
            var label = isUrgent == true ? "Urgencia: URGENTE" : "Urgencia: NORMAL";
            parts.Add($"{label}\n{urgencyInstructions.Trim()}");
        }

        return string.Join("\n\n", parts);
    }

    private static string BuildPromptVersion(string templateCode, bool? isUrgent, string? intentCode)
    {
        var urgency = isUrgent switch
        {
            true => UrgentProfileCode,
            false => NormalProfileCode,
            _ => "unknown-urgency"
        };
        var intent = string.IsNullOrWhiteSpace(intentCode) ? "no-intent" : intentCode.Trim().ToLowerInvariant();
        return $"{templateCode}+{urgency}+{intent}";
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToLowerInvariant();
}
