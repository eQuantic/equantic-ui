using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Strategies;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;
using eQuantic.UI.Compiler.CodeGen.Strategies.UI;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;
using eQuantic.UI.Compiler.CodeGen.Strategies.Special;
using eQuantic.UI.Compiler.CodeGen.Strategies.Statements;
using eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;
using eQuantic.UI.Compiler.CodeGen.Strategies.Async;   
using eQuantic.UI.Compiler.CodeGen.Registry;

using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Robust C# to JavaScript expression converter using Roslyn AST
/// </summary>
public class CSharpToJsConverter
{
    private readonly StrategyRegistry _strategyRegistry;
    private readonly StatementStrategyRegistry _statementRegistry;
    private readonly ConversionContext _context;
    private SemanticModel? _semanticModel;

    public CSharpToJsConverter()
    {
        _context = new ConversionContext
        { 
            Converter = this,
            SemanticHelper = new SemanticHelper(null)
        };
        _strategyRegistry = new StrategyRegistry();
        _statementRegistry = new StatementStrategyRegistry();

        RegisterStrategies();
    }
    
    /// <summary>The model in force, for an emitter that must ASK what a default refers to.</summary>
    internal SemanticModel? Model => _semanticModel;

    /// <summary>See <see cref="ConversionContext.SymbolsAreAuthoritative"/> — set true by hosts
    /// that hand over the project's COMPLETE compilation (SDK build, design session, test
    /// harnesses that compile their snippets). Survives <see cref="Reset"/>: it describes the
    /// host, not the file.</summary>
    public bool SymbolsAreAuthoritative
    {
        get => _context.SymbolsAreAuthoritative;
        set => _context.SymbolsAreAuthoritative = value;
    }

    public void SetSemanticModel(SemanticModel? semanticModel)
    {
        _semanticModel = semanticModel;
        _context.SemanticModel = semanticModel;
        _context.SemanticHelper = new SemanticHelper(semanticModel);
    }

    public void SetCurrentClass(string? className)
    {
        _context.CurrentClassName = className;
    }

    /// <summary>Names the array an ITERATOR method is filling — null outside one.</summary>
    public void SetIteratorBuffer(string? buffer)
    {
        _context.IteratorBuffer = buffer;
    }

    /// <summary>
    /// Converts an expression that lives in a DIFFERENT syntax tree than the file being compiled —
    /// a referenced constant's initializer, inlined at the use site (e.g. an icon pack's
    /// <c>static readonly IconGlyph</c>). The tree's own semantic model is swapped in for the
    /// conversion and restored after, so cross-assembly initializers resolve exactly as their
    /// declaring compilation would bind them. Single-threaded per file, so save/restore is safe.
    /// </summary>
    public string ConvertExpressionWithModel(ExpressionSyntax expression, SemanticModel model)
    {
        var savedModel = _semanticModel;
        var savedContextModel = _context.SemanticModel;
        var savedHelper = _context.SemanticHelper;
        try
        {
            _semanticModel = model;
            _context.SemanticModel = model;
            _context.SemanticHelper = new SemanticHelper(model);
            return ConvertExpression(expression);
        }
        finally
        {
            _semanticModel = savedModel;
            _context.SemanticModel = savedContextModel;
            _context.SemanticHelper = savedHelper;
        }
    }

    public HashSet<string> UsedHelpers => _context.UsedHelpers;

    /// <summary>The statement layout in force — what the emitter hands its builder.</summary>
    public JsLayout Layout => _context.Layout;

    /// <summary>See <see cref="ConversionContext.UsedAppTypes"/> — output-introduced app-type names.</summary>
    public HashSet<string> UsedAppTypes => _context.UsedAppTypes;

    /// <summary>See <see cref="ConversionContext.UsedRuntimeTypes"/> — output-introduced names the
    /// RUNTIME provides (the declarative factory surface).</summary>
    public HashSet<string> UsedRuntimeTypes => _context.UsedRuntimeTypes;

