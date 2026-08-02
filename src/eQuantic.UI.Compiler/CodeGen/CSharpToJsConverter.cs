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

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Robust C# to JavaScript expression converter using Roslyn AST
/// </summary>
public class CSharpToJsConverter
{
    private readonly TypeMappingRegistry _registry;
    private readonly StrategyRegistry _strategyRegistry;
    private readonly StatementStrategyRegistry _statementRegistry;
    private readonly ConversionContext _context;
    private SemanticModel? _semanticModel;

    public CSharpToJsConverter()
    {
        _registry = new TypeMappingRegistry();
        _context = new ConversionContext 
        { 
            Converter = this,
            SemanticHelper = new SemanticHelper(null)
        };
        _strategyRegistry = new StrategyRegistry();
        _statementRegistry = new StatementStrategyRegistry();

        RegisterStrategies();
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

    /// <summary>See <see cref="ConversionContext.UsedAppTypes"/> — output-introduced app-type names.</summary>
    public HashSet<string> UsedAppTypes => _context.UsedAppTypes;

    /// <summary>Diagnostics raised during the most recent conversion(s); call <see cref="ClearDiagnostics"/> between components.</summary>
    public IReadOnlyList<ConversionDiagnostic> Diagnostics => _context.Diagnostics;

    public void ClearDiagnostics() => _context.ClearDiagnostics();

    private void RegisterStrategies()
    {
        // Compile-Time Evaluation Strategy (Highest Priority - 100)
        _strategyRegistry.Register<CompileTimeEvaluatedExpressionStrategy>();

        _strategyRegistry.Register<AnyStrategy>();
        _strategyRegistry.Register<EnumStrategy>();
        _strategyRegistry.Register<EnumMethodStrategy>();
        _strategyRegistry.Register<EnumHasFlagStrategy>();
        _strategyRegistry.Register<NullableStrategy>();
        _strategyRegistry.Register<StructuralEqualsStrategy>();
        _strategyRegistry.Register<TupleExpressionStrategy>();
        _strategyRegistry.Register<TupleMemberAccessStrategy>();
        _strategyRegistry.Register<CheckedExpressionStrategy>();
        _strategyRegistry.Register<GuidTypeStrategy>();
        _strategyRegistry.Register<NamespaceRemovalStrategy>();
        _strategyRegistry.Register<NameofStrategy>(); // Priority 15
        _strategyRegistry.Register<DefaultKeywordStrategy>(); // Priority 15
        
        // LINQ Strategies
        _strategyRegistry.Register<SelectStrategy>();
        _strategyRegistry.Register<WhereStrategy>();
        _strategyRegistry.Register<FirstStrategy>();
        _strategyRegistry.Register<LastStrategy>();
        _strategyRegistry.Register<SingleStrategy>();
        _strategyRegistry.Register<AllStrategy>();
        _strategyRegistry.Register<CountStrategy>();
        _strategyRegistry.Register<OrderByStrategy>();
        _strategyRegistry.Register<SkipStrategy>();
        _strategyRegistry.Register<TakeStrategy>();
        _strategyRegistry.Register<DistinctStrategy>();
        _strategyRegistry.Register<ContainsStrategy>();
        _strategyRegistry.Register<SelectManyStrategy>();
        _strategyRegistry.Register<SumStrategy>();
        _strategyRegistry.Register<AverageStrategy>();
        _strategyRegistry.Register<MinMaxStrategy>();
        _strategyRegistry.Register<ReverseStrategy>();
        _strategyRegistry.Register<GroupByStrategy>();
        _strategyRegistry.Register<AggregateStrategy>();
        _strategyRegistry.Register<ToDictionaryStrategy>();
        _strategyRegistry.Register<ToLookupStrategy>();
        _strategyRegistry.Register<LookupIndexerStrategy>();
        _strategyRegistry.Register<JoinStrategy>();
        _strategyRegistry.Register<GroupJoinStrategy>();
        _strategyRegistry.Register<ZipStrategy>();
        _strategyRegistry.Register<ConcatStrategy>();
        _strategyRegistry.Register<UnionStrategy>();
        _strategyRegistry.Register<IntersectStrategy>();
        _strategyRegistry.Register<ExceptStrategy>();
        _strategyRegistry.Register<CastStrategy>();
        _strategyRegistry.Register<OfTypeStrategy>();
        _strategyRegistry.Register<TakeWhileStrategy>();
        _strategyRegistry.Register<SkipWhileStrategy>();
        _strategyRegistry.Register<DistinctByStrategy>();
        _strategyRegistry.Register<MinByMaxByStrategy>();
        _strategyRegistry.Register<ChunkStrategy>();
        _strategyRegistry.Register<ElementAtStrategy>();
        _strategyRegistry.Register<SequenceEqualStrategy>();
        _strategyRegistry.Register<DefaultIfEmptyStrategy>();

        // Primitive Type Strategies (Low Priority than new Invocation Strategies but higher than fallback)
        _strategyRegistry.Register<StringMethodStrategy>();
        _strategyRegistry.Register<StringStaticStrategy>(); // New Phase 7
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
        _strategyRegistry.Register<ThrowExpressionStrategy>();
        _strategyRegistry.Register<StackAllocArrayCreationStrategy>();
        _strategyRegistry.Register<WithExpressionStrategy>();
        _strategyRegistry.Register<TypeofExpressionStrategy>();
        _strategyRegistry.Register<BaseExpressionStrategy>();
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
        _strategyRegistry.Register<BooleanMethodStrategy>();
        _strategyRegistry.Register<CharMethodStrategy>();
        _strategyRegistry.Register<ConvertStrategy>();
        // StringStaticStrategy is registered in primitives block? Checking order logic.
        // It's a Primitive strategy, so lets check where StringMethodStrategy is.

        // Legacy Expression Strategies
        // Inlines external IconGlyph constants (pack glyphs) at the use site — must beat the fallback
        // member access, which would emit a broken `LucideIcons.camera`.
        _strategyRegistry.Register<InlinedConstantStrategy>();
        // Sequences that come from nothing, and .NET names for what the browser already has.
        _strategyRegistry.Register<Strategies.Linq.EnumerableFactoryStrategy>();
        _strategyRegistry.Register<Strategies.Types.WebPrimitiveStrategy>();
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
    }

    /// <summary>
    /// Convert a C# expression string to JavaScript
    /// </summary>
    public string Convert(string code)
    {
        // Legacy/Fallback string conversion (compiles new tree)
        var parsed = CSharpSyntaxTree.ParseText(code).GetRoot();
        
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
    public string ConvertExpression(ExpressionSyntax expression, string? expectedType = null)
    {
        _context.ExpectedType = expectedType;
        // Check cache
        var cached = _context.GetCached(expression);
        if (cached != null) return cached;

        // Try strategies
        var strategy = _strategyRegistry.FindStrategy(expression, _context);
        if (strategy != null)
        {
            var result = strategy.Convert(expression, _context);
            _context.SetCached(expression, result);
            return result;
        }

        _context.Report(expression, ConversionSeverity.Error, "EQ1001",
            $"C# expression '{expression.Kind()}' has no transpilation strategy — it cannot be emitted as JavaScript. " +
            "Rewrite it in a transpilable form, or add a conversion strategy for this construct.");
        return expression.ToString();
    }

    public string ConvertBlock(BlockSyntax block)
    {
        var sb = new StringBuilder();
        sb.Append("{"); // Use standard formatting
        foreach (var stmt in block.Statements)
        {
             sb.Append(ConvertStatement(stmt));
        }
        sb.Append("}");
        return sb.ToString();
    }
    
    public string ConvertStatement(StatementSyntax stmt)
    {
        var strategy = _statementRegistry.FindStrategy(stmt, _context);
        if (strategy != null)
        {
            return strategy.Convert(stmt, _context);
        }
        
        if (stmt is BlockSyntax block)
        {
            return ConvertBlock(block);
        }

        _context.Report(stmt, ConversionSeverity.Error, "EQ1002",
            $"C# statement '{stmt.Kind()}' has no transpilation strategy — it cannot be emitted as JavaScript. " +
            "Rewrite it in a transpilable form, or add a conversion strategy for this construct.");
        return stmt.ToString();
    }
}
