using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Models;

namespace eQuantic.UI.Compiler.Services;

public class SemanticValidator
{
    private readonly SemanticModel _semanticModel;

    public SemanticValidator(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public List<CompilationError> Validate(ComponentDefinition component)
    {
        var errors = new List<CompilationError>();

        // Only validate logic for Stateful Components (Client Logic)
        if (!component.IsStateful) return errors;

        var root = _semanticModel.SyntaxTree.GetRoot();
        
        // Find all method invocations
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol == null) continue;

            var containingType = symbol.ContainingType.ToString();

            // Rule 1: No System.IO in Client Components
            if (containingType.StartsWith("System.IO"))
            {
                errors.Add(CreateError($"Forbidden: System.IO usage '{containingType}' in Client Component.", invocation));
            }
            
            // Rule 2: No direct Database access (Entity Framework)
            if (containingType.StartsWith("Microsoft.EntityFrameworkCore"))
            {
                errors.Add(CreateError($"Forbidden: Direct DB access '{containingType}' in Client Component. Use Server Actions.", invocation));
            }
        }

        return errors;
    }

    private CompilationError CreateError(string message, SyntaxNode node)
    {
        var position = node.GetLocation().GetLineSpan().StartLinePosition;
        return new CompilationError
        {
            Message = message,
            Line = position.Line + 1,
            Column = position.Character + 1
        };
    }
}
