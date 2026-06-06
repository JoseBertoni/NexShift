using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NexShift.Infrastructure.Services.Migrator.Roslyn.Rewriters;

/// <summary>
/// Ensures required using directives are present after transformation.
/// Adds only what's missing — never duplicates.
/// </summary>
public static class UsingNormalizer
{
    private static readonly Dictionary<string, string[]> RequiredUsings = new()
    {
        ["IHttpContextAccessor"] = ["Microsoft.AspNetCore.Http"],
        ["IConfiguration"]       = ["Microsoft.Extensions.Configuration"],
        ["IActionResult"]        = ["Microsoft.AspNetCore.Mvc"],
        ["ControllerBase"]       = ["Microsoft.AspNetCore.Mvc"],
        ["ILogger"]              = ["Microsoft.Extensions.Logging"],
    };

    public static CompilationUnitSyntax AddMissingUsings(
        CompilationUnitSyntax root,
        IEnumerable<string> requiredForSymbols)
    {
        var existing = root.Usings
            .Select(u => u.Name?.ToString() ?? "")
            .ToHashSet(StringComparer.Ordinal);

        var toAdd = requiredForSymbols
            .Where(symbol => RequiredUsings.TryGetValue(symbol, out _))
            .SelectMany(symbol => RequiredUsings[symbol])
            .Where(ns => !existing.Contains(ns))
            .Distinct()
            .Select(ns => UsingDirective(ParseName(ns))
                .WithTrailingTrivia(ElasticCarriageReturnLineFeed))
            .ToList();

        if (toAdd.Count == 0) return root;

        return root.WithUsings(root.Usings.AddRange(toAdd));
    }
}
