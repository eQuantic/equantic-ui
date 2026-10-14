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
        + "build can read: a literal, a const, or one of .NET's own scheme names (Uri.UriSchemeMailto).",
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
        BuilderCall.MethodName(node) is { } name && name.StartsWith("Opens", System.StringComparison.Ordinal);

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
        return Constant(invocation.Arguments[0].Value) is { } declared
            ? BundleFactRule.Scheme(declared) is { } scheme && scheme != FileScheme ? new Declaration(scheme, location) : null
            : new Declaration(null, location);
    }

    /// <summary><c>file</c> passes as a scheme NAME and is refused by the policy itself: a file has
    /// typed doors. Declaring it would list a scheme the app can never open.</summary>
    private const string FileScheme = "file";

    /// <summary>
    /// The text an argument is known to hold at build time: a constant, or one of .NET's own scheme
    /// names. <c>Uri.UriSchemeMailto</c> and its siblings are <c>static readonly</c>, not <c>const</c>,
    /// so the compiler has no value for them, and the builder's own documentation offers them. Their
    /// value is read from the <see cref="System.Uri"/> this generator runs on, by the field's name, so
    /// no list here can fall behind the ones .NET adds.
    /// </summary>
    private static string? Constant(IOperation argument)
    {
        if (argument.ConstantValue is { HasValue: true, Value: string constant }) return constant;
        return argument is IFieldReferenceOperation
        {
            Field: { IsStatic: true, IsReadOnly: true, Name: var name, ContainingType: var type },
        } && type?.ToDisplayString() == "System.Uri" && name.StartsWith("UriScheme", System.StringComparison.Ordinal)
            ? typeof(System.Uri).GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.GetValue(null) as string
            : null;
    }
}
