using Microsoft.AspNetCore.Mvc;
using NexShift.Core.Entities;
using NexShift.Infrastructure.Data;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly NexShiftDbContext _db;
    private readonly ILogger<JobsController> _logger;

    public JobsController(NexShiftDbContext db, ILogger<JobsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Consulta el estado de un job de migración.
    /// Hacer polling sobre este endpoint hasta que status sea "completed" o "failed".
    /// </summary>
    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetJob(Guid jobId)
    {
        var job = await _db.MigrationJobs.FindAsync(jobId);
        if (job == null)
            return NotFound(new { error = "Job no encontrado" });

        return Ok(new
        {
            jobId = job.Id,
            repositoryId = job.RepositoryId,
            status = job.Status.ToString().ToLower(),   // "queued" | "running" | "completed" | "failed"
            progress = job.Progress,                    // 0-100
            targetFramework = job.TargetFramework,
            createdAt = job.CreatedAt,
            completedAt = job.CompletedAt,
            errorMessage = job.Status == JobStatus.Failed ? job.ErrorMessage : null,
            stats = job.Status == JobStatus.Completed
                ? new
                {
                    migrationPercentage = job.MigrationPercentage,
                    automatedCount = job.AutomatedCount,
                    manualCount = job.ManualCount,
                    reviewCount = job.ReviewCount
                }
                : null,
            build = job.Status == JobStatus.Completed
                ? new
                {
                    executed = job.BuildSuccess.HasValue,
                    success = job.BuildSuccess,
                    errorCount = job.BuildErrorCount,
                    warningCount = job.BuildWarningCount,
                    // Label legible para el frontend
                    label = !job.BuildSuccess.HasValue  ? "No ejecutado"
                          : job.BuildSuccess.Value      ? $"✅ Compila ({job.BuildWarningCount} warnings)"
                          :                               $"❌ {job.BuildErrorCount} error(es) de compilación"
                }
                : null,
            downloadUrl = job.Status == JobStatus.Completed
                ? $"/api/jobs/{job.Id}/download"
                : null
        });
    }

    /// <summary>
    /// Descarga el ZIP resultante de una migración completada.
    /// Solo disponible cuando status == "completed".
    /// </summary>
    [HttpGet("{jobId}/download")]
    public async Task<IActionResult> Download(Guid jobId)
    {
        var job = await _db.MigrationJobs.FindAsync(jobId);
        if (job == null)
            return NotFound(new { error = "Job no encontrado" });

        if (job.Status != JobStatus.Completed)
            return BadRequest(new
            {
                error = $"El job está en estado '{job.Status.ToString().ToLower()}', todavía no está listo para descargar"
            });

        if (string.IsNullOrEmpty(job.OutputZipPath) || !System.IO.File.Exists(job.OutputZipPath))
        {
            _logger.LogWarning("ZIP no encontrado en disco para job {JobId} — path: {Path}", jobId, job.OutputZipPath);
            return NotFound(new { error = "El archivo ZIP expiró o fue eliminado del servidor" });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(job.OutputZipPath);
        return File(bytes, "application/zip", $"nexshift-{jobId}.zip");
    }
}
