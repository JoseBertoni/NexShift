using Microsoft.AspNetCore.Mvc;
using NexShift.Core.Interfaces;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryController : ControllerBase
{
    private readonly IGitHubService _gitHub;
    private readonly IProjectAnalyzer _analyzer;
    private readonly ILogger<RepositoryController> _logger;

    public RepositoryController(IGitHubService cloner, IProjectAnalyzer analyzer, ILogger<RepositoryController> logger)
    {
        _gitHub = cloner;
        _analyzer = analyzer;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "La URL del repo es requerida" });

        if (!request.Url.StartsWith("https://github.com/"))
            return BadRequest(new { error = "Por ahora solo soportamos repos de GitHub" });

        try
        {
            var analysis = await _analyzer.AnalyzeAsync(request.Url);

            return Ok(new
            {
                url = request.Url,
                detectedFramework = analysis.DetectedFramework,
                isLegacy = analysis.IsLegacy,
                migrationScore = analysis.MigrationScore,
                stats = new
                {
                    totalCsFiles = analysis.CsFiles.Count,
                    totalProjects = analysis.CsprojFiles.Count,
                },
                packages = new
                {
                    total = analysis.Packages.Count,
                    deprecated = analysis.Packages.Count(p => p.IsDeprecated),
                    items = analysis.Packages
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analizando repo {Url}", request.Url);
            return BadRequest(new { error = ex.Message });
        }
    }
}
    public record AnalyzeRequest(string Url);