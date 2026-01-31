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

    public HashSet<string> UsedHelpers => _context.UsedHelpers;

    private void RegisterStrategies()
    {
        _strategyRegistry.Register<AnyStrategy>();
        _strategyRegistry.Register<EnumStrategy>();
        _strategyRegistry.Register<EnumMethodStrategy>();
        _strategyRegistry.Register<NullableStrategy>();
        _strategyRegistry.Register<TupleExpressionStrategy>();
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
        _strategyRegistry.Register<JoinStrategy>();
        _strategyRegistry.Register<ZipStrategy>();
        _strategyRegistry.Register<ConcatStrategy>();
        _strategyRegistry.Register<UnionStrategy>();
        _strategyRegistry.Register<IntersectStrategy>();
        _strategyRegistry.Register<ExceptStrategy>();
        _strategyRegistry.Register<CastStrategy>();
        _strategyRegistry.Register<OfTypeStrategy>();

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

        // === UI Strategies (Priority 10) ===
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
        _strategyRegistry.Register<IsPatternStrategy>();
        _strategyRegistry.Register<DeclarationExpressionStrategy>();
        _strategyRegistry.Register<CollectionExpressionStrategy>();
        _strategyRegistry.Register<NullCoalescingAssignmentStrategy>(); // Priority 15 - before AssignmentExpressionStrategy
        _strategyRegistry.Register<AssignmentExpressionStrategy>();
        _strategyRegistry.Register<IdentifierStrategy>();
        _strategyRegistry.Register<RangeExpressionStrategy>();
        _strategyRegistry.Register<ThrowExpressionStrategy>();
        _strategyRegistry.Register<StackAllocArrayCreationStrategy>();
        
        // Additional Types
        _strategyRegistry.Register<DateTimeStrategy>();
        _strategyRegistry.Register<TimeSpanStrategy>();
        _strategyRegistry.Register<RegexStrategy>();
        _strategyRegistry.Register<HashSetStrategy>();
        
        // Phase 7: Static Helpers & Async
        _strategyRegistry.Register<TaskMethodStrategy>();
        _strategyRegistry.Register<NumberMethodStrategy>();
        // StringStaticStrategy is registered in primitives block? Checking order logic.
        // It's a Primitive strategy, so lets check where StringMethodStrategy is.

        // Legacy Expression Strategies
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

        // Statement Strategies
        _statementRegistry.Register<IfStatementStrategy>();
        _statementRegistry.Register<ForStatementStrategy>();
        _statementRegistry.Register<ForEachStatementStrategy>();
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
    }

    /// <summary>
    /// Convert a C# expression string to JavaScript
    /// </summary>
    public string Convert(string code)
    {
        // Legacy/Fallback string conversion (compiles new tree)
        var parsed = CSharpSyntaxTree.ParseText(code).GetRoot();
        
        // Try to find the best node to convert
        var expr = parsed.DescendantNodes().OfType<ExpressionSyntax>().FirstOrDefault();
        if (expr != null) return ConvertExpression(expr);
        
        var block = parsed.DescendantNodes().OfType<BlockSyntax>().FirstOrDefault();
        if (block != null) return ConvertBlock(block);
        
        var stmt = parsed.DescendantNodes().OfType<StatementSyntax>().FirstOrDefault();
        if (stmt != null) return ConvertStatement(stmt);
        
        return code;
    }

    public string Convert(SyntaxNode node)
    {
        if (node is ExpressionSyntax expr) return ConvertExpression(expr);
        if (node is BlockSyntax block) return ConvertBlock(block);
        if (node is StatementSyntax stmt) return ConvertStatement(stmt);
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

        return expression switch
        {
            ParenthesizedExpressionSyntax parens => $"({ConvertExpression(parens.Expression)})",
            _ => expression.ToString()
        };
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
        
        return stmt.ToString(); 
    }
}
