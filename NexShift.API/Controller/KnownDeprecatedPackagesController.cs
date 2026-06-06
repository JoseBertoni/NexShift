using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class KnownDeprecatedPackagesController : ControllerBase
{
    private readonly IKnownDeprecatedPackageRepository _repo;

    public KnownDeprecatedPackagesController(IKnownDeprecatedPackageRepository repo)
        => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _repo.GetAllActiveAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateKnownDeprecatedPackageRequest req, CancellationToken ct)
    {
        var package = new KnownDeprecatedPackage
        {
            Name = req.Name,
            Category = req.Category,
            Reason = req.Reason,
            SuggestedReplacement = req.SuggestedReplacement,
            AdvisoryUrl = req.AdvisoryUrl
        };
        var created = await _repo.AddAsync(package, ct);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateKnownDeprecatedPackageRequest req, CancellationToken ct)
    {
        var existing = (await _repo.GetAllActiveAsync(ct)).FirstOrDefault(p => p.Id == id);
        if (existing is null) return NotFound();

        existing.Name = req.Name;
        existing.Category = req.Category;
        existing.Reason = req.Reason;
        existing.SuggestedReplacement = req.SuggestedReplacement;
        existing.AdvisoryUrl = req.AdvisoryUrl;

        await _repo.UpdateAsync(existing, ct);
        return Ok(existing);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _repo.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("cache/invalidate")]
    public async Task<IActionResult> InvalidateCache()
    {
        await _repo.InvalidateCacheAsync();
        return Ok(new { message = "Cache invalidated" });
    }
}

public record CreateKnownDeprecatedPackageRequest(
    [Required] string Name,
    [Required] string Category,          // "Deprecated" | "SecurityVulnerability"
    [Required] string Reason,
    string? SuggestedReplacement,
    string? AdvisoryUrl
);

public record UpdateKnownDeprecatedPackageRequest(
    [Required] string Name,
    [Required] string Category,
    [Required] string Reason,
    string? SuggestedReplacement,
    string? AdvisoryUrl
);