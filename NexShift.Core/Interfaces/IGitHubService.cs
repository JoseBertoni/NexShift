namespace NexShift.Core.Interfaces;

public interface IGitHubService
{
    /// <summary>
    /// Obtiene el árbol completo de archivos del repo sin bajar nada
    /// </summary>
    Task<RepositoryTree> GetRepositoryTreeAsync(string repoUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja el contenido de un archivo específico
    /// </summary>
    Task<string> GetFileContentAsync(string repoUrl, string filePath, CancellationToken cancellationToken = default);
}

public class RepositoryTree
{
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public List<string> CsprojFiles { get; set; } = new();
    public List<string> PackagesConfigFiles { get; set; } = new();
    public List<string> CsFiles { get; set; } = new();
    public int TotalFiles { get; set; }
    public long RepoSizeKb { get; set; }
    public List<string> AllFiles { get; set; } = new();
    public List<string> WebConfigFiles { get; set; } = new();
}