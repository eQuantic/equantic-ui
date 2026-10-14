using eQuantic.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Native.Generators;

/// <summary>
/// Reads <c>builder.Workspace.Opens…</c> out of <c>Program.cs</c>, so the line that widens what
/// the app hands to the system also reaches the build.
/// <para>
/// On a desktop the line is a run-time fact and nothing more: the policy is composed when the app
/// is built. A phone adds a second reader. It answers "does something open this link" only for the
/// schemes the app's manifest says it may ask about (<c>LSApplicationQueriesSchemes</c> on iOS,
/// <c>queries</c> on Android 11 and later), and a manifest is written at BUILD time. The same shape
/// as <see cref="BundleDeclarations"/>: the fluent call is read here, written as an assembly
/// attribute, and the build writes each system's file from it.
/// </para>
/// </summary>
internal static class WorkspaceDeclarations
{
    private const string BuilderType = "eQuantic.UI.Native.Hosting.PhotonWorkspaceBuilder";

    internal static readonly DiagnosticDescriptor SchemeMustBeConstant = new(
        "EQ3007", "A scheme a phone app opens must be a constant",
        "The scheme passed to builder.Workspace.Opens() is built at run time, and a phone answers "
        + "CanOpen only for the schemes its manifest lists, which is written at BUILD time. The "
        + "app would open the link and still be told nothing opens it. Pass something the "
        + "compiler can evaluate: a literal or a const.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>
    /// One opened scheme. A null <paramref name="scheme"/> means the call was ours and its argument
    /// could not be read at build time, which is reported at <paramref name="location"/> where a
    /// manifest has to be written.
    /// </summary>
    internal readonly struct Declaration(string? scheme, Location location)
    {
        /// <summary>The scheme's name, lower-case, or null when it could not be read.</summary>
        internal string? Scheme { get; } = scheme;

        /// <summary>Where it was written.</summary>
        internal Location Location { get; } = location;
    }

    internal static bool MightDeclare(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name },
        } && name.StartsWith("Opens", System.StringComparison.Ordinal);

    /// <summary>The scheme a call opens, or null when this call is not one of ours, or opens
    /// nothing the builder would accept.</summary>
    internal static Declaration? Read(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetOperation(context.Node) is not IInvocationOperation invocation)
            return null;
        if (invocation.TargetMethod.ContainingType?.ToDisplayString() != BuilderType) return null;

        var location = context.Node.GetLocation();
        if (OpenedSchemeRule.Named(invocation.TargetMethod.Name) is { } named)
            return new Declaration(named, location);

        // The general form, `Opens("slack")`. A name the builder refuses is nothing to write: the
        // builder says so itself when the app starts, by throwing at this very line.
        if (invocation.Arguments.Length != 1) return null;
        return invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string declared }
            ? BundleFactRule.Scheme(declared) is { } scheme ? new Declaration(scheme, location) : null
            : new Declaration(null, location);
    }
}
