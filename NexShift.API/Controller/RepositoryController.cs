using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryController : ControllerBase
{
    private readonly IGitHubService _gitHub;
    private readonly IProjectAnalyzer _analyzer;
    private readonly ILogger<RepositoryController> _logger;
    private readonly NexShiftDbContext _db;
    private readonly IMigrationService _migrationService;
    private readonly IPdfReportService _pdfReportService;
    private readonly IMigrationQueue _migrationQueue;

    public RepositoryController(
        IGitHubService cloner,
        IProjectAnalyzer analyzer,
        ILogger<RepositoryController> logger,
        NexShiftDbContext db,
        IMigrationService migrationService,
        IPdfReportService pdfReportService,
        IMigrationQueue migrationQueue)
    {
        _gitHub = cloner;
        _analyzer = analyzer;
        _logger = logger;
        _db = db;
        _migrationService = migrationService;
        _pdfReportService = pdfReportService;
        _migrationQueue = migrationQueue;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "La URL del repo es requerida" });

        if (!request.Url.StartsWith("https://github.com/"))
            return BadRequest(new { error = "Por ahora solo soportamos repos de GitHub" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        try
        {
            var analysis = await _analyzer.AnalyzeAsync(request.Url);

            var repoName = request.Url.Split('/').TakeLast(2).First() + "/" + request.Url.Split('/').Last();

            var repository = new NexShift.Core.Entities.Repository
            {
                Id = Guid.NewGuid(),
                Url = request.Url,
                Name = repoName,
                DetectedFramework = analysis.DetectedFramework,
                MigrationScore = analysis.MigrationScore,
                IsLegacy = analysis.IsLegacy,
                TotalCsFiles = analysis.CsFiles.Count,
                TotalProjects = analysis.CsprojFiles.Count,
                TotalPackages = analysis.Packages.Count,
                DeprecatedPackages = analysis.Packages.Count(p => p.IsDeprecated),
                AnalysisResultJson = System.Text.Json.JsonSerializer.Serialize(analysis.Packages),
                Status = NexShift.Core.Entities.RepositoryStatus.Analyzed,
                IpAddress = ip
            };

            _db.Repositories.Add(repository);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Análisis guardado. RepoId: {Id}, Score: {Score}", repository.Id, repository.MigrationScore);

            return Ok(new
            {
                id = repository.Id,
                url = request.Url,
                detectedFramework = analysis.DetectedFramework,
                isLegacy = analysis.IsLegacy,
                migrationScore = analysis.MigrationScore,
                projectType = analysis.ProjectType.ToString(),
                scoreBreakdown = analysis.ScoreBreakdown.Select(f => new
                {
                    factor = f.Factor,
                    impact = f.Impact,
                    details = f.Details
                }),
                findings = new
                {
                    filesScanned = analysis.Findings.TotalFilesScanned,
                    systemWeb = analysis.Findings.FilesWithSystemWeb,
                    wcf = analysis.Findings.FilesWithWcf,
                    webForms = analysis.Findings.FilesWithWebForms,
                    binaryFormatter = analysis.Findings.FilesWithBinaryFormatter,
                    appDomain = analysis.Findings.FilesWithAppDomain,
                    windowsOnlyApis = analysis.Findings.FilesWithWindowsOnlyApis,
                    legacyAuth = analysis.Findings.FilesWithLegacyAuth,
                    httpContextCurrent = analysis.Findings.FilesWithHttpContextCurrent,
                    configurationManager = analysis.Findings.FilesWithConfigurationManager,
                    threadAbort = analysis.Findings.FilesWithThreadAbort,
                    hasAspxFiles = analysis.Findings.HasAspxFiles,
                    hasSvcFiles = analysis.Findings.HasSvcFiles
                },
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

    /// <summary>
    /// Dry-run: devuelve el plan completo de migración sin modificar nada.
    /// Incluye tipo de proyecto, cambios planificados con niveles de confianza y roadmap.
    /// </summary>
    [HttpPost("{id}/plan")]
    public async Task<IActionResult> Plan(Guid id, [FromBody] MigrateRequest request)
    {
        var repository = await _db.Repositories.FindAsync(id);
        if (repository == null)
            return NotFound(new { error = "Repo no encontrado — analizalo primero con /analyze" });

        try
        {
            var plan = await _migrationService.PlanAsync(
                repository.Url,
                request?.TargetFramework ?? "net8.0");

            return Ok(new
            {
                repositoryId = id,
                repoUrl = repository.Url,
                targetFramework = plan.TargetFramework,
                projectType = plan.ProjectType.ToString(),
                detectedFramework = plan.DetectedFramework,
                estimates = new
                {
                    automationPercentage = plan.EstimatedAutomationPercentage,
                    automatedChanges = plan.EstimatedAutomatedChanges,
                    manualItems = plan.EstimatedManualItems,
                    reviewItems = plan.EstimatedReviewItems,
                    totalFilesScanned = plan.TotalFilesScanned,
                    filesWithChanges = plan.TotalFilesWithChanges,
                    filesClean = plan.TotalFilesClean
                },
                plannedChanges = plan.PlannedChanges.Select(c => new
                {
                    filePath = c.FilePath,
                    description = c.Description,
                    confidence = c.Confidence.ToString(),    // "Safe" | "ReviewRequired" | "HighRisk"
                    type = c.TransformationType              // "Framework" | "Config" | "PureRegex" | "AI"
                }),
                backlogItems = plan.BacklogItems.Select(b => new
                {
                    filePath = b.FilePath,
                    category = b.Category.ToString(),
                    title = b.Title,
                    description = b.Description,
                    reason = b.Reason
                }),
                roadmap = plan.Roadmap.Select(s => new
                {
                    order = s.Order,
                    title = s.Title,
                    description = s.Description,
                    risk = s.Risk,
                    isAutomatable = s.IsAutomatable,
                    estimatedFilesAffected = s.EstimatedFilesAffected
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando plan para repo {Id}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Encola la migración en background. Devuelve 202 Accepted con jobId inmediatamente.
    /// Hacer polling sobre GET /api/jobs/{jobId} para conocer el progreso.
    /// </summary>
    [HttpPost("{id}/migrate")]
    public async Task<IActionResult> Migrate(Guid id, [FromBody] MigrateRequest request)
    {
        var repository = await _db.Repositories.FindAsync(id);
        if (repository == null)
            return NotFound(new { error = "Repo no encontrado — analizalo primero con /analyze" });

        if (repository.Status == RepositoryStatus.Migrating)
            return Conflict(new { error = "Ya hay una migración en curso para este repositorio" });

        try
        {
            var targetFramework = request?.TargetFramework ?? "net8.0";

            // Crear el job en BD
            var job = new MigrationJob
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                TargetFramework = targetFramework,
                Status = JobStatus.Queued,
                Progress = 0,
                CreatedAt = DateTime.UtcNow
            };

            _db.MigrationJobs.Add(job);
            repository.Status = RepositoryStatus.Migrating;
            await _db.SaveChangesAsync();

            // Encolar para procesamiento async
            _migrationQueue.Enqueue(new MigrationJobRequest(
                job.Id,
                repository.Id,
                repository.Url,
                targetFramework));

            _logger.LogInformation(
                "Job {JobId} encolado para repo {RepoId} → {Framework}",
                job.Id, repository.Id, targetFramework);

            return Accepted(new
            {
                jobId = job.Id,
                status = "queued",
                message = "Migración encolada. Hacer polling sobre /api/jobs/{jobId} para ver el progreso.",
                pollingUrl = $"/api/jobs/{job.Id}",
                downloadUrl = $"/api/jobs/{job.Id}/download"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encolando migración para repo {Id}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/report")]
    public async Task<IActionResult> GetReport(Guid id)
    {
        var repository = await _db.Repositories.FindAsync(id);
        if (repository == null)
            return NotFound(new { error = "Repositorio no encontrado" });

        if (repository.AnalysisResultJson == null)
            return BadRequest(new { error = "El repositorio no tiene análisis. Ejecutá /analyze primero." });

        var packages = System.Text.Json.JsonSerializer.Deserialize<List<PackageInfo>>(
            repository.AnalysisResultJson) ?? new List<PackageInfo>();

        var analysis = new ProjectAnalysisResult
        {
            DetectedFramework = repository.DetectedFramework ?? "unknown",
            MigrationScore = repository.MigrationScore,
            IsLegacy = repository.IsLegacy,
            Packages = packages,
            CsFiles = Enumerable.Range(0, repository.TotalCsFiles).Select(i => $"file{i}.cs").ToList(),
            CsprojFiles = Enumerable.Range(0, repository.TotalProjects).Select(i => $"project{i}.csproj").ToList(),
        };

        var pdf = await _pdfReportService.GenerateDebtReport(analysis, repository.Url);
        return File(pdf, "application/pdf", $"nexshift-report-{id}.pdf");
    }
}

public record AnalyzeRequest(string Url);
public record MigrateRequest(string? TargetFramework);