    /// <summary>Diagnostics raised during the most recent conversion(s); call <see cref="ClearDiagnostics"/> between components.</summary>
    public IReadOnlyList<ConversionDiagnostic> Diagnostics => _context.Diagnostics;

    public void ClearDiagnostics() => _context.ClearDiagnostics();

    /// <summary>See <see cref="ConversionContext.Reset"/> — call between components, not just
    /// between diagnostics.</summary>
    public void Reset() => _context.Reset();

    /// <summary>Resx accessor reads the most recent conversion(s) rewrote (Track L D3) — drained
    /// alongside diagnostics, cleared by the same call.</summary>
    public IReadOnlyList<Services.ResourceUse> ResourceUses => _context.ResourceUses;

    /// <summary>Raises a diagnostic from OUTSIDE a strategy — the emitter's own whole-method checks.</summary>
    /// <summary>Turns TypeScript annotations on — see <see cref="ConversionContext.TypeAnnotations"/>.</summary>
    public void EmitTypeAnnotations(bool enabled) => _context.TypeAnnotations = enabled;

    /// <summary>See <see cref="ConversionContext.DesignMode"/> — off in every build that ships.</summary>
    public void EmitDesignOrigins(bool enabled) => _context.DesignMode = enabled;

    public void Report(SyntaxNode node, ConversionSeverity severity, string code, string message) =>
        _context.Report(node, severity, code, message);

