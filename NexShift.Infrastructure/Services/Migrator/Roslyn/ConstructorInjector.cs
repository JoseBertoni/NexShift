using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NexShift.Infrastructure.Services.Migrator.Roslyn.Rewriters;

/// <summary>
/// Injects missing dependencies into a class constructor.
/// Given a set of (interfaceType, fieldName, paramName) tuples,
/// adds the private readonly field and the constructor parameter + assignment
/// if they are not already present.
///
/// Example injection: IHttpContextAccessor → _httpContextAccessor
/// </summary>
public sealed class ConstructorInjector : CSharpSyntaxRewriter
{
    public record Injection(string InterfaceType, string FieldName, string ParamName);

    private readonly IReadOnlyList<Injection> _injections;
    public bool WasModified { get; private set; }

    public ConstructorInjector(IReadOnlyList<Injection> injections)
    {
        _injections = injections;
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var modified = node;

        foreach (var inj in _injections)
        {
            // Skip if field already exists
            var fieldExists = modified.Members
                .OfType<FieldDeclarationSyntax>()
                .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == inj.FieldName));

            if (!fieldExists)
            {
                var field = BuildField(inj.InterfaceType, inj.FieldName);
                modified = modified.WithMembers(modified.Members.Insert(0, field));
                WasModified = true;
            }

            // Add constructor parameter + assignment
            modified = (ClassDeclarationSyntax)InjectIntoConstructor(modified, inj);
        }

        return modified;
    }

    private static SyntaxNode InjectIntoConstructor(ClassDeclarationSyntax classNode, Injection inj)
    {
        var constructor = classNode.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        if (constructor == null)
            return classNode; // No constructor found — skip (edge case)

        // Check if already has the parameter
        var paramExists = constructor.ParameterList.Parameters
            .Any(p => p.Type?.ToString() == inj.InterfaceType);

        if (paramExists) return classNode;

        // Add parameter
        var newParam = Parameter(Identifier(inj.ParamName))
            .WithType(IdentifierName(inj.InterfaceType).WithTrailingTrivia(Space));

        var newParams = constructor.ParameterList.AddParameters(newParam);

        // Add assignment to body: _field = paramName;
        var assignment = ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(inj.FieldName),
                IdentifierName(inj.ParamName)))
            .WithLeadingTrivia(ElasticTab, ElasticTab)
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed);

        var newBody = constructor.Body == null
            ? Block(assignment)
            : constructor.Body.AddStatements(assignment);

        var newConstructor = constructor
            .WithParameterList(newParams)
            .WithBody(newBody);

        return classNode.ReplaceNode(constructor, newConstructor);
    }

    private static FieldDeclarationSyntax BuildField(string interfaceType, string fieldName)
    {
        return FieldDeclaration(
            VariableDeclaration(IdentifierName(interfaceType).WithTrailingTrivia(Space))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(fieldName)))))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PrivateKeyword).WithTrailingTrivia(Space),
                Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(Space)))
            .WithLeadingTrivia(ElasticTab)
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed);
    }
}
