using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces;

public interface IBuildValidator
{
    /// <summary>
    /// Escribe los archivos transformados en un directorio temporal,
    /// ejecuta dotnet restore + dotnet build y retorna el resultado parseado.
    /// </summary>
    Task<BuildResult> ValidateAsync(
        Dictionary<string, string> files,
        string targetFramework,
        CancellationToken cancellationToken = default);
}