    private void RegisterStrategies()
    {
        // Compile-Time Evaluation Strategy (Highest Priority - 100)
        _strategyRegistry.Register<CompileTimeEvaluatedExpressionStrategy>();

        _strategyRegistry.Register<EnumStrategy>();
        _strategyRegistry.Register<EnumMethodStrategy>();
        _strategyRegistry.Register<EnumHasFlagStrategy>();
        _strategyRegistry.Register<NullableStrategy>();
        _strategyRegistry.Register<StructuralEqualsStrategy>();
        _strategyRegistry.Register<ReferenceEqualsStrategy>();
        _strategyRegistry.Register<EventSubscriptionStrategy>();
        _strategyRegistry.Register<TupleExpressionStrategy>();
        _strategyRegistry.Register<TupleMemberAccessStrategy>();
        _strategyRegistry.Register<CheckedExpressionStrategy>();
        _strategyRegistry.Register<GuidTypeStrategy>();
        _strategyRegistry.Register<NamespaceRemovalStrategy>();
        _strategyRegistry.Register<NameofStrategy>(); // Priority 15
        _strategyRegistry.Register<DefaultKeywordStrategy>(); // Priority 15
        
        // LINQ Strategies
        _strategyRegistry.Register<QueryExpressionStrategy>();
        _strategyRegistry.Register<FirstStrategy>();
        _strategyRegistry.Register<LastStrategy>();
        _strategyRegistry.Register<SingleStrategy>();
        _strategyRegistry.Register<CountStrategy>();
        _strategyRegistry.Register<OrderByStrategy>();
        _strategyRegistry.Register<SkipStrategy>();
        _strategyRegistry.Register<TakeStrategy>();
        _strategyRegistry.Register<DistinctStrategy>();
        _strategyRegistry.Register<ContainsStrategy>();
        _strategyRegistry.Register<SumStrategy>();
        _strategyRegistry.Register<AverageStrategy>();
        _strategyRegistry.Register<MinMaxStrategy>();
        _strategyRegistry.Register<GroupByStrategy>();
        _strategyRegistry.Register<LookupIndexerStrategy>();
        _strategyRegistry.Register<ZipStrategy>();
        _strategyRegistry.Register<CastStrategy>();
        _strategyRegistry.Register<OfTypeStrategy>();
        _strategyRegistry.Register<ElementAtStrategy>();
        _strategyRegistry.Register<SequenceEqualStrategy>();
        _strategyRegistry.Register<DefaultIfEmptyStrategy>();

        // Primitive Type Strategies (Low Priority than new Invocation Strategies but higher than fallback)
        _strategyRegistry.Register<StringMethodStrategy>();
        _strategyRegistry.Register<StringStaticStrategy>(); // New Phase 7
        _strategyRegistry.Register<PrimitiveStaticStrategy>(); // .NET 7+ statics on the primitives themselves
        _strategyRegistry.Register<BclSurfaceTailStrategy>();  // instance tail: Equals/String/Dictionary/List QoL
        _strategyRegistry.Register<ReverseStrategy>();
        _strategyRegistry.Register<LinqTableStrategy>(); // the LINQ surface as a table of shapes
        _strategyRegistry.Register<ListMethodStrategy>();
        _strategyRegistry.Register<ArrayStaticStrategy>();

        // === Invocation Strategies (Priority 10) ===
        _strategyRegistry.Register<ConsoleStrategy>();
        _strategyRegistry.Register<MathStrategy>();
        _strategyRegistry.Register<DictionaryStrategy>();
        _strategyRegistry.Register<ServiceProviderStrategy>();
        _strategyRegistry.Register<ToStringStrategy>();
        _strategyRegistry.Register<CollectionMaterializationStrategy>();

        // === UI Strategies (Priority 10-50) ===
        _strategyRegistry.Register<RuntimeUtilityStrategy>(); // Priority 50 - runtime utilities (ClassBuilder, etc.)
        _strategyRegistry.Register<HtmlNodeStrategy>();
        _strategyRegistry.Register<AddChildStrategy>();

        // === Expression Strategies (Priority 10) ===
        _strategyRegistry.Register<LambdaExpressionStrategy>();
        _strategyRegistry.Register<AwaitExpressionStrategy>();
        _strategyRegistry.Register<UnaryExpressionStrategy>();
        _strategyRegistry.Register<InterpolatedStringStrategy>();
        _strategyRegistry.Register<LiteralExpressionStrategy>();
        _strategyRegistry.Register<ConditionalExpressionStrategy>();
        _strategyRegistry.Register<InitializerExpressionStrategy>();
        _strategyRegistry.Register<ArrayCreationStrategy>();
        _strategyRegistry.Register<IsPatternStrategy>();
        _strategyRegistry.Register<DeclarationExpressionStrategy>();
        _strategyRegistry.Register<CollectionExpressionStrategy>();
        _strategyRegistry.Register<NullCoalescingAssignmentStrategy>(); // Priority 15 - before AssignmentExpressionStrategy
        _strategyRegistry.Register<AssignmentExpressionStrategy>();
        _strategyRegistry.Register<IdentifierStrategy>();
        _strategyRegistry.Register<RangeExpressionStrategy>();
        _strategyRegistry.Register<RangeIndexerStrategy>();
        _strategyRegistry.Register<ThrowExpressionStrategy>();
        _strategyRegistry.Register<StackAllocArrayCreationStrategy>();
        _strategyRegistry.Register<WithExpressionStrategy>();
        _strategyRegistry.Register<TypeofExpressionStrategy>();
        _strategyRegistry.Register<BaseExpressionStrategy>();
        _strategyRegistry.Register<FieldExpressionStrategy>();
        _strategyRegistry.Register<CastExpressionStrategy>();
        _strategyRegistry.Register<AsExpressionStrategy>();
        _strategyRegistry.Register<ParenthesizedExpressionStrategy>();
        _strategyRegistry.Register<SizeOfExpressionStrategy>();
        _strategyRegistry.Register<AnonymousMethodExpressionStrategy>();
        
        // Additional Types
        _strategyRegistry.Register<DateTimeStrategy>();
        _strategyRegistry.Register<TimeSpanStrategy>();
        _strategyRegistry.Register<DateOnlyTimeOnlyStrategy>();
        _strategyRegistry.Register<DateTimeOffsetStrategy>();
        _strategyRegistry.Register<StringBuilderStrategy>();
        _strategyRegistry.Register<QueueStackStrategy>();
        _strategyRegistry.Register<ValueKeyedDictionaryStrategy>(); // Priority 25 - structurally-keyed dictionaries
        _strategyRegistry.Register<SortedDictionaryStrategy>();     // Priority 25 - key-sorted dictionaries
        _strategyRegistry.Register<RegexStrategy>();
        _strategyRegistry.Register<HashSetStrategy>();
        _strategyRegistry.Register<NumericConstantStrategy>();

        // Phase 7: Static Helpers & Async
        _strategyRegistry.Register<TaskMethodStrategy>();
        _strategyRegistry.Register<NumberMethodStrategy>();
        _strategyRegistry.Register<CompareToStrategy>();
        _strategyRegistry.Register<BooleanMethodStrategy>();
        _strategyRegistry.Register<CharMethodStrategy>();
        _strategyRegistry.Register<ConvertStrategy>();
        // StringStaticStrategy is registered in primitives block? Checking order logic.
        // It's a Primitive strategy, so lets check where StringMethodStrategy is.

        // Expression strategies
        // Inlines external IconGlyph constants (pack glyphs) at the use site — must beat the fallback
        // member access, which would emit a broken `LucideIcons.camera`.
        _strategyRegistry.Register<InlinedConstantStrategy>();
        // Track L D2: resx accessors REWRITE to $eq.str — registered above the inliner so a
        // constant-looking accessor can never fall through to a path that bakes a culture in.
        _strategyRegistry.Register<ResourceAccessorStrategy>();
        // Sequences that come from nothing, and .NET names for what the browser already has.
        _strategyRegistry.Register<Strategies.Linq.EnumerableFactoryStrategy>();
        _strategyRegistry.Register<Strategies.Types.WebPrimitiveStrategy>();
        _strategyRegistry.Register<GenericNameStrategy>();
        _strategyRegistry.Register<ThisExpressionStrategy>();
        _strategyRegistry.Register<MemberAccessStrategy>();
        _strategyRegistry.Register<ElementAccessStrategy>();
        _strategyRegistry.Register<ObjectCreationStrategy>();
        _strategyRegistry.Register<AnonymousObjectCreationStrategy>();
        _strategyRegistry.Register<BinaryExpressionStrategy>();
        _strategyRegistry.Register<SwitchExpressionStrategy>();
        _strategyRegistry.Register<NullCoalescingStrategy>();
        _strategyRegistry.Register<ConditionalAccessStrategy>();
        _strategyRegistry.Register<IndexFromEndStrategy>();
        
        // Fallback Strategies (Priority 1)
        _strategyRegistry.Register<InvocationStrategy>();

        // Safety net (Priority 2): genuinely-impossible constructs become a build error (EQ2001)
        // instead of being emitted verbatim.
        _strategyRegistry.Register<UnsupportedConstructStrategy>();

        // Statement Strategies
        _statementRegistry.Register<IfStatementStrategy>();
        _statementRegistry.Register<ForStatementStrategy>();
        _statementRegistry.Register<ForEachStatementStrategy>();
        _statementRegistry.Register<ForEachVariableStatementStrategy>();
        _statementRegistry.Register<ReturnStatementStrategy>();
        _statementRegistry.Register<LocalDeclarationStrategy>();
        _statementRegistry.Register<ExpressionStatementStrategy>();
        _statementRegistry.Register<SwitchStatementStrategy>();
        _statementRegistry.Register<WhileStatementStrategy>();
        _statementRegistry.Register<DoWhileStatementStrategy>();
        _statementRegistry.Register<BreakStatementStrategy>();
        _statementRegistry.Register<ContinueStatementStrategy>();
        _statementRegistry.Register<ThrowStatementStrategy>();
        _statementRegistry.Register<TryStatementStrategy>();
        _statementRegistry.Register<UsingStatementStrategy>();
        _statementRegistry.Register<LockStatementStrategy>();
        _statementRegistry.Register<YieldStatementStrategy>();
        _statementRegistry.Register<FixedStatementStrategy>();
        _statementRegistry.Register<LocalFunctionStatementStrategy>();
        _statementRegistry.Register<CheckedStatementStrategy>();
        _statementRegistry.Register<UnsafeStatementStrategy>();
        _statementRegistry.Register<LabeledStatementStrategy>();
        _statementRegistry.Register<GotoStatementStrategy>();
        _statementRegistry.Register<EmptyStatementStrategy>();
    }

