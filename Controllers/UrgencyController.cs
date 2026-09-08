using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models.Rag;

namespace MoneyPenny.Controllers;

[Authorize(Roles = "Admin")]
public class UrgencyController : Controller
{
    private readonly VectorDbContext _context;

    public UrgencyController(VectorDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var rules = await _context.UrgencyRules
            .AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Phrase)
            .ToListAsync(cancellationToken);

        return View(rules);
    }

    public IActionResult Create()
    {
        return View(new UrgencyRule
        {
            IsActive = true,
            SortOrder = 100
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UrgencyRule rule, CancellationToken cancellationToken)
    {
        Normalize(rule);

        if (!ModelState.IsValid)
        {
            return View(rule);
        }

        rule.CreatedAt = DateTime.UtcNow;
        _context.UrgencyRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Frase «{rule.Phrase}» creada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var rule = await _context.UrgencyRules.FindAsync([id.Value], cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        return View(rule);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UrgencyRule rule, CancellationToken cancellationToken)
    {
        if (id != rule.Id)
        {
            return NotFound();
        }

        Normalize(rule);

        if (!ModelState.IsValid)
        {
            return View(rule);
        }

        var existing = await _context.UrgencyRules.FindAsync([id], cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Phrase = rule.Phrase;
        existing.SortOrder = rule.SortOrder;
        existing.IsActive = rule.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Frase «{existing.Phrase}» actualizada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var rule = await _context.UrgencyRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        return View(rule);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var rule = await _context.UrgencyRules.FindAsync([id], cancellationToken);
        if (rule is null)
        {
            return RedirectToAction(nameof(Index));
        }

        _context.UrgencyRules.Remove(rule);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Frase «{rule.Phrase}» eliminada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Config(CancellationToken cancellationToken)
    {
        var config = await EnsureConfigAsync(cancellationToken);
        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Config(UrgencyDetectionConfig model, CancellationToken cancellationToken)
    {
        var config = await EnsureConfigAsync(cancellationToken);
        config.PromptText = string.IsNullOrWhiteSpace(model.PromptText)
            ? string.Empty
            : model.PromptText.Trim();
        config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Instrucciones de clasificación actualizadas.";
        return RedirectToAction(nameof(Config));
    }

    public async Task<IActionResult> Profiles(CancellationToken cancellationToken)
    {
        var profiles = await EnsureProfilesAsync(cancellationToken);
        return View(profiles);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profiles(IFormCollection form, CancellationToken cancellationToken)
    {
        var profiles = await EnsureProfilesAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var profile in profiles)
        {
            var key = $"instructions_{profile.Id}";
            profile.ResponseInstructions = form[key].ToString() ?? string.Empty;
            profile.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Instrucciones de respuesta por urgencia actualizadas.";
        return RedirectToAction(nameof(Profiles));
    }

    private async Task<List<UrgencyProfile>> EnsureProfilesAsync(CancellationToken cancellationToken)
    {
        var profiles = await _context.UrgencyProfiles
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        if (profiles.Count >= 2)
        {
            return profiles;
        }

        var seededAt = DateTime.UtcNow;
        if (!profiles.Any(p => p.Code == "urgent"))
        {
            _context.UrgencyProfiles.Add(new UrgencyProfile
            {
                Code = "urgent",
                Name = "Urgente",
                ResponseInstructions = string.Empty,
                UpdatedAt = seededAt
            });
        }

        if (!profiles.Any(p => p.Code == "normal"))
        {
            _context.UrgencyProfiles.Add(new UrgencyProfile
            {
                Code = "normal",
                Name = "Normal",
                ResponseInstructions = string.Empty,
                UpdatedAt = seededAt
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await _context.UrgencyProfiles.OrderBy(p => p.Id).ToListAsync(cancellationToken);
    }

    private async Task<UrgencyDetectionConfig> EnsureConfigAsync(CancellationToken cancellationToken)
    {
        var config = await _context.UrgencyDetectionConfigs.FindAsync([1], cancellationToken);
        if (config is not null)
        {
            return config;
        }

        config = new UrgencyDetectionConfig
        {
            Id = 1,
            PromptText = string.Empty,
            UpdatedAt = DateTime.UtcNow
        };
        _context.UrgencyDetectionConfigs.Add(config);
        await _context.SaveChangesAsync(cancellationToken);
        return config;
    }

    private static void Normalize(UrgencyRule rule)
    {
        rule.Phrase = (rule.Phrase ?? string.Empty).Trim();
    }
}
