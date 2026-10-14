using System.Collections.Generic;
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

    /// <summary>
    /// The policy itself. An app that registers its OWN policy decides what it opens without the
    /// builder (<c>OpenUrlPolicy.Web.Allowing("slack")</c>), and the builder steps aside for it, so
    /// the schemes it widens by have to reach a phone's manifest as well, or the app would open the
    /// link and be told nothing opens it.
    /// </summary>
    private const string PolicyType = "eQuantic.UI.Primitives.OpenUrlPolicy";

    internal static readonly DiagnosticDescriptor SchemeMustBeConstant = new(
        "EQ3007", "A scheme a phone app opens must be a constant",
        "The scheme passed to builder.Workspace.Opens() or OpenUrlPolicy.Allowing() is built at run time, and a phone answers "
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
        BuilderCall.MethodName(node) is { } name
        && (name.StartsWith("Opens", System.StringComparison.Ordinal) || name == "Allowing");

    /// <summary>The schemes a call opens: none when it is not one of ours, or opens nothing the
    /// builder or the policy would accept.</summary>
    internal static IEnumerable<Declaration?> Read(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetOperation(context.Node) is not IInvocationOperation invocation)
            yield break;
        var type = invocation.TargetMethod.ContainingType?.ToDisplayString();
        if (type != BuilderType && type != PolicyType) yield break;

        var location = context.Node.GetLocation();
        if (type == BuilderType && OpenedSchemeRule.Named(invocation.TargetMethod.Name) is { } named)
        {
            yield return new Declaration(named, location);
            yield break;
        }

        // The general form, `Opens("slack")` or `Allowing("slack")`, and the policy's several at
        // once, `Allowing(["acme", "slack"])`. A name the builder refuses is nothing to write: the
        // builder says so itself when the app starts, by throwing at this very line.
        if (invocation.Arguments.Length != 1) yield break;
        foreach (var argument in Each(invocation.Arguments[0].Value))
        {
            if (argument is null || Constant(argument) is not { } declared)
                yield return new Declaration(null, location);
            else if (BundleFactRule.Scheme(declared) is { } scheme && scheme != FileScheme)
                yield return new Declaration(scheme, location);
        }
    }

    /// <summary>
    /// The scheme arguments one call passes: the argument itself for a string, each element of a
    /// collection written at the call for a sequence, and a null for a sequence the build cannot see
    /// into (a variable, a spread), which a phone reports.
    /// </summary>
    private static IEnumerable<IOperation?> Each(IOperation argument)
    {
        while (argument is IConversionOperation { IsImplicit: true } conversion) argument = conversion.Operand;
        if (argument.Type?.SpecialType == SpecialType.System_String)
        {
            yield return argument;
            yield break;
        }
        var elements = argument switch
        {
            ICollectionExpressionOperation collection => collection.Elements,
            IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues,
            _ => default(System.Collections.Immutable.ImmutableArray<IOperation>?),
        };
        if (elements is not { } known)
        {
            yield return null;
            yield break;
        }
        foreach (var element in known) yield return element is ISpreadOperation ? null : element;
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
