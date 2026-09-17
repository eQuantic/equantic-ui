using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every node in the vocabulary is accounted for by every dispatch that walks it, or it is named
/// here as a deliberate absence.
///
/// <para>
/// The SDK has ONE visual vocabulary and several places that decide what each word means — how big
/// it is, what the GPU draws, what a screen reader says, what DOM the server writes, what DOM the
/// browser writes, what an email client is allowed to see. Each was a switch over the node type,
/// each covers a different subset, and until this file nothing made them agree. A deliberate
/// omission and a forgotten one looked identical to a reader and to the build.
/// </para>
///
/// <para>
/// TWO REMAIN, and they are the only two this file still checks — the layout engine and the Photon
/// realizer. The other four left because a COMPILER asks them the question instead: the semantics
/// walk, both email alternatives over one shared refusal set and the web realizer are visitors, and
/// the browser's lowering answers to a generated union ending in <c>assertNever</c>, since it has no
/// interface to implement. Nothing below scans those four any more, which is the point — each
/// departure is recorded where its entry used to be. This file retires with the last one; the plan
/// is <c>docs/VOCABULARY-DISPATCH-PLAN.md</c>.
/// </para>
///
/// <para>
/// That is not a hypothetical. Seven defects arrived through this gap, and only two were found here:
/// <c>Canvas.Label</c> silent to VoiceOver (found by a consumer counting accessibility elements),
/// <c>Image.Label</c> emitting nothing on Photon while the web had carried <c>alt</c> all along,
/// <c>Vector</c>/<c>Drawing</c>/<c>CameraPreview</c> the same (all three in one sweep, once the
/// question was asked of the ASSEMBLY instead of a reader's memory), <c>Text.Align</c> dropped by
/// all three native text services for months, <c>VisualNode.Key</c> documented as reconciler
/// identity since the vocabulary existed and read by neither realizer — and, found the day this pin
/// grew to cover the web realizer, <c>CodeSurface</c> and <c>SheetSurface</c> lowering to an EMPTY
/// span on the server since the day each shipped, while the browser draws both.
/// </para>
///
/// <para>
/// The transpiler solves this same problem — dispatch over an open set of constructs, two
/// implementations that must agree — with a registry and a coverage suite that enumerates by
/// reflection against a baseline that may only shrink. CLAUDE.md names the transpiler as the bar;
/// this is that bar applied to the half of the SDK where the vocabulary actually lives.
/// </para>
///
/// <para>
/// WHY SOURCE AND NOT REFLECTION: a switch arm is not a member, so there is nothing to reflect over.
/// So each dispatch names the ONE method that answers its question, that method's body is cut out
/// of the file (braces matched, comments and strings removed), and the node has to appear inside it
/// in a shape C# uses to dispatch on a type — <c>case Text t:</c>, <c>Text t =&gt;</c>, the discard
/// <c>Spacer =&gt;</c>, or an <c>if</c> whose head is <c>node.Source is ScrollView s</c>.
/// Never a bare name, and never an arm in some OTHER method
/// of the same file: the first version of this matcher read the whole file, so a node answered in
/// <c>MinContentWidth</c> counted as measured by <c>MeasureCore</c>, and an <c>Overlay</c> the engine
/// does size sat in an exemption list saying it never got there. Review caught both.
/// </para>
///
/// <para>
/// THIS IS THE INSTRUMENT, NOT THE FIX. A regex over source is what a closed hierarchy dispatched
/// from outside its own assembly can be held to TODAY; the structural answer is the one each
/// language already has — a visitor whose methods are abstract, so a node added to the vocabulary
/// is a compile error in every realizer until it is handled or explicitly declined, and on the
/// TypeScript side a generated <c>NodeKind</c> union ending in <c>assertNever</c>, since the browser
/// has no interface to implement. Four dispatches down, two to go. Until the last one, this is
/// what keeps the seven from becoming eight.
/// See <c>docs/ARCHITECTURE-AUDIT.md</c>.
/// </para>
/// </summary>
public class VocabularyCoverageTests
{
    /// <summary>The vocabulary, asked of the assembly rather than listed.</summary>
    private static IReadOnlyList<Type> Vocabulary() =>
        typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(VisualNode).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();