    /// <summary>
    /// Convert a C# expression string to JavaScript
    /// </summary>
    public string Convert(string code)
    {
        // String conversion fallback
        var parsed = CSharpSyntaxTree.ParseText(code, Services.ParseDefaults.Options).GetRoot();
        
        // Check for Global Statements (Top Level)
        var globalStatement = parsed.DescendantNodes().OfType<GlobalStatementSyntax>().FirstOrDefault();
        if (globalStatement != null)
        {
            return ConvertStatement(globalStatement.Statement);
        }

        // Check for specific statements that might be parsed without GlobalStatement wrapper in some contexts?
        // Actually ParseText wraps top-level statements in GlobalStatementSyntax.
        
        // If no global statement, maybe it's just an expression?
        // Note: "x + 1" is parsed as GlobalStatement -> ExpressionStatement -> Expression
        // So the above check handles "x + 1" too if we redirect ExpressionStatement to ConvertExpression?
        // ExpressionStatementStrategy usually handles this.
        
        // Fallback to searching for Block
        var block = parsed.DescendantNodes().OfType<BlockSyntax>().FirstOrDefault();
        if (block != null) return ConvertBlock(block);

        // Fallback to searching for standalone Expression if parsing was partial
        var expr = parsed.DescendantNodes().OfType<ExpressionSyntax>().FirstOrDefault();
        if (expr != null) return ConvertExpression(expr);
        
        return code;
    }

