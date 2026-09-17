using System.Runtime.CompilerServices;
using eQuantic.UI.Codegen;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Build;

/// <summary>
/// Generates the TypeScript union of every WIRE KIND the vocabulary declares
/// (<c>src/shared/node-kinds.generated.ts</c>) — the browser's half of "one door per node".
///
/// <para>
/// The C# side closes this with a visitor: a node added to the vocabulary is a method every
/// implementation must fill in, or it does not compile. The browser has no interface to implement —
/// class names do not survive bundling, so the client dispatches on <c>nodeKind</c>, a string. A
/// string has no exhaustiveness. A UNION does: with <c>nodeKind</c> typed as this union rather than
/// <c>string</c>, <c>lowerNodeKind</c> can end in <c>assertNever</c> and a kind with no case stops
/// the runtime's build.
/// </para>
///
/// <para>
/// GENERATED, for the reason every mirror here is: a hand-written union drifts the day a node is
/// added, and a drifted union is worse than the <c>string</c> it replaced — it is a type that lies,
/// and it would lie in the one direction that matters, by making the browser's switch look
/// exhaustive when it is not.
/// </para>
///
/// <para>
/// THE THREE THINGS THAT WOULD MAKE THE KEY NOT AN IDENTITY are checked HERE, and they were checked
/// in <c>VocabularyCoverageTests.EveryNode_DeclaresItsOwnWireKind</c> before: a duplicate kind (two
/// nodes lowering as the same thing, and one of them invisible to any pin keyed by kind), an EMPTY
/// kind (which would generate <c>''</c> into the union), and a concrete node claiming the seam's own
/// word. They belong to the generator because the generator is what cannot proceed without them —
/// and because that is what lets the source-reading pin retire without losing a check.
/// </para>
/// </summary>
public static class NodeKindTsGenerator
{
    /// <summary>
    /// The expansion seam's kind, added EXPLICITLY: <c>UiComponent</c> is abstract, so no scan of
    /// concrete types reaches the sealed <c>"component"</c> it declares for every component an app
    /// or the SDK writes.
    /// </summary>
    public const string Seam = "component";

    public static string Generate()
    {
        var ts = new CodeWriter();
        ts.AppendLine("/**");
        ts.AppendLine(" * GENERATED — do not edit. Every wire kind the C# vocabulary declares");
        ts.AppendLine(" * (eQuantic.UI.Primitives, VisualNode.NodeKind), plus the expansion seam.");
        ts.AppendLine(" * Regenerate: EQ_UPDATE_NODE_KINDS_TS=1 dotnet test eQuantic.UI.Web.Tests");
        ts.AppendLine(" * (NodeKindTsGeneratorTests pins this file byte-for-byte against the generator).");
        ts.AppendLine(" *");
        ts.AppendLine(" * This union is what makes the browser's dispatch exhaustive: `lowerNodeKind`");
        ts.AppendLine(" * ends in assertNever, so a kind added here with no case is a build error.");
        ts.AppendLine(" */");
        ts.AppendLine();
        TsUnion.Write(ts, "NodeKind", Kinds());

        return ts.ToString();
    }

    /// <summary>
    /// Every concrete node's wire kind in ordinal order, then the seam — the order the file is
    /// written in, so it is stable whatever order reflection hands the types back.
    /// </summary>
    public static IReadOnlyList<string> Kinds() => Kinds(Declared());

    /// <summary>
    /// The same three rules over ANY declaration set, which is the point of the overload: a healthy
    /// assembly cannot exercise a refusal, so the rules would otherwise be code no test reaches. A
    /// test builds the malformed vocabularies the real one is not allowed to have and watches each
    /// one be refused. (A first version of that test read this file looking for the message strings,
    /// which would have passed for a generator whose checks had been deleted and whose prose
    /// remained — review caught it, and it is the same defect this whole PR is about: an instrument
    /// that cannot fail.)
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A node declares no kind, two declare the same one, or a concrete node claims the seam's word.
    /// </exception>
    public static IReadOnlyList<string> Kinds(IEnumerable<(string Node, string Kind)> declared)
    {
        var all = declared.ToList();

        var empty = all.Where(d => d.Kind.Length == 0).Select(d => d.Node).ToList();
        if (empty.Count > 0)
            throw new InvalidOperationException(
                "A node without a wire kind cannot cross to the browser, and would generate '' into "
                + $"the union: {string.Join(", ", empty)}");

        // NOT ESCAPED — REFUSED. A kind ends up in two places that must agree character for
        // character: a quoted member of this union, and a `case '…':` in the browser's lowering. A
        // kind carrying a quote, a backslash or a newline could be escaped into the union and would
        // still be a kind nobody can write a case for — so escaping it would buy a file that
        // compiles and a dispatch that never matches. Every kind the vocabulary has is a plain
        // camelCase identifier, which is the only shape that works on both sides; anything else is
        // a mistake at the declaration, and this says so there instead of as a TypeScript syntax
        // error two build steps away.
        var unwritable = all.Where(d => d.Kind.Length > 0 && !IsWritableKind(d.Kind))
            .Select(d => $"{d.Node}: '{d.Kind}'")
            .ToList();
        if (unwritable.Count > 0)
            throw new InvalidOperationException(
                "A wire kind has to be a plain camelCase identifier — it is quoted into this union "
                + "AND written as a case in the browser's lowering, and the two must match exactly: "
                + $"{string.Join(", ", unwritable)}");

        var stolen = all.Where(d => d.Kind == Seam).Select(d => d.Node).ToList();
        if (stolen.Count > 0)
            throw new InvalidOperationException(
                $"\"{Seam}\" is UiComponent's kind — the expansion seam — and belongs to no concrete "
                + $"node: {string.Join(", ", stolen)}");

        var shared = all.GroupBy(d => d.Kind, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(" and ", group.Select(d => d.Node))}")
            .ToList();
        if (shared.Count > 0)
            throw new InvalidOperationException(
                "The wire kind is a node's identity on the client; two nodes on one kind lower as "
                + $"the same thing: {string.Join("; ", shared)}");

        return all.Select(d => d.Kind)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .Append(Seam)
            .ToList();
    }

    /// <summary>A lowercase-initial identifier, and nothing else: the shape every kind in the
    /// vocabulary already has, and the only one a TypeScript literal and a switch case can share.</summary>
    private static bool IsWritableKind(string kind) =>
        char.IsAsciiLetterLower(kind[0]) && kind.All(char.IsAsciiLetterOrDigit);

    /// <summary>What the vocabulary actually declares, read off the assembly.</summary>
    public static IEnumerable<(string Node, string Kind)> Declared() =>
        Vocabulary().Select(node => (node.Name, Kind: WireKind(node)));

    /// <summary>The vocabulary, asked of the assembly rather than listed.</summary>
    public static IEnumerable<Type> Vocabulary() =>
        typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(VisualNode).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal);

    /// <summary>
    /// The wire kind a node declares, read off an instance the constructor never ran on: every
    /// <c>NodeKind</c> in the vocabulary is a constant expression, so it needs no fields, and asking
    /// the type this way is what lets this stay reflection-driven instead of keeping a hand list of
    /// forty strings beside the forty classes.
    /// </summary>
    private static string WireKind(Type node) =>
        ((VisualNode)RuntimeHelpers.GetUninitializedObject(node)).NodeKind;
}
