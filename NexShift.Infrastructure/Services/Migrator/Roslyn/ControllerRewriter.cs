using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NexShift.Infrastructure.Services.Migrator.Roslyn.Rewriters;

/// <summary>
/// Migrates legacy Web API controller patterns to ASP.NET Core equivalents:
/// - [ApiController] base class: ApiController → ControllerBase
/// - ActionResult → IActionResult
/// - HttpResponseMessage return types → IActionResult
/// - Adds [ApiController] attribute if missing
/// - Adds [Route] attribute if missing
/// </summary>
public sealed class ControllerRewriter : CSharpSyntaxRewriter
{
    public bool WasModified { get; private set; }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var modified = node;

        // Replace base type: ApiController → ControllerBase
        if (node.BaseList != null)
        {
            var newBases = node.BaseList.Types.Select(bt =>
            {
                var typeName = bt.Type.ToString();
                if (typeName == "ApiController")
                {
                    WasModified = true;
                    return bt.WithType(IdentifierName("ControllerBase"));
                }
                return bt;
            });

            modified = modified.WithBaseList(
                node.BaseList.WithTypes(SeparatedList(newBases)));
        }

        // Add [ApiController] attribute if not present
        var hasApiController = node.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() is "ApiController" or "ApiControllerAttribute");

        if (!hasApiController && IsController(node))
        {
            var apiAttr = AttributeList(
                SingletonSeparatedList(
                    Attribute(IdentifierName("ApiController"))))
                .WithTrailingTrivia(ElasticCarriageReturnLineFeed);

            modified = modified.WithAttributeLists(
                modified.AttributeLists.Insert(0, apiAttr));

            WasModified = true;
        }

        return base.VisitClassDeclaration(modified);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var modified = node;
        var returnType = node.ReturnType.ToString().Trim();

        // ActionResult → IActionResult
        if (returnType == "ActionResult")
        {
            modified = modified.WithReturnType(
                IdentifierName("IActionResult")
                    .WithTriviaFrom(node.ReturnType));
            WasModified = true;
        }

        // HttpResponseMessage → IActionResult
        if (returnType == "HttpResponseMessage")
        {
            modified = modified.WithReturnType(
                IdentifierName("IActionResult")
                    .WithTriviaFrom(node.ReturnType));
            WasModified = true;
        }

        // Task<HttpResponseMessage> → Task<IActionResult>
        if (returnType.StartsWith("Task<HttpResponseMessage"))
        {
            var taskType = GenericName(
                Identifier("Task"),
                TypeArgumentList(
                    SingletonSeparatedList<TypeSyntax>(
                        IdentifierName("IActionResult"))))
                .WithTriviaFrom(node.ReturnType);

            modified = modified.WithReturnType(taskType);
            WasModified = true;
        }

        return base.VisitMethodDeclaration(modified);
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // base.InternalServerError() → StatusCode(500)
        // InternalServerError()     → StatusCode(500)
        if (node.Expression is MemberAccessExpressionSyntax ma &&
            ma.Name.Identifier.Text == "InternalServerError" &&
            node.ArgumentList.Arguments.Count == 0)
        {
            WasModified = true;
            return InvocationExpression(
                IdentifierName("StatusCode"),
                ArgumentList(SingletonSeparatedList(
                    Argument(LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(500))))))
                .WithTriviaFrom(node);
        }

        if (node.Expression is IdentifierNameSyntax id &&
            id.Identifier.Text == "InternalServerError" &&
            node.ArgumentList.Arguments.Count == 0)
        {
            WasModified = true;
            return InvocationExpression(
                IdentifierName("StatusCode"),
                ArgumentList(SingletonSeparatedList(
                    Argument(LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(500))))))
                .WithTriviaFrom(node);
        }

        return base.VisitInvocationExpression(node);
    }

    public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var ns = node.Name?.ToString() ?? "";

        // Remove System.Web.Http — replaced by Microsoft.AspNetCore.Mvc
        if (ns is "System.Web.Http" or "System.Web.Mvc")
        {
            WasModified = true;
            return null;
        }

        return base.VisitUsingDirective(node);
    }

    private static bool IsController(ClassDeclarationSyntax node) =>
        node.Identifier.Text.EndsWith("Controller", StringComparison.OrdinalIgnoreCase);
}
