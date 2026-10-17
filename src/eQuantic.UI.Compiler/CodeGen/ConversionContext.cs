using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Context shared between conversion strategies.
/// </summary>
public class ConversionContext
{
    public SemanticModel? SemanticModel { get; set; }
    public required CSharpToJsConverter Converter { get; set; }
    public required SemanticHelper SemanticHelper { get; set; }

    /// <summary>
    /// The expression the dispatcher is converting, innermost first: where a fence deep inside a
    /// helper reports, a twin's zero or an operator's home among them, which are handed no node of
    /// their own.
    /// </summary>
    internal SyntaxNode? Converting { get; set; }

    /// <summary>
    /// Converts with <paramref name="at"/> as the node a fence reports at (<see cref="Converting"/>),
    /// where a statement converts a value of its own and no expression of it is being converted: a
    /// <c>foreach</c>'s element and a deconstruction's part. A refused conversion there was told at
    /// whatever expression enclosed the statement, or not at all.
    /// </summary>
    internal T ConvertingAt<T>(SyntaxNode at, Func<T> convert)
    {
        var outer = Converting;
        Converting = at;
        try
        {
            return convert();
        }
        finally
        {
            Converting = outer;
        }
    }

    /// <summary>
    /// The host PROMISED the semantic model is complete — the project's real references and
    /// generated sources are in it (the SDK build, the design session). Under that promise, an
    /// in-tree call the model cannot bind is a build error (EQ2006), never a name-guessed
    /// translation: it means missing references or code that doesn't compile, and guessing there
    /// is how `List.Add` once shipped as a JS `.add`. Left false (standalone/minimal-model hosts,
    /// bare snippets), the name heuristics keep deciding exactly as they always have — that mode
    /// resolves almost nothing by construction, so "unbound" carries no signal there.
    /// </summary>
    public bool SymbolsAreAuthoritative { get; set; }

    /// <summary>
    /// Whether the host handed over the project's compilation, which the module rule reads a class's
    /// chain of bases from (<see cref="Services.PlainClassModule"/>): there an exception class of the
    /// app's has a module of its own, and is built as the class it is (#611). A host with none keeps it
    /// out by its base's name, so nothing writes its twin, and a construction that named it met a class
    /// nothing declares (<see cref="Strategies.ExceptionTypes.HasTwin(ITypeSymbol?, ConversionContext)"/>).
    /// It describes the host, as <see cref="SymbolsAreAuthoritative"/> does, though the two differ: a
    /// library transpiled from its own sources has the compilation, and symbols that are not
    /// authoritative.
    /// </summary>
    internal bool HasProjectCompilation => ProjectCompilation is not null;

    /// <summary>
    /// The project's compilation, when the host handed one over: the app's, which references each
    /// project the app references as a compilation of its own. A question about the whole build is
    /// asked of it, never of <see cref="SemanticModel"/>'s, which is the file's own (its tree replaces
    /// the project's): no other file's symbols compare equal to that one's, and a file of a referenced
    /// project sees nothing of the app (<see cref="Extensions.ServerOnlyContracts"/>). It describes the
    /// host and survives <see cref="Reset"/>.
    /// </summary>
    internal Compilation? ProjectCompilation { get; set; }

    /// <summary>
    /// The catalogue ids of the build, when it compiles the projects the app references beside the
    /// app: one answer for every file, where <see cref="Services.ResourceClasses.IdFor"/> answers for
    /// the one compilation a file is in (#624). Null for a host with a single compilation, which
    /// that answer is right for. It describes the host and survives <see cref="Reset"/>.
    /// </summary>
    internal Services.ResourceCatalogIds? ResourceIds { get; set; }

    /// <summary>The catalogue id a read of <paramref name="resourceClass"/> is written with, which
    /// the catalogs are keyed by.</summary>
    internal string CatalogIdOf(INamedTypeSymbol resourceClass) =>
        ResourceIds?.IdFor(resourceClass) ?? Services.ResourceClasses.IdFor(SemanticModel!.Compilation, resourceClass);

    /// <summary>
    /// Whether a name heuristic is an HONEST basis for translating this node: the model cannot be
    /// asked (none, or a strategy-rewrote node from no tree it knows), or the host never promised
    /// the model was complete. When this is false, symbols are the only evidence that counts.
    /// </summary>
    public bool CanGuess(SyntaxNode node) => !SymbolsAreAuthoritative || !SemanticHelper.KnowsOrMapped(node);

