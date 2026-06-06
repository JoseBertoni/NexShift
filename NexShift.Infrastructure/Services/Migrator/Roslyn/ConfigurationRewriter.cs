using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NexShift.Infrastructure.Services.Migrator.Roslyn.Rewriters;

/// <summary>
/// Migrates legacy configuration access patterns:
/// - WebConfigurationManager.AppSettings["key"] → _configuration["key"]
/// - ConfigurationManager.AppSettings["key"]    → _configuration["key"]
/// - WebConfigurationManager.ConnectionStrings["key"].ConnectionString
///     → _configuration.GetConnectionString("key")
/// Requires IConfiguration to be injected — marks NeedsConfigurationInjection = true.
/// </summary>
public sealed class ConfigurationRewriter : CSharpSyntaxRewriter
{
    private bool _configAlreadyInjected;

    public bool NeedsConfigurationInjection { get; private set; }
    public bool WasModified { get; private set; }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        _configAlreadyInjected = node.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Type.ToString().Contains("IConfiguration"));

        return base.VisitClassDeclaration(node);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var rootName = GetRootIdentifier(node);

        if (rootName is not ("WebConfigurationManager" or "ConfigurationManager"))
            return base.VisitMemberAccessExpression(node);

        // ConnectionStrings["key"].ConnectionString
        // → _configuration.GetConnectionString("key")
        if (node.Name.Identifier.Text == "ConnectionString" &&
            node.Expression is ElementAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax connStrings }
                && connStrings.Name.Identifier.Text == "ConnectionStrings")
        {
            if (connStrings.Expression is ElementAccessExpressionSyntax connElem)
            {
                var keyArg = connElem.ArgumentList.Arguments.First().Expression;
                WasModified = true;
                NeedsConfigurationInjection = !_configAlreadyInjected;

                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("_configuration"),
                        IdentifierName("GetConnectionString")),
                    ArgumentList(SingletonSeparatedList(Argument(keyArg))))
                    .WithTriviaFrom(node);
            }
        }

        // AppSettings["key"] — handled at ElementAccess level below
        return base.VisitMemberAccessExpression(node);
    }

    public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        // WebConfigurationManager.AppSettings["key"] → _configuration["key"]
        if (node.Expression is MemberAccessExpressionSyntax ma &&
            ma.Name.Identifier.Text == "AppSettings" &&
            GetRootIdentifier(ma) is "WebConfigurationManager" or "ConfigurationManager")
        {
            WasModified = true;
            NeedsConfigurationInjection = !_configAlreadyInjected;

            return ElementAccessExpression(
                IdentifierName("_configuration"),
                node.ArgumentList)
                .WithTriviaFrom(node);
        }

        return base.VisitElementAccessExpression(node);
    }

    public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var ns = node.Name?.ToString() ?? "";
        if (ns is "System.Configuration" or "System.Web.Configuration")
        {
            WasModified = true;
            return null;
        }
        return base.VisitUsingDirective(node);
    }

    private static string? GetRootIdentifier(ExpressionSyntax expr)
    {
        return expr switch
        {
            MemberAccessExpressionSyntax ma => GetRootIdentifier(ma.Expression),
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => null
        };
    }
}
