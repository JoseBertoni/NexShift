using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexShift.Core.Interfaces;
using Octokit;

namespace NexShift.Infrastructure.Services;

public class GitHubService : IGitHubService
{
    private readonly GitHubClient _client;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(ILogger<GitHubService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _client = new GitHubClient(new ProductHeaderValue("NexShift"));

        var token = configuration["GitHub:Token"]
                 ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        if (!string.IsNullOrEmpty(token))
            _client.Credentials = new Credentials(token);
    }

    public async Task<RepositoryTree> GetRepositoryTreeAsync(string repoUrl, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        _logger.LogInformation("Obteniendo tree de {Owner}/{Repo}", owner, repo);

        var repoInfo = await _client.Repository.Get(owner, repo);
        var tree = await _client.Git.Tree.GetRecursive(owner, repo, repoInfo.DefaultBranch);

        var allBlobs = tree.Tree
            .Where(f => f.Type.Value == TreeType.Blob)
            .Select(f => f.Path)
            .ToList();

        if (tree.Truncated)
        {
            _logger.LogWarning("Tree truncado para {Owner}/{Repo} — expandiendo manualmente", owner, repo);
            allBlobs = await ExpandTruncatedTreeAsync(owner, repo, repoInfo.DefaultBranch, cancellationToken);
        }

        // Filtrar /obj/ y /bin/ de AllFiles también
        allBlobs = allBlobs
            .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
            .ToList();

        var result = new RepositoryTree
        {
            Owner = owner,
            RepoName = repo,
            DefaultBranch = repoInfo.DefaultBranch,
            RepoSizeKb = repoInfo.Size,
            TotalFiles = allBlobs.Count
        };

        result.AllFiles = allBlobs;

        result.CsprojFiles = allBlobs
            .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToList();

        result.CsFiles = allBlobs
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        result.PackagesConfigFiles = allBlobs
            .Where(f => f.EndsWith("packages.config", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith("Directory.Build.props", StringComparison.OrdinalIgnoreCase)
                     || f.Equals("global.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        result.WebConfigFiles = allBlobs
            .Where(f => f.EndsWith("Web.config", StringComparison.OrdinalIgnoreCase)
                     && !f.Contains("bin/"))
            .ToList();

        _logger.LogInformation("Tree obtenido: {CsFiles} .cs, {Csproj} .csproj, {Total} total",
            result.CsFiles.Count, result.CsprojFiles.Count, result.TotalFiles);

        return result;
    }

    private async Task<List<string>> ExpandTruncatedTreeAsync(
    string owner, string repo, string branch, CancellationToken cancellationToken)
{
    var allFiles = new List<string>();

    // Obtenemos el tree raíz sin recursivo
    var rootTree = await _client.Git.Tree.Get(owner, repo, branch);

    foreach (var item in rootTree.Tree)
    {
        if (item.Type.Value == TreeType.Blob)
        {
            allFiles.Add(item.Path);
        }
        else if (item.Type.Value == TreeType.Tree)
        {
            try
            {
                // Expandimos cada directorio recursivamente
                var subTree = await _client.Git.Tree.GetRecursive(owner, repo, item.Sha);
                var subFiles = subTree.Tree
                    .Where(f => f.Type.Value == TreeType.Blob)
                    .Select(f => $"{item.Path}/{f.Path}")
                    .ToList();
                allFiles.AddRange(subFiles);

                // Rate limit safety
                await Task.Delay(100, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo expandir directorio {Path}", item.Path);
            }
        }
    }

    return allFiles;
}

    public async Task<string> GetFileContentAsync(string repoUrl, string filePath, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);

        var contents = await _client.Repository.Content.GetAllContents(owner, repo, filePath);
        var file = contents.FirstOrDefault();

        if (file == null) return string.Empty;

        var content = file.Content ?? string.Empty;

        // Eliminar BOM (Byte Order Mark) que rompe el parser XML
        content = content.TrimStart('\uFEFF', '\u200B');

        return content;
    }

    // Convierte "https://github.com/owner/repo" en ("owner", "repo")
    private static (string owner, string repo) ParseRepoUrl(string repoUrl)
    {
        var uri = new Uri(repoUrl.TrimEnd('/'));
        var segments = uri.AbsolutePath.Trim('/').Split('/');

        if (segments.Length < 2)
            throw new ArgumentException($"URL de repo inválida: {repoUrl}");

        return (segments[0], segments[1]);
    }
}