    public string Convert(SyntaxNode node)
    {
        if (node is ExpressionSyntax expr) return ConvertExpression(expr);
        if (node is BlockSyntax block) return ConvertBlock(block);
        if (node is StatementSyntax stmt) return ConvertStatement(stmt);
        _context.Report(node, ConversionSeverity.Error, "EQ1003",
            $"C# node '{node.Kind()}' has no transpilation strategy — it cannot be emitted as JavaScript. " +
            "Rewrite it in a transpilable form, or add a conversion strategy for this construct.");
        return node.ToString();
    }
    
    /// <summary>
    /// Convert a parsed expression to JavaScript with an expected type hint
    /// </summary>
    /// <summary>The expression as JavaScript text, standing on its own.</summary>
    public string ConvertExpression(ExpressionSyntax expression, string? expectedType = null) =>
        JsExprWriter.Write(ConvertIr(expression, expectedType));

    /// <summary>
    /// The expression as IR. A strategy that has crossed over
    /// (<see cref="IExpressionIrStrategy"/>) builds a real node; every other one still returns
    /// text, which is wrapped OPAQUE and spliced verbatim — so an unmigrated strategy's output is
    /// byte-identical to what it was before the IR existed. Callers that place an expression
    /// SOMEWHERE (an operand, a receiver) should ask for this and let
    /// <see cref="JsExprWriter"/> punctuate; callers that just need text can keep calling
    /// <see cref="ConvertExpression(ExpressionSyntax, string?)"/>.
    /// </summary>
    public JsExpr ConvertIr(ExpressionSyntax expression, string? expectedType = null)
    {
        _context.ExpectedType = expectedType;
        var cached = _context.GetCached(expression);
        if (cached != null) return cached;

        var strategy = _strategyRegistry.FindStrategy(expression, _context);
        if (strategy != null)
        {
            var result = strategy is IExpressionIrStrategy ir
                ? StampIr(expression, ir.ConvertIr(expression, _context))
                : JsExpr.Opaque(Stamp(expression, strategy.Convert(expression, _context)));
            // The bound tree has the last word: the implicit conversion C# applied around this
            // expression, the string it flows into — settled once here, for every site.
            result = ValueFlow.Settle(expression, result, _context);
            _context.SetCached(expression, result);
            return result;
        }

        _context.Report(expression, ConversionSeverity.Error, "EQ1001",
            $"C# expression '{expression.Kind()}' has no transpilation strategy — it cannot be emitted as JavaScript. " +
            "Rewrite it in a transpilable form, or add a conversion strategy for this construct.");
        return JsExpr.Opaque(expression.ToString());
    }