    /// <summary>
    /// The gate the static-surface strategies share: whether a member access's RECEIVER is the
    /// intended TYPE — by symbol where the model can answer (so a user class that merely SHARES
    /// the name never routes here), by text only where guessing is honest. In-tree but unbound
    /// under an authoritative model is evidence AGAINST, exactly as everywhere else.
    /// </summary>
    public bool ReceiverIsType(ExpressionSyntax receiver,
        Func<INamedTypeSymbol, bool> matches, params string[] textualNames)
    {
        if (SemanticHelper.KnowsOrMapped(receiver))
        {
            var symbol = SemanticHelper.GetSymbol(receiver);
            if (symbol is INamedTypeSymbol named) return matches(named);
            if (symbol is not null) return false;
            if (!CanGuess(receiver)) return false;
        }

        return textualNames.Contains(receiver.ToString());
    }

    /// <summary>
    /// The gate the LINQ-family strategies share: the method NAME matches, and either the symbol
    /// proves it is <c>System.Linq</c> or guessing is honest here (<see cref="CanGuess"/>).
    /// </summary>
    public bool IsLinqMethod(SyntaxNode node, string methodName)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.ValueText != methodName) return false;

        if (SemanticHelper.GetSymbol(node) is { } symbol)
            return SemanticHelper.IsLinqExtension(symbol.ContainingType);
        return CanGuess(node);
    }
    public string? ExpectedType { get; set; }
    public string? CurrentClassName { get; set; }

    /// <summary>How statements are laid out — see <see cref="JsLayout"/>.</summary>
    public JsLayout Layout { get; set; } = JsLayout.Pretty;

    /// <summary>
    /// The block depth being converted, maintained by the converter while it descends. A statement
    /// strategy still producing TEXT renders any nested block eagerly, through the string API, and
    /// that render has to land at the depth the block will later be printed at — which is exactly
    /// this, because a block is always printed where it was built.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>The temporaries a lowering binds in the function its C# is written in, declared by
    /// the statement or the concise body that holds it (#539).</summary>
    internal Temporaries Temporaries { get; } = new();

    /// <summary>What each pattern-matching operation holds the parts of a <c>Deconstruct</c> the app
    /// wrote in, once per value, declared by the operation itself.</summary>
    internal MatchParts MatchParts { get; } = new();

    /// <summary>
    /// Set while converting an ITERATOR method's body. A C# iterator yields a sequence, and every
    /// sequence in the emitted world is an ARRAY — so the method fills this buffer and returns it,
    /// instead of becoming a JS generator whose result `.length` reads as undefined the moment any
    /// LINQ operator touches it. Laziness is the trade, and it only matters for infinite sequences.
    /// </summary>
    public string? IteratorBuffer { get; set; }

    /// <summary>
    /// Set while converting a twin constructor's parameter defaults, where a record's
    /// primary-constructor parameter IS the constructor's own JavaScript parameter: a field
    /// initializer that reads it (<c>public string Tag = "#" + Id;</c>) runs there, before any
    /// member is assigned. Everywhere else such a parameter reads as captured state,
    /// <c>this.id</c>, which in that position was still undefined (#385).
    /// </summary>
    public bool ConstructorParametersInScope { get; set; }
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

    /// <summary>Type receivers known by the source-directory fallback. These sets are populated by
    /// the emitter from <see cref="ComponentDependencyResolver"/> so expression strategies do not
    /// misclassify a known static/runtime type as an instance member or enum solely by shape.</summary>
    public IReadOnlySet<string> FallbackStaticTypes { get; set; } = new HashSet<string>();
    public IReadOnlySet<string> FallbackRuntimeTypes { get; set; } = new HashSet<string>();

    public bool IsFallbackTypeReceiver(string name) =>
        FallbackStaticTypes.Contains(name) || FallbackRuntimeTypes.Contains(name);

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
        // The same diagnostic at the same place says nothing new: an emitter that asks for a field's
        // zero for its declaration and again for its constructor met the same fence twice (#584).
        var diagnostic = new ConversionDiagnostic(severity, code, message, pos.Line + 1, pos.Character + 1);
        if (!Diagnostics.Contains(diagnostic)) Diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// A strategy CLAIMED this node and then met a form it has no emission for. The verbatim C# is
    /// returned so the output shows what was meant, and the build FAILS — these tails used to return
    /// the raw text with no diagnostic at all, the one path left that could still ship C# to the
    /// browser after EQ1001–EQ1003 closed the no-strategy case.
    /// </summary>
    public string Unhandled(SyntaxNode node, string strategy)
    {
        var text = node.ToString();
        var shown = text.Length > 80 ? text[..77] + "…" : text;
        Report(node, ConversionSeverity.Error, "EQ1004",
            $"'{shown}' matched the {strategy} translation, but this exact form has no JavaScript "
            + "emission. Rewrite it in a transpilable form, or add the missing case to the strategy.");
        return text;
    }

    public void ClearDiagnostics()
    {
        Diagnostics.Clear();
        ResourceUses.Clear();
        RefusedTwins.Clear();
        RefusedReads.Clear();
        Refused.Clear();
        InterpolationCultures.Clear();
    }

    /// <summary>
    /// The twins a fence refused while this module was converted (#584): the module's import net,
    /// which reports a twin the build writes no module for, says nothing more of one already said
    /// where the code named it. Kept with the diagnostics, and cleared with them.
    /// </summary>
    internal HashSet<string> RefusedTwins { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The in-tree reads <c>PlatformMemberRead</c> refused while this module was converted (#847): a
    /// read made on one of them is the same refusal, and says nothing more. Kept with the diagnostics,
    /// and cleared with them.
    /// </summary>
    internal HashSet<SyntaxNode> RefusedReads { get; } = new();

    /// <summary>
    /// The in-tree nodes the client boundary refused while this module was converted
    /// (<see cref="Extensions.HostOnlySymbolExtensions"/>): a member read off a value whose expression
    /// holds one of them is the same refusal, and says nothing more. Kept with the diagnostics, and
    /// cleared with them.
    /// </summary>
    internal HashSet<SyntaxNode> Refused { get; } = new();

    /// <summary>
    /// The interpolations written in a culture a call names (#839), by whether it is the invariant one:
    /// <c>FormattableString.Invariant($"…")</c>'s and <c>string.Create(provider, $"…")</c>'s, whose holes
    /// <c>InterpolatedStringStrategy</c> and <c>ValueFlow</c> write in that culture. An interpolation made
    /// a <c>FormattableString</c> anywhere else is refused: the browser holds the text it writes, not its
    /// format and its arguments. Cleared with the diagnostics.
    /// </summary>
    internal Dictionary<SyntaxNode, bool> InterpolationCultures { get; } = new();

    /// <summary>Whether the interpolation <paramref name="interpolated"/> writes its holes in the invariant
    /// culture, as a call that names it asked.</summary>
    internal bool WritesInvariant(SyntaxNode? interpolated) =>
        interpolated is not null && InterpolationCultures.TryGetValue(SemanticHelper.Original(interpolated), out var invariant) && invariant;

    /// <summary>Whether the client boundary refused a node inside <paramref name="expression"/> already.</summary>
    internal bool RefusedWithin(SyntaxNode expression) =>
        Refused.Any(refused => refused.SyntaxTree == expression.SyntaxTree && expression.Span.Contains(refused.Span));

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
        SemanticHelper.ClearSynthetics();
        UsedHelpers.Clear();
        UsedAppTypes.Clear();
        UsedRuntimeTypes.Clear();
        _cache.Clear();
        Temporaries.Reset();
        ExpectedType = null;
        IteratorBuffer = null;
        Depth = 0;
    }

    // Cache to avoid reprocessing the same node multiple times. Keyed by SyntaxNode, so an entry
    // keeps its entire tree reachable — see Reset(). It holds IR rather than text: a node reached
    // once as an operand and once on its own must be the same expression both times, and only the
    // IR remembers how tightly it binds.
    private readonly Dictionary<SyntaxNode, JsExpr> _cache = new();

    public JsExpr? GetCached(SyntaxNode node)
    {
        return _cache.TryGetValue(node, out var result) ? result : null;
    }

    public void SetCached(SyntaxNode node, JsExpr result)
    {
        _cache[node] = result;
    }

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
