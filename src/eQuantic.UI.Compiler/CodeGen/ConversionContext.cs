using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Context shared between conversion strategies.
/// </summary>
public class ConversionContext
{
    public SemanticModel? SemanticModel { get; set; }
    public required CSharpToJsConverter Converter { get; set; }
    public required SemanticHelper SemanticHelper { get; set; }
    public string? ExpectedType { get; set; }
    public string? CurrentClassName { get; set; }

    /// <summary>
    /// Set while converting an ITERATOR method's body. A C# iterator yields a sequence, and every
    /// sequence in the emitted world is an ARRAY — so the method fills this buffer and returns it,
    /// instead of becoming a JS generator whose result `.length` reads as undefined the moment any
    /// LINQ operator touches it. Laziness is the trade, and it only matters for infinite sequences.
    /// </summary>
    public string? IteratorBuffer { get; set; }
    public HashSet<string> UsedHelpers { get; } = new();

    /// <summary>APP types the conversion itself introduced into the OUTPUT — names that never
    /// appear in the source syntax (an extension call reduced to `NodeExtensions.also(...)`), so
    /// the syntax-walking import collector cannot see them. The emitter unions these into the
    /// per-module import set.</summary>
    public HashSet<string> UsedAppTypes { get; } = new();

    /// <summary>
    /// RUNTIME-PROVIDED names the conversion introduced into the output — same problem as
    /// <see cref="UsedAppTypes"/>, opposite destination. The declarative factory surface is the
    /// case that needs it: `Column(gap: …)` names nothing, and the emitted `UI.column(…)` must
    /// import UI from <c>@equantic/runtime</c>, never from a <c>./UI</c> module that exists in no
    /// project.
    /// </summary>
    public HashSet<string> UsedRuntimeTypes { get; } = new();

    /// <summary>Diagnostics raised during this conversion (unconverted or impossible constructs).</summary>
    public List<ConversionDiagnostic> Diagnostics { get; } = new();

    /// <summary>Track L D3: every resx accessor this conversion rewrote — (id, key, designer
    /// path) triples the CLI drains into per-culture catalogs. Rides the context exactly as
    /// diagnostics do, because the semantic model's compilation is ephemeral per file.</summary>
    public List<Services.ResourceUse> ResourceUses { get; } = new();

    /// <summary>Record a diagnostic anchored at <paramref name="node"/>'s source position.</summary>
    public void Report(SyntaxNode node, ConversionSeverity severity, string code, string message)
    {
        var pos = node.GetLocation().GetLineSpan().StartLinePosition;
        Diagnostics.Add(new ConversionDiagnostic(severity, code, message, pos.Line + 1, pos.Character + 1));
    }

    public void ClearDiagnostics()
    {
        Diagnostics.Clear();
        ResourceUses.Clear();
    }

    /// <summary>
    /// Drops everything that belonged to the PREVIOUS emission. A converter outlives the file it is
    /// converting — the compiler builds one and drives every component through it — and the node
    /// cache is keyed by <c>SyntaxNode</c>, so every entry keeps a whole syntax tree reachable for as
    /// long as the converter lives. Measured over 200 compiles of one real component: 38.4 KB
    /// retained per compile before this, 4.7 KB after. Invisible in a CLI that exits when the build
    /// does, and the difference between a flat and a climbing process in a design host that stays up
    /// all day recompiling on every pause in typing.
    /// <para>
    /// The name sets are cleared for the same reason and not because they were producing wrong
    /// output: <c>GenerateImports</c> already intersects them with the identifiers the emitted body
    /// actually mentions, so a stale entry was dropped there rather than imported.
    /// </para>
    /// </summary>
    public void Reset()
    {
        ClearDiagnostics();
        UsedHelpers.Clear();
        UsedAppTypes.Clear();
        UsedRuntimeTypes.Clear();
        _cache.Clear();
        ExpectedType = null;
        IteratorBuffer = null;
        NullGuardAnswered = false;
    }

    // Cache to avoid reprocessing the same node multiple times. Keyed by SyntaxNode, so an entry
    // keeps its entire tree reachable — see Reset().
    private readonly Dictionary<SyntaxNode, string> _cache = new();

    public string? GetCached(SyntaxNode node)
    {
        return _cache.TryGetValue(node, out var result) ? result : null;
    }

    public void SetCached(SyntaxNode node, string result)
    {
        _cache[node] = result;
    }

    /// <summary>
    /// Set by a strategy whose translation already NAMES its receiver and already answers for a null
    /// one — a helper call rather than a member on the value. A <c>?.</c> in front of that would be
    /// nonsense, so the conditional access steps aside and takes the call as the whole chain.
    /// </summary>
    public bool NullGuardAnswered { get; set; }

    /// <summary>
    /// DESIGN MODE: every node construction is wrapped so it carries the source span that built it
    /// (<c>VisualNode.Origin</c>), which is what lets a visual editor answer "which C# made this
    /// pixel". Off by default and never on in an SDK build — the wrapper is real emitted code, and
    /// shipping it would put a design tool's concern in every user's bundle.
    /// </summary>
    public bool DesignMode { get; set; }

    /// <summary>
    /// Whether the output is TYPESCRIPT. Off by default, because the same converter also produces
    /// plain `.mjs` — the conformance harness EXECUTES the emission, and `x: number` is a parse
    /// error there rather than a type. The module emitters turn it on; nothing else may.
    /// </summary>
    public bool TypeAnnotations { get; set; }
}