    /// <summary>
    /// In design mode, wraps a node CONSTRUCTION so the built node remembers the span that made it.
    /// Everything else passes through untouched.
    /// <para>
    /// Constructions only — <c>new Card(…)</c>, <c>Card(…)</c> — never a reference. Stamping
    /// <c>column</c> where it is merely mentioned would overwrite the origin the construction already
    /// set with the span of a use, and the editor would select the wrong line.
    /// </para>
    /// <para>
    /// It needs the semantic model to know a node from anything else; without one it stamps nothing,
    /// which costs identity and never produces a wrong answer.
    /// </para>
    /// </summary>
    /// <summary>The IR form of <see cref="Stamp"/>: a construction that design mode wraps comes
    /// back call-shaped (the origin wrapper IS a call); anything else is returned untouched.</summary>
    private JsExpr StampIr(ExpressionSyntax expression, JsExpr emitted)
    {
        if (!_context.DesignMode) return emitted;
        var text = JsExprWriter.Write(emitted);
        var stamped = Stamp(expression, text);
        return ReferenceEquals(stamped, text) ? emitted : JsExpr.Callish(stamped);
    }

    private string Stamp(ExpressionSyntax expression, string emitted)
    {
        if (!_context.DesignMode) return emitted;
        if (expression is not (ObjectCreationExpressionSyntax
            or ImplicitObjectCreationExpressionSyntax
            or InvocationExpressionSyntax)) return emitted;

        var type = _context.SemanticHelper.GetType(expression);
        if (!type.IsVisualNode()) return emitted;

        var span = expression.GetLocation().GetLineSpan();
        if (!span.IsValid) return emitted;

        // FULL path: the origin is opened by an editor, and a relative one resolves against whatever
        // that editor's working directory happens to be — which is not where the compiler was run.
        // A caller is free to hand us a relative path; the stamp must still name a file.
        var file = expression.SyntaxTree.FilePath;
        if (file.Length > 0)
        {
            try { file = Path.GetFullPath(file); }
            catch (Exception) { /* an unrooted or malformed path stays as it came */ }
        }

        var origin = string.Concat(
            file, "|",
            span.StartLinePosition.Line.ToString(), ":", span.StartLinePosition.Character.ToString(), "|",
            span.EndLinePosition.Line.ToString(), ":", span.EndLinePosition.Character.ToString());

        _context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.Origin}({emitted}, {JsString(origin)}, {JsString(LabelFor(expression, type!))})";
    }

    /// <summary>
    /// What a design tool should CALL this node: its component type, and the local it is assigned to
    /// when it has one — <c>Column · content</c>. A screen assembles half a dozen Columns, and the
    /// name the author gave one is the only thing that tells them apart on sight.
    /// </summary>
    private string LabelFor(ExpressionSyntax expression, ITypeSymbol type)
    {
        // A helper method's STATIC return type is the abstract node, so the type name would label
        // half a real screen "VisualNode" — true, and useless. What identifies it is the method that
        // built it, which is also where the author has to go to change it.
        var name = type.Name;
        if (name is "VisualNode" or "UiComponent"
            && expression is InvocationExpressionSyntax
            && _context.SemanticHelper.GetSymbol(expression) is IMethodSymbol method)
        {
            name = $"{method.Name}()";
        }

        return Named(expression, name);
    }

    private static string Named(ExpressionSyntax expression, string name)
    {
        // `var content = new Column(…)` — the declarator is the construction's own parent, so this
        // names only the node that IS the variable, never one merely passed to it.
        var declared = expression.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            ? declarator.Identifier.Text
            : expression.Parent is AssignmentExpressionSyntax { Left: IdentifierNameSyntax assigned }
                ? assigned.Identifier.Text
                : null;

        return declared is null ? name : $"{name} · {declared}";
    }

    /// <summary>A JavaScript string literal. Paths carry backslashes on Windows and may carry quotes
    /// anywhere, and either one unescaped ends the literal early — which breaks the whole module.</summary>
    private static string JsString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// <summary>The block as text, laid out at the current depth — what a strategy still
    /// producing text splices for a nested body.</summary>
    public string ConvertBlock(BlockSyntax block) =>
        JsStatementWriter.Write(ConvertBlockIr(block), _context.Layout, _context.Depth);

    public JsStatement ConvertBlockIr(BlockSyntax block)
    {
        // C# HOISTS a local function: a screen can write `Items.Select(Row)` and declare
        // `VisualNode Row(…)` under the return, and the CLR neither knows nor cares. A `const`
        // arrow is not hoisted, so emitted in place it sits in its temporal dead zone at the moment
        // the code above reads it — dead code, and a ReferenceError in the browser while the server
        // renders the same source perfectly.
        // So the declarations LEAD the block, in source order. Their bodies only dereference at CALL
        // time, which is why one that captures a local declared further down still reads it (and C#
        // already refuses a call before that local is assigned).
        var statements = new List<JsStatement>();
        _context.Depth++;
        try
        {
            foreach (var stmt in block.Statements.Where(statement => statement is LocalFunctionStatementSyntax))
                statements.Add(ConvertStatementIr(stmt));
            statements.AddRange(WithUsingDeclarations(
                block.Statements.Where(statement => statement is not LocalFunctionStatementSyntax).ToList(), 0));
        }
        finally
        {
            _context.Depth--;
        }
        return JsStatement.Block(statements);
    }

    /// <summary>
    /// A <c>using</c> DECLARATION owns the rest of its block: everything after it runs inside a
    /// try whose finally disposes the resource — also when the body throws or returns — and
    /// several in one block nest, each disposing in reverse order of declaration. Emitted as a
    /// bare const (which is what it was for a long time), the resource was simply never disposed.
    /// </summary>
    private List<JsStatement> WithUsingDeclarations(IReadOnlyList<StatementSyntax> statements, int from)
    {
        var result = new List<JsStatement>();
        for (var i = from; i < statements.Count; i++)
        {
            var stmt = statements[i];
            result.Add(ConvertStatementIr(stmt));
            if (stmt is not LocalDeclarationStatementSyntax usingDecl
                || !usingDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)) continue;

            List<JsStatement> rest;
            _context.Depth++;
            try { rest = WithUsingDeclarations(statements, i + 1); }
            finally { _context.Depth--; }

            var isAsync = usingDecl.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword);
            var disposes = usingDecl.Declaration.Variables.Reverse()
                .Select(variable => Strategies.Statements.UsingLowering.Dispose(variable.Identifier.Text.ToJsIdentifier(), isAsync))
                .ToList();
            result.Add(JsStatement.Try(JsStatement.Block(rest), Array.Empty<JsCatch>(), JsStatement.Block(disposes)));
            return result;
        }
        return result;
    }

    /// <summary>The statement as text, laid out at the current depth.</summary>
    public string ConvertStatement(StatementSyntax stmt) =>
        JsStatementWriter.Write(ConvertStatementIr(stmt), _context.Layout, _context.Depth);

    /// <summary>The statement as IR — every statement strategy builds one.</summary>
    public JsStatement ConvertStatementIr(StatementSyntax stmt)
    {
        var strategy = _statementRegistry.FindStrategy(stmt, _context);
        if (strategy != null)
        {
            return strategy.Convert(stmt, _context);
        }

        if (stmt is BlockSyntax block)
        {
            return ConvertBlockIr(block);
        }

        _context.Report(stmt, ConversionSeverity.Error, "EQ1002",
            $"C# statement '{stmt.Kind()}' has no transpilation strategy — it cannot be emitted as JavaScript. " +
            "Rewrite it in a transpilable form, or add a conversion strategy for this construct.");
        return JsStatement.Raw(stmt.ToString());
    }
}
