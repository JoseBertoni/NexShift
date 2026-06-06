using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using System.IO.Compression;
using System.Text;

namespace NexShift.Infrastructure.Services.Migrator;

public class ZipBuilder
{
    public byte[] Build(Dictionary<string, string> files)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return memoryStream.ToArray();
    }

    public byte[] BuildWithReport(Dictionary<string, string> files, string reportMarkdown)
    {
        var allFiles = new Dictionary<string, string>(files)
        {
            ["MIGRATION_REPORT.md"] = reportMarkdown
        };

        return Build(allFiles);
    }

    public static string GenerateReport(
    string repoUrl,
    string targetFramework,
    List<string> changes,
    MigrationResult result)
    {
        var sb = new StringBuilder();

        // ─── Header ───────────────────────────────────────────────────────────
        sb.AppendLine("# NexShift Migration Report");
        sb.AppendLine();
        sb.AppendLine($"**Repository:** {repoUrl}");
        sb.AppendLine($"**Target Framework:** {targetFramework}");
        sb.AppendLine($"**Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        // ─── Migration Score ──────────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Migration Summary");
        sb.AppendLine();
        sb.AppendLine($"| Métrica | Valor |");
        sb.AppendLine($"|---------|-------|");
        sb.AppendLine($"| ✅ Automatizado | {result.MigrationPercentage}% |");
        sb.AppendLine($"| ✅ Cambios aplicados | {result.AutomatedCount} |");
        sb.AppendLine($"| ⚠️  Requieren revisión | {result.ReviewCount} |");
        sb.AppendLine($"| ❌ Requieren decisión manual | {result.ManualCount} |");
        sb.AppendLine($"| 📋 Total backlog items | {result.BacklogItems.Count} |");
        sb.AppendLine();

        // ─── Migration Backlog — Manual Required ──────────────────────────────
        var manualItems = result.BacklogItems
            .Where(b => b.Category == BacklogCategory.ManualRequired)
            .GroupBy(b => b.FilePath)
            .ToList();

        if (manualItems.Any())
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## ❌ Requieren Decisión Manual");
            sb.AppendLine();
            sb.AppendLine("> Estos puntos no pueden automatizarse. Requieren una decisión arquitectural.");
            sb.AppendLine();

            foreach (var fileGroup in manualItems)
            {
                sb.AppendLine($"### `{fileGroup.Key}`");
                foreach (var item in fileGroup)
                {
                    sb.AppendLine($"- **{item.Title}**");
                    sb.AppendLine($"  - {item.Description}");
                    sb.AppendLine($"  - 💡 *{item.Reason}*");
                }
                sb.AppendLine();
            }
        }

        // ─── Migration Backlog — Needs Review ────────────────────────────────
        var reviewItems = result.BacklogItems
            .Where(b => b.Category == BacklogCategory.NeedsReview)
            .GroupBy(b => b.FilePath)
            .ToList();

        if (reviewItems.Any())
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## ⚠️  Requieren Revisión");
            sb.AppendLine();
            sb.AppendLine("> Transformados automáticamente pero requieren validación humana.");
            sb.AppendLine();

            foreach (var fileGroup in reviewItems)
            {
                sb.AppendLine($"### `{fileGroup.Key}`");
                foreach (var item in fileGroup)
                {
                    sb.AppendLine($"- **{item.Title}**");
                    sb.AppendLine($"  - {item.Description}");
                    sb.AppendLine($"  - 💡 *{item.Reason}*");
                }
                sb.AppendLine();
            }
        }

        // ─── Build Result ─────────────────────────────────────────────────────
        if (result.BuildResult?.WasExecuted == true)
        {
            sb.AppendLine("---");
            sb.AppendLine();

            if (result.BuildResult.Success)
            {
                sb.AppendLine($"## ✅ Build: COMPILÓ CORRECTAMENTE");
                sb.AppendLine();
                sb.AppendLine($"El código migrado compila sin errores contra `{targetFramework}`.");
                if (result.BuildResult.WarningCount > 0)
                    sb.AppendLine($"> ⚠️  {result.BuildResult.WarningCount} warning(s) de compilación — revisar pero no bloquean el build.");
            }
            else
            {
                sb.AppendLine($"## ❌ Build: {result.BuildResult.ErrorCount} ERROR(ES) DE COMPILACIÓN");
                sb.AppendLine();
                sb.AppendLine("> Estos errores deben resolverse antes de que el código funcione.");
                sb.AppendLine();

                // Agrupar errores por archivo
                var errorsByFile = result.BuildResult.Errors
                    .GroupBy(e => e.FilePath)
                    .ToList();

                foreach (var fileGroup in errorsByFile)
                {
                    sb.AppendLine($"### `{fileGroup.Key}`");
                    foreach (var err in fileGroup)
                        sb.AppendLine($"- **{err.Code}** (línea {err.Line}): {err.Message}");
                    sb.AppendLine();
                }

                if (result.BuildResult.WarningCount > 0)
                    sb.AppendLine($"> ⚠️  {result.BuildResult.WarningCount} warning(s) adicionales.");
            }

            sb.AppendLine($"> Build ejecutado en {result.BuildResult.Duration.TotalSeconds:F1}s");
            sb.AppendLine();
        }

        // ─── Changes Applied ──────────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## ✅ Cambios Aplicados Automáticamente");
        sb.AppendLine();
        foreach (var change in changes)
            sb.AppendLine($"- {change}");
        sb.AppendLine();

        // ─── Diff visual por archivo ──────────────────────────────────────────
        if (result.FileDiffs.Any())
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 🔍 Diff de Archivos Modificados");
            sb.AppendLine();
            sb.AppendLine($"> Se muestran los primeros {result.FileDiffs.Count} archivos modificados.");
            sb.AppendLine("> Las líneas con `-` fueron eliminadas, las líneas con `+` fueron agregadas.");
            sb.AppendLine();

            foreach (var (filePath, diff) in result.FileDiffs)
            {
                sb.AppendLine($"### `{filePath}`");
                sb.AppendLine();
                sb.AppendLine(GenerateDiff(diff.Original, diff.Transformed, maxLines: 80));
                sb.AppendLine();
            }
        }

        // ─── Next Steps ───────────────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Próximos Pasos");
        sb.AppendLine();
        sb.AppendLine("1. Resolver todos los items ❌ del backlog con tu arquitecto");
        sb.AppendLine("2. Revisar los items ⚠️  y validar que el comportamiento es correcto");
        sb.AppendLine("3. Ejecutar `dotnet build` para verificar errores de compilación");
        sb.AppendLine("4. Actualizar migraciones de EF Core si usás base de datos");
        sb.AppendLine("5. Testear todos los endpoints y funcionalidad crítica");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by NexShift — https://nexshift.dev*");

        return sb.ToString();
    }

    // ── Generador de diff unificado (línea a línea) ───────────────────────────
    // Muestra las líneas que cambiaron con prefijo - (removida) o + (agregada).
    // Incluye 2 líneas de contexto alrededor de cada cambio para legibilidad.
    private static string GenerateDiff(string original, string transformed, int maxLines = 80)
    {
        var origLines = original.Split('\n');
        var newLines  = transformed.Split('\n');

        var sb = new StringBuilder();
        sb.AppendLine("```diff");

        var maxLen = Math.Max(origLines.Length, newLines.Length);
        var outputLines = 0;
        var i = 0;

        while (i < maxLen && outputLines < maxLines)
        {
            var origLine = i < origLines.Length ? origLines[i] : null;
            var newLine  = i < newLines.Length  ? newLines[i]  : null;

            if (origLine == null)
            {
                // Nueva línea agregada
                sb.AppendLine($"+ {newLine}");
                outputLines++;
            }
            else if (newLine == null)
            {
                // Línea eliminada
                sb.AppendLine($"- {origLine}");
                outputLines++;
            }
            else if (origLine.TrimEnd() != newLine.TrimEnd())
            {
                // Línea modificada: mostrar antes y después
                sb.AppendLine($"- {origLine}");
                sb.AppendLine($"+ {newLine}");
                outputLines += 2;
            }
            // Si son iguales no se muestra (diff limpio)

            i++;
        }

        if (outputLines >= maxLines)
            sb.AppendLine($"  ... (diff truncado — {maxLen - i} líneas adicionales)");

        sb.AppendLine("```");
        return sb.ToString();
    }
}