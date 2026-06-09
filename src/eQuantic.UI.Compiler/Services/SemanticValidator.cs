using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Models;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Validates the client/server boundary for stateful (client) components. Calls into APIs that have
/// no browser-side equivalent — file I/O, networking, threading, processes, native interop, direct DB
/// access — are reported as build errors so the user is steered to a <c>[ServerAction]</c> instead of
/// shipping code that would only fail at runtime.
/// </summary>
public class SemanticValidator
{
    private readonly SemanticModel _semanticModel;

    /// <summary>
    /// Forbidden containing-type prefixes for client components. Each entry is matched against the
    /// invoked symbol's containing type (display string). Order matters only for which message wins.
    /// </summary>
    private static readonly (string Prefix, string Code, string Reason)[] ForbiddenApis =
    {
        ("System.IO", "EQ2101", "file-system access"),
        ("Microsoft.EntityFrameworkCore", "EQ2102", "direct database access"),
        ("System.Data", "EQ2102", "direct database access"),
        ("System.Net.Http", "EQ2103", "HTTP networking"),
        ("System.Net.Sockets", "EQ2103", "socket networking"),
        ("System.Threading.Thread", "EQ2104", "OS threading"),       // Thread / ThreadPool — not Tasks
        ("System.Threading.Monitor", "EQ2104", "OS-level locking"),
        ("System.Threading.Mutex", "EQ2104", "OS-level locking"),
        ("System.Threading.Semaphore", "EQ2104", "OS-level locking"),
        ("System.Diagnostics.Process", "EQ2105", "spawning processes"),
        ("System.Runtime.InteropServices", "EQ2106", "native interop (P/Invoke)"),
        ("System.Reflection.Emit", "EQ2107", "runtime IL generation"),
    };

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

            var containingType = symbol.ContainingType?.ToDisplayString() ?? string.Empty;

            foreach (var (prefix, code, reason) in ForbiddenApis)
            {
                if (containingType.StartsWith(prefix, StringComparison.Ordinal))
                {
                    errors.Add(CreateError(
                        code,
                        $"'{containingType}' ({reason}) is not available in a client component. Move this to a [ServerAction].",
                        invocation,
                        component.SourcePath));
                    break; // one diagnostic per invocation
                }
            }
        }

        return errors;
    }

    private static CompilationError CreateError(string code, string message, SyntaxNode node, string sourcePath)
    {
        var position = node.GetLocation().GetLineSpan().StartLinePosition;
        return new CompilationError
        {
            Code = code,
            Message = message,
            SourcePath = sourcePath,
            Line = position.Line + 1,
            Column = position.Character + 1,
        };
    }
}