    private static readonly string Root = RepositoryRoot();

    /// <summary>
    /// A dispatch: the file, the ONE method in it that answers the question, and the nodes that
    /// method is allowed not to know about. An exemption needs its reason beside it, in the same
    /// commit; <see cref="NoExemptionOutlivesTheGapItDescribes"/> takes it out the moment the node
    /// is handled. Nothing here checks that a list never GROWS — nothing can; that is what review
    /// is for, and why every entry carries its reason where a reviewer reads it.
    /// </summary>
    private sealed record Dispatch(
        string Name, string Path, string Method, string Question, params string[] Exempt);

    private static readonly Dispatch[] Dispatches =
    [
        new("LayoutEngine",
            "src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs",
            "MeasureCore",
            "how big is it, and where",
            // Navigable is a web-only keyboard container today, and WebFrame is the DOM escape
            // hatch, which cannot cross at all. Overlay is NOT here: the engine does size it — to
            // zero in the page flow — and the realizer lays its child out against the viewport in
            // the overlay pass; the first version of this list said it never reached the engine.
            "Navigable", "WebFrame"),

        new("PhotonRealizer",
            "src/eQuantic.UI.Native.Components/PhotonRealizer.cs",
            "EmitNode",
            "what does the GPU draw",
            // The realizer paints LAID-OUT nodes. Everything the layout pass already resolved into
            // geometry arrives as a rectangle with children, so containers and positioners have
            // nothing left to draw: Stack, Grid, Flexible, Spacer, Positioned, Pinned, SafeArea,
            // InFlow, and an AdaptiveNode that IS its resolved variant by the time paint runs. Row
            // and Column are not here — a flex container with a background is chrome, and the
            // `FlexNode` arm paints it. Anchored and Overlay are not here either: EmitNode meets
            // both and QUEUES them for the overlay pass, which is handling. Navigable and WebFrame
            // are the web's.
            "AdaptiveNode", "Flexible", "Grid", "InFlow", "Navigable", "Pinned", "Positioned",
            "SafeArea", "Spacer", "Stack", "WebFrame"),

        // `Semantics` LEFT THIS LIST, and that is the shape the rest are headed for. The semantics
        // walk is a visitor now (`SemanticsVisitor`, one method per node, no default arm), so the
        // question this pin asks of it — "is every node accounted for?" — is asked by the COMPILER,
        // and the reasons that lived in an exemption array are constants named for them, returned
        // by the arm the compiler now demands for every node. A regex over source cannot be wrong
        // about a dispatch that no longer has a switch.

        // `WebRealizer` LEFT THIS LIST, and it was the one with an exemption that had already
        // changed its reason once. `WebLoweringVisitor` answers for all forty nodes, so `CodeSurface`
        // is not a string in an array any more — it is a method that returns null and says, where
        // the node is, that the client appends a caret the server has no business rendering. The
        // difference is that the compiler now knows it is the ONLY node that may.

        // `EmailRealizer` LEFT THIS LIST TOO, and took the longest exemption array with it. Both
        // email walks are visitors over one shared refusal set (`EmailWalk`), so the thirty-three
        // nodes that used to be strings here are methods the compiler demands — and the plain-text
        // walk, which had NO default arm and skipped what it did not know in silence, now refuses
        // exactly what the HTML refuses. `EmailRefusalParityTests` sends every one of the 33 through
        // both alternatives and expects the same reason from each: the half a source regex could
        // never check.

        // `lowering.ts` LEFT THIS LIST, and it is the departure that changes what this file IS. The
        // browser's dispatch is keyed by wire kind, so a regex over `case 'text':` was the only
        // instrument available — until the kind became a GENERATED union (`node-kinds.generated.ts`)
        // and `lowerNodeKind` ended in `assertNever`. A kind with no case now fails `tsc`, which is
        // the runtime's own build, and that is a stronger check than this file could ever be: it
        // fails where the code is written rather than where somebody remembered to look.
        // `EveryNode_DeclaresItsOwnWireKind` went with it, into `NodeKindTsGenerator` — the three
        // rules it held are what the generator refuses to emit a union without.
    ];

