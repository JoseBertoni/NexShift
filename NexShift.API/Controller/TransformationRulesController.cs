using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TransformationRulesController : ControllerBase
{
    private readonly ITransformationRuleRepository _repository;
    private readonly ILogger<TransformationRulesController> _logger;

    public TransformationRulesController(
        ITransformationRuleRepository repository,
        ILogger<TransformationRulesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // GET /api/transformationrules
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var rules = await _repository.GetAllActiveAsync(cancellationToken);
        return Ok(rules);
    }

    // GET /api/transformationrules/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var rule = await _repository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
            return NotFound(new { error = $"Rule {id} not found" });

        return Ok(rule);
    }

    // POST /api/transformationrules
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTransformationRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // NeedsAI=true rules don't need a Replacement value
        if (!request.NeedsAI && string.IsNullOrWhiteSpace(request.Replacement))
            return BadRequest(new { error = "Replacement is required when NeedsAI is false" });

        var rule = new TransformationRule
        {
            Pattern = request.Pattern.Trim(),
            Replacement = request.NeedsAI ? null : request.Replacement?.Trim(),
            NeedsAI = request.NeedsAI,
            IsRegex = request.IsRegex,
            Priority = request.Priority,
            Description = request.Description.Trim(),
            IsActive = true
        };

        var created = await _repository.CreateAsync(rule, cancellationToken);

        _logger.LogInformation("New TransformationRule created by admin: {Pattern}", rule.Pattern);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/transformationrules/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTransformationRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!request.NeedsAI && string.IsNullOrWhiteSpace(request.Replacement))
            return BadRequest(new { error = "Replacement is required when NeedsAI is false" });

        try
        {
            var rule = new TransformationRule
            {
                Id = id,
                Pattern = request.Pattern.Trim(),
                Replacement = request.NeedsAI ? null : request.Replacement?.Trim(),
                NeedsAI = request.NeedsAI,
                IsRegex = request.IsRegex,
                Priority = request.Priority,
                Description = request.Description.Trim(),
                IsActive = request.IsActive
            };

            var updated = await _repository.UpdateAsync(rule, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Rule {id} not found" });
        }
    }

    // DELETE /api/transformationrules/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.DeleteAsync(id, cancellationToken);
            _logger.LogInformation("TransformationRule {Id} deactivated by admin", id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Rule {id} not found" });
        }
    }

    // POST /api/transformationrules/cache/invalidate
    [HttpPost("cache/invalidate")]
    public IActionResult InvalidateCache()
    {
        _repository.InvalidateCache();
        _logger.LogInformation("TransformationRules cache invalidated by admin");
        return Ok(new { message = "Cache invalidated" });
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record CreateTransformationRuleRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(500)]
    string Pattern,

    // Null allowed when NeedsAI = true
    string? Replacement,

    bool NeedsAI,

    bool IsRegex,

    [property: System.ComponentModel.DataAnnotations.Range(1, 1000)]
    int Priority,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(300)]
    string Description
);

public record UpdateTransformationRuleRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(500)]
    string Pattern,

    string? Replacement,

    bool NeedsAI,

    bool IsRegex,

    [property: System.ComponentModel.DataAnnotations.Range(1, 1000)]
    int Priority,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(300)]
    string Description,

    bool IsActive
);