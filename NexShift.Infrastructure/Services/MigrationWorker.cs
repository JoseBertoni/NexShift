using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;

namespace NexShift.Infrastructure.Services;

public class MigrationWorker : BackgroundService
{
    private readonly IMigrationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MigrationWorker> _logger;

    public MigrationWorker(
        IMigrationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MigrationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MigrationWorker iniciado — esperando jobs");

        while (!stoppingToken.IsCancellationRequested)
        {
            MigrationJobRequest jobRequest;
            try
            {
                jobRequest = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _logger.LogInformation(
                "Procesando job {JobId} — repo: {RepoUrl} → {Framework}",
                jobRequest.JobId, jobRequest.RepoUrl, jobRequest.TargetFramework);

            await ProcessJobAsync(jobRequest, stoppingToken);
        }

        _logger.LogInformation("MigrationWorker detenido");
    }

    private async Task ProcessJobAsync(MigrationJobRequest jobRequest, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexShiftDbContext>();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
        var buildValidator = scope.ServiceProvider.GetRequiredService<IBuildValidator>();

        var job = await db.MigrationJobs.FindAsync(new object[] { jobRequest.JobId }, stoppingToken);
        if (job == null)
        {
            _logger.LogWarning("Job {JobId} no encontrado en BD — descartando", jobRequest.JobId);
            return;
        }

        job.Status = JobStatus.Running;
        job.Progress = 10;
        await db.SaveChangesAsync(stoppingToken);

        try
        {
            // ── 1. Migrar ─────────────────────────────────────────────────────
            var result = await migrationService.MigrateAsync(
                jobRequest.RepoUrl,
                jobRequest.TargetFramework,
                null,
                stoppingToken);

            if (!result.Success || result.ZipBytes == null)
            {
                job.Status = JobStatus.Failed;
                job.Progress = 0;
                job.ErrorMessage = result.ErrorMessage ?? "La migración falló sin mensaje de error";

                var failedRepo = await db.Repositories.FindAsync(new object[] { jobRequest.RepositoryId }, stoppingToken);
                if (failedRepo != null) failedRepo.Status = RepositoryStatus.Failed;

                await db.SaveChangesAsync(stoppingToken);
                _logger.LogWarning("Job {JobId} falló: {Error}", jobRequest.JobId, job.ErrorMessage);
                return;
            }

            // ── 2. Guardar ZIP ────────────────────────────────────────────────
            job.Progress = 75;
            await db.SaveChangesAsync(stoppingToken);

            var tempDir = Path.Combine(Path.GetTempPath(), "nexshift");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, $"{jobRequest.JobId}.zip");
            await File.WriteAllBytesAsync(zipPath, result.ZipBytes, stoppingToken);

            // ── 3. Build validation ───────────────────────────────────────────
            job.Progress = 80;
            await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Job {JobId} — iniciando build validation", jobRequest.JobId);

            // Pasar los archivos transformados (sin el reporte .md) al validador
            var filesToBuild = result.FileDiffs.Keys
                .ToDictionary(
                    k => k,
                    k => result.FileDiffs[k].Transformed);

            // Si no hay FileDiffs (todos eran archivos sin cambios), extraer del ZIP
            // El validador necesita el código transformado, no el binario
            BuildResult buildResult;
            if (filesToBuild.Any())
            {
                buildResult = await buildValidator.ValidateAsync(
                    filesToBuild, jobRequest.TargetFramework, stoppingToken);
            }
            else
            {
                buildResult = new BuildResult
                {
                    WasExecuted = false,
                    ExecutionError = "No hay archivos .cs con cambios para compilar — el proyecto puede no tener código C# legacy"
                };
            }

            // ── 4. Guardar resultados ─────────────────────────────────────────
            job.Status = JobStatus.Completed;
            job.Progress = 100;
            job.OutputZipPath = zipPath;
            job.CompletedAt = DateTime.UtcNow;
            job.MigrationPercentage = result.MigrationPercentage;
            job.AutomatedCount = result.AutomatedCount;
            job.ManualCount = result.ManualCount;
            job.ReviewCount = result.ReviewCount;
            job.BuildResultJson = JsonSerializer.Serialize(buildResult);
            job.BuildSuccess = buildResult.WasExecuted ? buildResult.Success : null;
            job.BuildErrorCount = buildResult.ErrorCount;
            job.BuildWarningCount = buildResult.WarningCount;

            var repo = await db.Repositories.FindAsync(new object[] { jobRequest.RepositoryId }, stoppingToken);
            if (repo != null) repo.Status = RepositoryStatus.Completed;

            await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "Job {JobId} finalizado — Migration: {Mp}% | Build: {BuildStatus} ({Errors} errores, {Warnings} warnings)",
                jobRequest.JobId, result.MigrationPercentage,
                buildResult.WasExecuted ? (buildResult.Success ? "✅ OK" : "❌ FALLÓ") : "⏭ no ejecutado",
                buildResult.ErrorCount, buildResult.WarningCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no manejada en job {JobId}", jobRequest.JobId);

            job.Status = JobStatus.Failed;
            job.Progress = 0;
            job.ErrorMessage = ex.Message;

            try
            {
                var repo = await db.Repositories.FindAsync(new object[] { jobRequest.RepositoryId }, stoppingToken);
                if (repo != null) repo.Status = RepositoryStatus.Failed;
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Error guardando estado de fallo para job {JobId}", jobRequest.JobId);
            }
        }
    }
}