    public static IEnumerable<object[]> EveryDispatch() => Dispatches.Select(d => new object[] { d.Name });

    /// <summary>
    /// THE GUARD: a node enters the vocabulary and a dispatch does not know about it. Without this
    /// the switch stays silent and so, in five of the seven cases above, did the application.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryDispatch))]
    public void EveryNode_IsHandledOrNamedAsAnAbsence(string dispatchName)
    {
        var dispatch = Dispatches.Single(d => d.Name == dispatchName);
        var body = BodyOf(dispatch);
        var vocabulary = Vocabulary();
        vocabulary.Should().HaveCountGreaterThan(20, "the vocabulary has to be real for this to mean anything");

        var unaccounted = vocabulary
            .Where(node => !Handles(body, node) && !dispatch.Exempt.Contains(node.Name))
            .Select(node => node.Name)
            .ToArray();

        unaccounted.Should().BeEmpty(
            $"{dispatch.Name}.{dispatch.Method} decides {dispatch.Question} and these nodes reach it "
            + $"with no case of their own. Handle them, or add them to that dispatch's Exempt list "
            + $"with the reason. Unaccounted: {string.Join(", ", unaccounted)}");
    }

    /// <summary>
    /// The other direction, and the half an exemption list cannot do without: an exemption that has
    /// stopped being true. A node that is now handled must come OUT of the list, or the list
    /// slowly becomes a record of what somebody once believed.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryDispatch))]
    public void NoExemptionOutlivesTheGapItDescribes(string dispatchName)
    {
        var dispatch = Dispatches.Single(d => d.Name == dispatchName);
        var body = BodyOf(dispatch);
        var byName = Vocabulary().ToDictionary(t => t.Name);

        var stale = dispatch.Exempt
            .Where(name => byName.TryGetValue(name, out var node) && Handles(body, node))
            .ToArray();

        stale.Should().BeEmpty(
            $"{dispatch.Name}.{dispatch.Method} now handles these, so they are no longer absences. "
            + $"Remove them from its Exempt list: {string.Join(", ", stale)}");
    }

    /// <summary>An exemption naming a node that no longer exists is the same rot from the other end.</summary>
    [Fact]
    public void NoExemptionNamesANodeThatIsGone()
    {
        var vocabulary = Vocabulary().Select(t => t.Name).ToHashSet();
        var ghosts = Dispatches
            .SelectMany(d => d.Exempt.Select(node => (d.Name, node)))
            .Where(e => !vocabulary.Contains(e.node))
            .Select(e => $"{e.Name}: {e.node}")
            .ToArray();

        ghosts.Should().BeEmpty("an exemption for a node the vocabulary no longer has is dead prose");
    }

    /// <summary>
    /// The instrument, checked against itself: every dispatch's method must be FOUND, and its body
    /// must be a real switch and not an empty pair of braces — a matcher pointed at the wrong method
    /// would report every node handled or none, and either is a pass for the wrong reason.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryDispatch))]
    public void TheDispatchMethod_IsWhereTheMatcherLooks(string dispatchName)
    {
        var dispatch = Dispatches.Single(d => d.Name == dispatchName);
        var body = BodyOf(dispatch);

        body.Length.Should().BeGreaterThan(200, $"{dispatch.Method} is a dispatch over the vocabulary, not a stub");
        Vocabulary().Count(node => Handles(body, node))
            .Should().BeGreaterThan(3, $"{dispatch.Method} handles nodes; a body handling none means the wrong method was cut out");
    }

    // ---- reading a dispatch -----------------------------------------------------------------------

