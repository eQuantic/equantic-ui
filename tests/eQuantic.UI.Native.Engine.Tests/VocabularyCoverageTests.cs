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
/// FOUR REMAIN. Two of those six — the semantics walk and the email realizer, the second of them
/// carrying the plain-text alternative that had no default arm at all — are visitors now, so the
/// question below is asked of them by the COMPILER and they left this list. Each departure is
/// recorded where its entry used to be. This file retires with the last one; the plan is
/// <c>docs/VOCABULARY-DISPATCH-PLAN.md</c>.
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
/// <c>Spacer =&gt;</c>, or an <c>if</c> whose head is <c>node.Source is ScrollView s</c> — or, for the
/// TypeScript twin, as <c>case 'text':</c>
/// over the WIRE KIND the C# node declares. Never a bare name, and never an arm in some OTHER method
/// of the same file: the first version of this matcher read the whole file, so a node answered in
/// <c>MinContentWidth</c> counted as measured by <c>MeasureCore</c>, and an <c>Overlay</c> the engine
/// does size sat in an exemption list saying it never got there. Review caught both.
/// </para>
///
/// <para>
/// THIS IS THE INSTRUMENT, NOT THE FIX. A regex over source is what a closed hierarchy dispatched
/// from outside its own assembly can be held to TODAY; the structural answer is the one the language
/// already has — a visitor whose methods are abstract, so a node added to the vocabulary is a
/// compile error in every realizer until it is handled or explicitly declined, and a generated
/// <c>NodeKind</c> union with an exhaustive switch on the TypeScript side — and that answer is now
/// landing, two dispatches down. Until the last one, this is what keeps the seven from becoming
/// eight.
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

    private enum Language
    {
        /// <summary>Type patterns over the C# node classes.</summary>
        CSharp,

        /// <summary>String cases over the wire kind each C# node declares in <see cref="VisualNode.NodeKind"/>.</summary>
        TypeScript,
    }

    /// <summary>
    /// A dispatch: the file, the ONE method in it that answers the question, and the nodes that
    /// method is allowed not to know about. An exemption needs its reason beside it, in the same
    /// commit; <see cref="NoExemptionOutlivesTheGapItDescribes"/> takes it out the moment the node
    /// is handled. Nothing here checks that a list never GROWS — nothing can; that is what review
    /// is for, and why every entry carries its reason where a reviewer reads it.
    /// </summary>
    private sealed record Dispatch(
        string Name, string Path, string Method, string Question, Language Language, params string[] Exempt);

    private static readonly Dispatch[] Dispatches =
    [
        new("LayoutEngine",
            "src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs",
            "MeasureCore",
            "how big is it, and where",
            Language.CSharp,
            // Navigable is a web-only keyboard container today, and WebFrame is the DOM escape
            // hatch, which cannot cross at all. Overlay is NOT here: the engine does size it — to
            // zero in the page flow — and the realizer lays its child out against the viewport in
            // the overlay pass; the first version of this list said it never reached the engine.
            "Navigable", "WebFrame"),

        new("PhotonRealizer",
            "src/eQuantic.UI.Native.Components/PhotonRealizer.cs",
            "EmitNode",
            "what does the GPU draw",
            Language.CSharp,
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

        new("WebRealizer",
            "src/eQuantic.UI.Web/WebRealizer.cs",
            "LowerNodeKind",
            "what DOM does the server write",
            Language.CSharp,
            // `SheetSurface` left this list when the server learned to write it. `CodeSurface` is
            // still here, and the reason CHANGED rather than survived: it is no longer "nobody
            // noticed", it is that the client appends a caret to every code surface and the server
            // has no business rendering a caret. Emitting only the child would give hydration a tree
            // one element short, which the reconciler records as a failed adoption — so the shape
            // has to be settled before the arm is worth having, and settling it needs a running
            // page rather than a guess. `SurfaceSsrTests` holds the half that is done.
            "CodeSurface"),

        // `EmailRealizer` LEFT THIS LIST TOO, and took the longest exemption array with it. Both
        // email walks are visitors over one shared refusal set (`EmailWalk`), so the thirty-three
        // nodes that used to be strings here are methods the compiler demands — and the plain-text
        // walk, which had NO default arm and skipped what it did not know in silence, now refuses
        // exactly what the HTML refuses. `EmailRefusalParityTests` sends every one of the 33 through
        // both alternatives and expects the same reason from each: the half a source regex could
        // never check.

        new("lowering.ts",
            "src/eQuantic.UI.Runtime/src/shared/lowering.ts",
            "lowerNodeKind",
            "what DOM does the browser write",
            Language.TypeScript
            // The client twin of WebRealizer, keyed by wire kind rather than by type. It covers
            // the whole vocabulary — including the two surfaces the server drops — and this line
            // is what makes that a fact the build knows rather than a count somebody took once.
            ),
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
            .Where(node => !Handles(body, node, dispatch.Language) && !dispatch.Exempt.Contains(node.Name))
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
            .Where(name => byName.TryGetValue(name, out var node) && Handles(body, node, dispatch.Language))
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
    /// The TypeScript door is keyed by <see cref="VisualNode.NodeKind"/>, so that key has to be a
    /// real identity: one kind per concrete node, no two nodes sharing one. Two nodes on the same
    /// kind would let the TypeScript pin pass for a node the browser has never heard of.
    /// </summary>
    [Fact]
    public void EveryNode_DeclaresItsOwnWireKind()
    {
        var kinds = Vocabulary().Select(node => (node.Name, Kind: WireKind(node))).ToArray();

        kinds.Should().OnlyContain(k => k.Kind.Length > 0, "a node without a wire kind cannot cross to the browser");
        kinds.Select(k => k.Kind).Should().OnlyHaveUniqueItems(
            "the wire kind is the node's identity on the client; two nodes on one kind lower as the same thing");
        kinds.Should().OnlyContain(k => k.Kind != "component",
            "\"component\" is UiComponent's kind — the expansion seam — and belongs to no concrete node");
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
        Vocabulary().Count(node => Handles(body, node, dispatch.Language))
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
        var signature = dispatch.Language == Language.TypeScript
            ? new Regex($@"\bfunction {Regex.Escape(dispatch.Method)}\(")
            : new Regex($@"\b{Regex.Escape(dispatch.Method)}\((?![^)]*\)\s*;)"); // a declaration, not a call statement
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

            // Strings: their braces must not unbalance the cut, and in C# their WORDS must not count
            // — a diagnostic message saying "is Box" is prose. In TypeScript the string IS the arm
            // (`case 'box':` dispatches on the wire kind), so there the literal is kept verbatim.
            if (c is '"' or '\'' or '`')
            {
                var quote = c;
                var from = i;
                i++;
                while (i < source.Length && source[i] != quote)
                {
                    if (source[i] == '\\') i++;
                    i++;
                }
                blanked.Append(dispatch.Language == Language.TypeScript ? source[from..Math.Min(i + 1, source.Length)] : " ");
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
    /// what the arm is for. On the TypeScript side the shape is the one string case over its kind.
    /// </summary>
    private static bool Handles(string body, Type node, Language language) => language switch
    {
        Language.TypeScript => Regex.IsMatch(body, $@"\bcase '{Regex.Escape(WireKind(node))}':"),
        _ => SelfAndBases(node).Any(name => HandlesCSharp(body, name)),
    };

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
