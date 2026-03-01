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

        // Info básica del repo
        var repoInfo = await _client.Repository.Get(owner, repo);

        // Tree completo en una sola llamada
        var tree = await _client.Git.Tree.GetRecursive(owner, repo, repoInfo.DefaultBranch);



        var result = new RepositoryTree
        {
            Owner = owner,
            RepoName = repo,
            DefaultBranch = repoInfo.DefaultBranch,
            RepoSizeKb = repoInfo.Size,
            TotalFiles = tree.Tree.Count
        };

        result.CsprojFiles = tree.Tree
            .Where(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToList();

        result.CsFiles = tree.Tree
            .Where(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToList();

        result.PackagesConfigFiles = tree.Tree
            .Where(f => f.Path.EndsWith("packages.config", StringComparison.OrdinalIgnoreCase)
                     || f.Path.EndsWith("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
                     || f.Path.EndsWith("Directory.Build.props", StringComparison.OrdinalIgnoreCase)
                     || f.Path.Equals("global.json", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToList();


        return result;
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