    /// <summary>
    /// The body of the ONE method the dispatch names — cut out by matching braces from the method's
    /// signature, with comments and string literals blanked so a node named in prose or in a
    /// diagnostic message cannot pass for an arm. The method may be block-bodied or an expression
    /// body ending in a <c>switch</c>; both open with the first brace after the signature.
    /// </summary>
    private static string BodyOf(Dispatch dispatch)
    {
        var source = File.ReadAllText(Path.Combine(Root, dispatch.Path));
        // A declaration, not a call statement.
        var signature = new Regex($@"\b{Regex.Escape(dispatch.Method)}\((?![^)]*\)\s*;)");
        var start = signature.Match(source);
        start.Success.Should().BeTrue($"{dispatch.Path} declares {dispatch.Method}; the dispatch moved or was renamed");

        var open = source.IndexOf('{', start.Index);
        open.Should().BeGreaterThan(-1);

        var depth = 0;
        var blanked = new StringBuilder();
        for (var i = open; i < source.Length; i++)
        {
            var c = source[i];

            // Comments never count.
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                blanked.Append('\n');
                continue;
            }
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 1;
                blanked.Append(' ');
                continue;
            }

            // Strings: their braces must not unbalance the cut, and their WORDS must not count —
            // a diagnostic message saying "is Box" is prose, not an arm.
            if (c is '"' or '\'' or '`')
            {
                var quote = c;
                i++;
                while (i < source.Length && source[i] != quote)
                {
                    if (source[i] == '\\') i++;
                    i++;
                }
                blanked.Append(' ');
                continue;
            }

            blanked.Append(c);
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return blanked.ToString();
        }

        throw new InvalidOperationException($"{dispatch.Method} in {dispatch.Path} never closes its braces");
    }

    /// <summary>
    /// A node is HANDLED when it appears in the body in a shape C# uses to dispatch on a type — or
    /// when a base of it does: an arm for <c>FlexNode</c> handles a <c>Row</c>, because that is
    /// what the arm is for.
    /// </summary>
    private static bool Handles(string body, Type node) =>
        SelfAndBases(node).Any(name => HandlesCSharp(body, name));

    private static bool HandlesCSharp(string body, string node)
    {
        // The name may be QUALIFIED at the dispatch: `case Primitives.Image` is how the semantics
        // walk writes it, because `Image` collides with a type in scope there. The first version of
        // this matcher missed that and reported a node as unhandled three lines above its own case —
        // the instrument was wrong before the code was, which is the usual order.
        var name = $@"(?:\w+\.)*{Regex.Escape(node)}";
        return Regex.IsMatch(body, $@"\bcase {name}\b")                        // case Text t:  case Text:  case Text { … }:
            || Regex.IsMatch(body, $@"\b{name}(?: [a-z]\w*)? =>")              // Text t =>   and the discard  Spacer =>
            || Regex.IsMatch(body, $@"\bif \(\w+(?:\.\w+)* is \(?{name}\b")  // if (node.Source is ScrollView s)   if (x is (DragDismiss or …)
            || Regex.IsMatch(body, $@"\bis \([^)]* or {name}\)");             // … is (DragDismiss or Draggable)
    }

    // The `is` shape is deliberately the HEAD of an `if` and nothing else. EmitNode answers for nine
    // nodes that way after its switch, which is handling — but it also reads `node.Source is Box ob`
    // in a ternary and `node.Source is Pressable { … }` in a guard BEFORE the switch, which is not:
    // those read one property of a node whose arm is elsewhere, and counting them would let the arm
    // for Pressable disappear while the guard kept it "handled". Review caught that on the version
    // that accepted any `is`.

    /// <summary>The node's own name, then every base strictly below <see cref="VisualNode"/>.</summary>
    private static IEnumerable<string> SelfAndBases(Type node)
    {
        for (var type = node; type is not null && type != typeof(VisualNode) && type != typeof(object); type = type.BaseType)
            yield return type.Name;
    }

    /// <summary>
    /// The wire kind a node declares, read off an instance the constructor never ran on: every
    /// <c>NodeKind</c> in the vocabulary is a constant expression, so it needs no fields, and asking
    /// the type this way is what lets the pin stay reflection-driven instead of keeping a hand list
    /// of thirty-nine strings beside the thirty-nine classes.
    /// </summary>
    private static string WireKind(Type node) =>
        ((VisualNode)RuntimeHelpers.GetUninitializedObject(node)).NodeKind;

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the pin reads the engine's source, so it has to find the tree");
        return dir!.FullName;
    }
}
