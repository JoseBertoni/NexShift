using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class BacklogRulesController : ControllerBase
{
    private readonly IBacklogRuleRepository _repository;
    private readonly ILogger<BacklogRulesController> _logger;

    public BacklogRulesController(
        IBacklogRuleRepository repository,
        ILogger<BacklogRulesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // GET /api/backlogrules
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var rules = await _repository.GetAllActiveAsync(cancellationToken);
        return Ok(rules);
    }

    // GET /api/backlogrules/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var rule = await _repository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
            return NotFound(new { error = $"Regla {id} no encontrada" });

        return Ok(rule);
    }

    // POST /api/backlogrules
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBacklogRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.Category != "ManualRequired" && request.Category != "NeedsReview")
            return BadRequest(new { error = "Category debe ser 'ManualRequired' o 'NeedsReview'" });

        var rule = new BacklogRule
        {
            Pattern = request.Pattern.Trim(),
            Category = request.Category,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Reason = request.Reason.Trim(),
            IsActive = true
        };

        var created = await _repository.CreateAsync(rule, cancellationToken);

        _logger.LogInformation("Nueva BacklogRule creada por admin: {Pattern}", rule.Pattern);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/backlogrules/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateBacklogRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.Category != "ManualRequired" && request.Category != "NeedsReview")
            return BadRequest(new { error = "Category debe ser 'ManualRequired' o 'NeedsReview'" });

        try
        {
            var rule = new BacklogRule
            {
                Id = id,
                Pattern = request.Pattern.Trim(),
                Category = request.Category,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Reason = request.Reason.Trim(),
                IsActive = request.IsActive
            };

            var updated = await _repository.UpdateAsync(rule, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Regla {id} no encontrada" });
        }
    }

    // DELETE /api/backlogrules/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.DeleteAsync(id, cancellationToken);
            _logger.LogInformation("BacklogRule {Id} desactivada por admin", id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Regla {id} no encontrada" });
        }
    }

    // POST /api/backlogrules/cache/invalidate
    [HttpPost("cache/invalidate")]
    public IActionResult InvalidateCache()
    {
        _repository.InvalidateCache();
        _logger.LogInformation("Caché invalidada manualmente por admin");
        return Ok(new { message = "Caché invalidada correctamente" });
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record CreateBacklogRuleRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(200)]
    string Pattern,

    [property: System.ComponentModel.DataAnnotations.Required]
    string Category,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(200)]
    string Title,

    [property: System.ComponentModel.DataAnnotations.Required]
    string Description,

    [property: System.ComponentModel.DataAnnotations.Required]
    string Reason
);

public record UpdateBacklogRuleRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(200)]
    string Pattern,

    [property: System.ComponentModel.DataAnnotations.Required]
    string Category,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(200)]
    string Title,

    [property: System.ComponentModel.DataAnnotations.Required]
    string Description,

    [property: System.ComponentModel.DataAnnotations.Required]
    string Reason,

    bool IsActive
);