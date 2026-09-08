using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models.Rag;
using MoneyPenny.Services.Rag;
using MoneyPenny.Services.Rag.Prompts;

namespace MoneyPenny.Controllers;

[Authorize(Roles = "Admin")]
public class RagPromptsController : Controller
{
    private readonly VectorDbContext _context;

    public RagPromptsController(VectorDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var templates = await _context.RagPromptTemplates
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return View(templates);
    }

    public IActionResult Create()
    {
        return View(new RagPromptTemplate
        {
            IsActive = true,
            SortOrder = 100,
            GenerationQuestion = RagOrchestrator.DefaultGenerationQuestion
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RagPromptTemplate template, CancellationToken cancellationToken)
    {
        Normalize(template);

        if (await _context.RagPromptTemplates.AnyAsync(t => t.Code == template.Code, cancellationToken))
        {
            ModelState.AddModelError(nameof(template.Code), "Ya existe una plantilla con ese código.");
        }

        if (!ModelState.IsValid)
        {
            return View(template);
        }

        template.CreatedAt = DateTime.UtcNow;
        _context.RagPromptTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Plantilla «{template.Name}» creada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var template = await _context.RagPromptTemplates.FindAsync([id.Value], cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return View(template);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RagPromptTemplate template, CancellationToken cancellationToken)
    {
        if (id != template.Id)
        {
            return NotFound();
        }

        Normalize(template);

        if (await _context.RagPromptTemplates.AnyAsync(
                t => t.Code == template.Code && t.Id != template.Id,
                cancellationToken))
        {
            ModelState.AddModelError(nameof(template.Code), "Ya existe una plantilla con ese código.");
        }

        if (!ModelState.IsValid)
        {
            return View(template);
        }

        var existing = await _context.RagPromptTemplates.FindAsync([id], cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Code = template.Code;
        existing.Name = template.Name;
        existing.SystemPrompt = template.SystemPrompt;
        existing.UserPromptTemplate = template.UserPromptTemplate;
        existing.GenerationQuestion = template.GenerationQuestion;
        existing.SortOrder = template.SortOrder;
        existing.IsActive = template.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Plantilla «{existing.Name}» actualizada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var template = await _context.RagPromptTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        ViewBag.RuleCount = await _context.RagPromptSelectionRules
            .CountAsync(r => r.PromptTemplateId == id, cancellationToken);

        return View(template);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var template = await _context.RagPromptTemplates.FindAsync([id], cancellationToken);
        if (template is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(template.Code, RagPromptResolver.DefaultTemplateCode, StringComparison.OrdinalIgnoreCase))
        {
            TempData["WarningMessage"] = "No se puede eliminar la plantilla default.";
            return RedirectToAction(nameof(Index));
        }

        _context.RagPromptTemplates.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Plantilla «{template.Name}» eliminada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Rules(CancellationToken cancellationToken)
    {
        var rules = await _context.RagPromptSelectionRules
            .AsNoTracking()
            .Include(r => r.PromptTemplate)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return View(rules);
    }

    public async Task<IActionResult> CreateRule(CancellationToken cancellationToken)
    {
        await PopulateRuleFormAsync(cancellationToken);
        return View(new RagPromptSelectionRule
        {
            IsActive = true,
            Priority = 50
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRule(RagPromptSelectionRule rule, CancellationToken cancellationToken)
    {
        NormalizeRule(rule);

        if (!ModelState.IsValid)
        {
            await PopulateRuleFormAsync(cancellationToken, rule.IntentCode);
            return View(rule);
        }

        rule.CreatedAt = DateTime.UtcNow;
        _context.RagPromptSelectionRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Regla de selección creada.";
        return RedirectToAction(nameof(Rules));
    }

    public async Task<IActionResult> EditRule(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var rule = await _context.RagPromptSelectionRules.FindAsync([id.Value], cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        await PopulateRuleFormAsync(cancellationToken, rule.IntentCode);
        return View(rule);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRule(int id, RagPromptSelectionRule rule, CancellationToken cancellationToken)
    {
        if (id != rule.Id)
        {
            return NotFound();
        }

        NormalizeRule(rule);

        if (!ModelState.IsValid)
        {
            await PopulateRuleFormAsync(cancellationToken, rule.IntentCode);
            return View(rule);
        }

        var existing = await _context.RagPromptSelectionRules.FindAsync([id], cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = rule.Name;
        existing.PromptTemplateId = rule.PromptTemplateId;
        existing.IsUrgent = rule.IsUrgent;
        existing.IntentCode = rule.IntentCode;
        existing.Priority = rule.Priority;
        existing.IsActive = rule.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Regla de selección actualizada.";
        return RedirectToAction(nameof(Rules));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(int id, CancellationToken cancellationToken)
    {
        var rule = await _context.RagPromptSelectionRules.FindAsync([id], cancellationToken);
        if (rule is not null)
        {
            _context.RagPromptSelectionRules.Remove(rule);
            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = "Regla eliminada.";
        }

        return RedirectToAction(nameof(Rules));
    }

    private async Task PopulateRuleFormAsync(
        CancellationToken cancellationToken,
        string? selectedIntentCode = null)
    {
        await PopulateTemplateSelectListAsync(cancellationToken);
        await PopulateIntentCodeSelectListAsync(cancellationToken, selectedIntentCode);
    }

    private async Task PopulateTemplateSelectListAsync(CancellationToken cancellationToken)
    {
        var templates = await _context.RagPromptTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .Select(t => new { t.Id, Label = t.Name + " (" + t.Code + ")" })
            .ToListAsync(cancellationToken);

        ViewBag.PromptTemplateId = new SelectList(templates, "Id", "Label");
    }

    private async Task PopulateIntentCodeSelectListAsync(
        CancellationToken cancellationToken,
        string? selectedIntentCode = null)
    {
        var intents = await _context.TicketIntents
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .Select(i => new { Value = i.Code, Label = i.Name + " (" + i.Code + ")" })
            .ToListAsync(cancellationToken);

        var items = intents
            .Select(i => new SelectListItem(i.Label, i.Value))
            .ToList();

        var normalizedSelected = string.IsNullOrWhiteSpace(selectedIntentCode)
            ? null
            : selectedIntentCode.Trim().ToLowerInvariant();

        if (normalizedSelected is not null
            && !items.Any(i => string.Equals(i.Value, normalizedSelected, StringComparison.OrdinalIgnoreCase)))
        {
            items.Insert(0, new SelectListItem(
                $"{normalizedSelected} (no disponible)",
                normalizedSelected,
                selected: true));
        }

        ViewBag.IntentCode = new SelectList(items, "Value", "Text", normalizedSelected);
    }

    private static void Normalize(RagPromptTemplate template)
    {
        template.Code = (template.Code ?? string.Empty).Trim().ToLowerInvariant();
        template.Name = (template.Name ?? string.Empty).Trim();
        template.SystemPrompt = template.SystemPrompt ?? string.Empty;
        template.UserPromptTemplate = template.UserPromptTemplate ?? string.Empty;
        template.GenerationQuestion = string.IsNullOrWhiteSpace(template.GenerationQuestion)
            ? RagOrchestrator.DefaultGenerationQuestion
            : template.GenerationQuestion.Trim();
    }

    private static void NormalizeRule(RagPromptSelectionRule rule)
    {
        rule.Name = string.IsNullOrWhiteSpace(rule.Name) ? null : rule.Name.Trim();
        rule.IntentCode = string.IsNullOrWhiteSpace(rule.IntentCode)
            ? null
            : rule.IntentCode.Trim().ToLowerInvariant();
    }
}
