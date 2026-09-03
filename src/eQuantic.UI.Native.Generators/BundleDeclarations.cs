using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Native.Generators;

/// <summary>
/// Reads <c>builder.Bundle.…</c> out of <c>Program.cs</c>, so the app states what the system reads
/// about it in C# and the generator writes the assembly declaration the build turns into an
/// Info.plist.
/// <para>
/// The same shape as <see cref="CapabilityDeclarations"/> and <see cref="EntitlementDeclarations"/>
/// — one idiom for platform facts, whether what is being asked is a person, the operating system,
/// or the Finder.
/// </para>
/// </summary>
internal static class BundleDeclarations
{
    private const string BuilderType = "eQuantic.UI.Native.Hosting.PhotonBundleBuilder";

    internal static readonly DiagnosticDescriptor ValueMustBeConstant = new(
        "EQ3005", "A bundle key's value must be a constant",
        "The value passed to builder.Bundle.{0}() is built at run time, and the Info.plist is "
        + "written at BUILD time — so this one would never reach the app's manifest. Use a literal "
        + "or a const string.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>
    /// One declared fact: Apple's key, the value as text, and how to write it. A null
    /// <paramref name="Key"/> means the call was ours but its value could not be read at build
    /// time — reported at <paramref name="Location"/> rather than silently dropped.
    /// </summary>
    internal readonly struct Declaration(
        string? key, string value, string kind, string method, Location location)
    {
        /// <summary>Apple's key, or null when the value could not be read at build time.</summary>
        internal string? Key { get; } = key;

        /// <summary>The value, as text — <see cref="Kind"/> decides how it is written.</summary>
        internal string Value { get; } = value;

        /// <summary>Text, Flag or UrlScheme — the name of a PhotonBundleValueKind member.</summary>
        internal string Kind { get; } = kind;

        /// <summary>The builder method that stated it, so a diagnostic can name it.</summary>
        internal string Method { get; } = method;

        /// <summary>Where it was written.</summary>
        internal Location Location { get; } = location;
    }

    private static readonly string[] Methods =
    [
        "Copyright", "Category", "MinimumSystemVersion", "Agent", "UrlScheme", "Key", "Flag",
    ];

    internal static bool MightDeclare(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name },
        } && System.Array.IndexOf(Methods, name) >= 0;

    /// <summary>The fact, or null when this call is not one of ours.</summary>
    internal static Declaration? Read(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetOperation(context.Node) is not IInvocationOperation invocation)
            return null;
        if (invocation.TargetMethod.ContainingType?.ToDisplayString() != BuilderType) return null;

        var method = invocation.TargetMethod.Name;
        var location = context.Node.GetLocation();
        var arguments = invocation.Arguments;

        Declaration Unreadable(string kind) => new(null, "", kind, method, location);
        Declaration Fact(string key, string value, string kind) => new(key, value, kind, method, location);

        switch (method)
        {
            // No argument at all: the method IS the fact.
            case "Agent":
                return Fact("LSUIElement", "true", "Flag");

            case "Copyright":
                return Constant(arguments, 0) is { } notice
                    ? Fact("NSHumanReadableCopyright", notice, "Text")
                    : Unreadable("Text");

            case "MinimumSystemVersion":
                return Constant(arguments, 0) is { } version
                    ? Fact("LSMinimumSystemVersion", version, "Text")
                    : Unreadable("Text");

            case "Category":
                return AppleCategory(EnumMemberName(arguments, 0)) is { } category
                    ? Fact("LSApplicationCategoryType", category, "Text")
                    : Unreadable("Text");

            case "UrlScheme":
                // Not written under a key of its own: the schemes are collected into the one
                // CFBundleURLTypes array the system reads.
                return Constant(arguments, 0) is { } scheme ? Fact("", scheme, "UrlScheme") : Unreadable("UrlScheme");

            case "Key":
                return Constant(arguments, 0) is { } key && Constant(arguments, 1) is { } value
                    ? Fact(key, value, "Text")
                    : Unreadable("Text");

            case "Flag":
                return Constant(arguments, 0) is { } flagKey && Boolean(arguments, 1) is { } flag
                    ? Fact(flagKey, flag ? "true" : "false", "Flag")
                    : Unreadable("Flag");

            default:
                return null;
        }
    }

    private static string? Constant(System.Collections.Immutable.ImmutableArray<IArgumentOperation> arguments, int index) =>
        arguments.Length > index && arguments[index].Value.ConstantValue is { HasValue: true, Value: string value }
            ? value
            : null;

    private static bool? Boolean(System.Collections.Immutable.ImmutableArray<IArgumentOperation> arguments, int index) =>
        arguments.Length > index && arguments[index].Value.ConstantValue is { HasValue: true, Value: bool value }
            ? value
            : null;

    /// <summary>
    /// The NAME of the enum member passed, found from the constant it reduces to.
    /// <para>
    /// By name and never by ordinal, which is a lesson this repository paid for once already: an
    /// enum member inserted in the middle shifted every value after it, and a manifest quietly
    /// declared Motion where the app had asked for Location. A name that stops matching is a
    /// compile error in the map below; an ordinal that shifts is a wrong app.
    /// </para>
    /// </summary>
    private static string? EnumMemberName(
        System.Collections.Immutable.ImmutableArray<IArgumentOperation> arguments, int index)
    {
        if (arguments.Length <= index) return null;
        var argument = arguments[index].Value;
        if (argument.ConstantValue is not { HasValue: true, Value: { } constant }) return null;
        if (argument.Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType) return null;

        return enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, constant))
            ?.Name;
    }

    /// <summary>Apple's spelling, by the member's NAME. Spelled here because a source generator
    /// sees the CALL and never the method body that maps it — the same split the entitlements
    /// builder lives with, and a test walks the enum to prove neither side has drifted.</summary>
    internal static string? AppleCategory(string? member) => member switch
    {
        "Utilities" => "public.app-category.utilities",
        "DeveloperTools" => "public.app-category.developer-tools",
        "Productivity" => "public.app-category.productivity",
        "Business" => "public.app-category.business",
        "Finance" => "public.app-category.finance",
        "GraphicsDesign" => "public.app-category.graphics-design",
        "Photography" => "public.app-category.photography",
        "Music" => "public.app-category.music",
        "Video" => "public.app-category.video",
        "Education" => "public.app-category.education",
        "SocialNetworking" => "public.app-category.social-networking",
        "News" => "public.app-category.news",
        "Reference" => "public.app-category.reference",
        "HealthcareFitness" => "public.app-category.healthcare-fitness",
        "Games" => "public.app-category.games",
        _ => null,   // None, or a member added without its Apple spelling — the test catches that.
    };
}
