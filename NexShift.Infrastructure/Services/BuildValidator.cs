using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;

namespace NexShift.Infrastructure.Services;

public class BuildValidator : IBuildValidator
{
    private readonly ILogger<BuildValidator> _logger;

    // Parsea líneas como:
    //   Controllers/HomeController.cs(45,12): error CS0246: ...
    //   src\Services\MyService.cs(12,5): warning CS8618: ...
    private static readonly Regex DiagnosticRegex = new(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s+(?<code>\w+):\s*(?<msg>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public BuildValidator(ILogger<BuildValidator> logger)
    {
        _logger = logger;
    }

    public async Task<BuildResult> ValidateAsync(
        Dictionary<string, string> files,
        string targetFramework,
        CancellationToken cancellationToken = default)
    {
        var result = new BuildResult { WasExecuted = false };
        var buildDir = Path.Combine(Path.GetTempPath(), "nexshift-build", Guid.NewGuid().ToString());

        try
        {
            // ── 1. Escribir archivos transformados al disco ────────────────────
            _logger.LogInformation("Build validation: escribiendo {Count} archivos en {Dir}", files.Count, buildDir);

            foreach (var (relativePath, content) in files)
            {
                // Solo incluir archivos de texto útiles para el build
                if (IsBinaryExtension(relativePath)) continue;

                var fullPath = Path.Combine(buildDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken);
            }

            // ── 2. Encontrar el .csproj raíz ─────────────────────────────────
            var csprojFiles = Directory.GetFiles(buildDir, "*.csproj", SearchOption.AllDirectories);
            if (!csprojFiles.Any())
            {
                result.WasExecuted = false;
                result.ExecutionError = "No se encontró ningún .csproj en los archivos migrados";
                return result;
            }

            // Si hay múltiples, usar el que está más cerca de la raíz
            var targetCsproj = csprojFiles
                .OrderBy(f => f.Split(Path.DirectorySeparatorChar).Length)
                .First();
            var projectDir = Path.GetDirectoryName(targetCsproj)!;

            result.WasExecuted = true;
            var stopwatch = Stopwatch.StartNew();

            // ── 3. dotnet restore ─────────────────────────────────────────────
            _logger.LogInformation("Build validation: dotnet restore en {Dir}", projectDir);
            var restoreOutput = await RunDotnetAsync("restore", projectDir, cancellationToken);

            if (restoreOutput.ExitCode != 0)
            {
                _logger.LogWarning("dotnet restore falló — continuando con build de todas formas");
            }

            // ── 4. dotnet build ───────────────────────────────────────────────
            _logger.LogInformation("Build validation: dotnet build en {Dir}", projectDir);
            var buildOutput = await RunDotnetAsync(
                $"build --no-restore -c Release",
                projectDir,
                cancellationToken);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            var fullOutput = buildOutput.StdOut + "\n" + buildOutput.StdErr;
            result.RawOutput = fullOutput.Length > 5000
                ? fullOutput[..5000] + "\n...(truncado)"
                : fullOutput;

            // ── 5. Parsear diagnósticos ───────────────────────────────────────
            var diagnostics = ParseDiagnostics(fullOutput, buildDir);
            result.Errors = diagnostics.Where(d => d.Severity == "error").ToList();
            result.Warnings = diagnostics.Where(d => d.Severity == "warning").ToList();
            result.ErrorCount = result.Errors.Count;
            result.WarningCount = result.Warnings.Count;
            result.Success = buildOutput.ExitCode == 0;

            _logger.LogInformation(
                "Build validation completado: {Status} | {Errors} errores | {Warnings} warnings | {Duration:F1}s",
                result.Success ? "EXITOSO" : "FALLÓ",
                result.ErrorCount, result.WarningCount, result.Duration.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando build validation");
            result.WasExecuted = false;
            result.ExecutionError = ex.Message;
        }
        finally
        {
            // Limpiar directorio temporal
            try
            {
                if (Directory.Exists(buildDir))
                    Directory.Delete(buildDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo limpiar directorio temporal {Dir}", buildDir);
            }
        }

        return result;
    }

    private static async Task<(string StdOut, string StdErr, int ExitCode)> RunDotnetAsync(
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Timeout de 3 minutos para el build
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(3));

        await process.WaitForExitAsync(cts.Token);

        return (stdout.ToString(), stderr.ToString(), process.ExitCode);
    }

    private static List<BuildDiagnostic> ParseDiagnostics(string output, string buildDir)
    {
        var diagnostics = new List<BuildDiagnostic>();

        foreach (Match match in DiagnosticRegex.Matches(output))
        {
            // Normalizar el path para que sea relativo
            var rawPath = match.Groups["file"].Value.Trim();
            var relativePath = rawPath.StartsWith(buildDir, StringComparison.OrdinalIgnoreCase)
                ? rawPath[buildDir.Length..].TrimStart(Path.DirectorySeparatorChar, '/')
                : rawPath;

            diagnostics.Add(new BuildDiagnostic
            {
                FilePath = relativePath.Replace('\\', '/'),
                Line = int.TryParse(match.Groups["line"].Value, out var l) ? l : 0,
                Column = int.TryParse(match.Groups["col"].Value, out var c) ? c : 0,
                Code = match.Groups["code"].Value,
                Message = match.Groups["msg"].Value.Trim(),
                Severity = match.Groups["severity"].Value.ToLower()
            });
        }

        return diagnostics;
    }

    private static bool IsBinaryExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".svg" or
                      ".pdf" or ".zip" or ".dll" or ".exe" or ".pdb" or
                      ".woff" or ".woff2" or ".ttf" or ".eot" or ".otf" or
                      ".mp3" or ".mp4" or ".mov" or ".avi";
    }

}
