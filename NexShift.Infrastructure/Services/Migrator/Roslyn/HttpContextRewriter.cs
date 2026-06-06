using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NexShift.Infrastructure.Services.Migrator.Roslyn.Rewriters;

/// <summary>
/// Replaces HttpContext.Current usages with IHttpContextAccessor injected via constructor.
/// Pattern: HttpContext.Current.X → _httpContextAccessor.HttpContext!.X
/// Also removes "using System.Web;" when no other System.Web references remain.
/// </summary>
public sealed class HttpContextRewriter : CSharpSyntaxRewriter
{
    private bool _needsAccessor;
    private bool _accessorAlreadyInjected;

    public bool NeedsAccessorInjection => _needsAccessor && !_accessorAlreadyInjected;
    public bool WasModified { get; private set; }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // Check if IHttpContextAccessor is already injected
        _accessorAlreadyInjected = node.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Type.ToString().Contains("IHttpContextAccessor"));

        return base.VisitClassDeclaration(node);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Match: HttpContext.Current  (and HttpContext.Current.X)
        if (node.Expression is IdentifierNameSyntax id &&
            id.Identifier.Text == "HttpContext" &&
            node.Name.Identifier.Text == "Current")
        {
            _needsAccessor = true;
            WasModified = true;

            // Replace with: _httpContextAccessor.HttpContext!
            var replacement = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("_httpContextAccessor"),
                Token(SyntaxKind.DotToken),
                IdentifierName("HttpContext!"))
                .WithTriviaFrom(node);

            return replacement;
        }

        return base.VisitMemberAccessExpression(node);
    }

    public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
    {
        // Remove "using System.Web;" — it doesn't exist in .NET 8
        if (node.Name?.ToString() == "System.Web")
        {
            WasModified = true;
            return null; // removes the node from the tree
        }

        return base.VisitUsingDirective(node);
    }
}
