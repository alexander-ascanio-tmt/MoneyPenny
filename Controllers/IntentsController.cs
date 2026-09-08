using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using MoneyPenny.Data;

using MoneyPenny.Models.Rag;



namespace MoneyPenny.Controllers;



[Authorize(Roles = "Admin")]

public class IntentsController : Controller

{

    private readonly VectorDbContext _context;



    public IntentsController(VectorDbContext context)

    {

        _context = context;

    }



    public async Task<IActionResult> Index(CancellationToken cancellationToken)

    {

        var intents = await _context.TicketIntents

            .AsNoTracking()

            .OrderBy(i => i.SortOrder)

            .ThenBy(i => i.Name)

            .ToListAsync(cancellationToken);



        return View(intents);

    }



    public IActionResult Create()

    {

        return View(new TicketIntent

        {

            IsActive = true,

            SortOrder = 100

        });

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Create(TicketIntent intent, CancellationToken cancellationToken)

    {

        Normalize(intent);



        if (await _context.TicketIntents.AnyAsync(i => i.Code == intent.Code, cancellationToken))

        {

            ModelState.AddModelError(nameof(intent.Code), "Ya existe una intención con ese código.");

        }



        if (!ModelState.IsValid)

        {

            return View(intent);

        }



        intent.CreatedAt = DateTime.UtcNow;
        _context.TicketIntents.Add(intent);

        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Intención «{intent.Name}» creada.";

        return RedirectToAction(nameof(Index));

    }



    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)

    {

        if (id is null)

        {

            return NotFound();

        }



        var intent = await _context.TicketIntents.FindAsync([id.Value], cancellationToken);

        if (intent is null)

        {

            return NotFound();

        }



        return View(intent);

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Edit(int id, TicketIntent intent, CancellationToken cancellationToken)

    {

        if (id != intent.Id)

        {

            return NotFound();

        }



        Normalize(intent);



        if (await _context.TicketIntents.AnyAsync(

                i => i.Code == intent.Code && i.Id != intent.Id,

                cancellationToken))

        {

            ModelState.AddModelError(nameof(intent.Code), "Ya existe una intención con ese código.");

        }



        if (!ModelState.IsValid)

        {

            return View(intent);

        }



        var existing = await _context.TicketIntents.FindAsync([id], cancellationToken);

        if (existing is null)

        {

            return NotFound();

        }



        existing.Code = intent.Code;

        existing.Name = intent.Name;

        existing.Description = intent.Description;
        existing.SortOrder = intent.SortOrder;

        existing.IsActive = intent.IsActive;

        existing.UpdatedAt = DateTime.UtcNow;



        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Intención «{existing.Name}» actualizada.";

        return RedirectToAction(nameof(Index));

    }



    public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)

    {

        if (id is null)

        {

            return NotFound();

        }



        var intent = await _context.TicketIntents

            .AsNoTracking()

            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (intent is null)

        {

            return NotFound();

        }



        ViewBag.AssignmentCount = await _context.TicketIntentAssignments

            .CountAsync(a => a.IntentId == id, cancellationToken);



        return View(intent);

    }



    [HttpPost, ActionName("Delete")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)

    {

        var intent = await _context.TicketIntents.FindAsync([id], cancellationToken);

        if (intent is null)

        {

            return RedirectToAction(nameof(Index));

        }



        var hasAssignments = await _context.TicketIntentAssignments

            .AnyAsync(a => a.IntentId == id, cancellationToken);

        if (hasAssignments)

        {

            TempData["WarningMessage"] =

                $"No se puede eliminar «{intent.Name}» porque tiene tickets asignados. Desactívala en su lugar.";

            return RedirectToAction(nameof(Index));

        }



        _context.TicketIntents.Remove(intent);

        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Intención «{intent.Name}» eliminada.";

        return RedirectToAction(nameof(Index));

    }



    public async Task<IActionResult> Config(CancellationToken cancellationToken)

    {

        var config = await EnsureConfigAsync(cancellationToken);

        return View(config);

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Config(IntentDetectionConfig model, CancellationToken cancellationToken)

    {

        var config = await EnsureConfigAsync(cancellationToken);

        config.PromptText = string.IsNullOrWhiteSpace(model.PromptText)

            ? string.Empty

            : model.PromptText.Trim();

        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Instrucciones de intención actualizadas.";

        return RedirectToAction(nameof(Config));

    }



    private async Task<IntentDetectionConfig> EnsureConfigAsync(CancellationToken cancellationToken)

    {

        var config = await _context.IntentDetectionConfigs.FindAsync([1], cancellationToken);

        if (config is not null)

        {

            return config;

        }



        config = new IntentDetectionConfig

        {

            Id = 1,

            PromptText = string.Empty,

            UpdatedAt = DateTime.UtcNow

        };

        _context.IntentDetectionConfigs.Add(config);

        await _context.SaveChangesAsync(cancellationToken);

        return config;

    }



    private static void Normalize(TicketIntent intent)

    {

        intent.Code = (intent.Code ?? string.Empty).Trim().ToLowerInvariant();

        intent.Name = (intent.Name ?? string.Empty).Trim();

        intent.Description = string.IsNullOrWhiteSpace(intent.Description)

            ? null

            : intent.Description.Trim();

    }

}

