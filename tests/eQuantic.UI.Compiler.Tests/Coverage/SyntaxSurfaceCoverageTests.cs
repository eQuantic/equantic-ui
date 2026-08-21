using System.Reflection;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Strategies;
using eQuantic.UI.Compiler.CodeGen.Strategies.Async;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;
using eQuantic.UI.Compiler.CodeGen.Strategies.Special;
using eQuantic.UI.Compiler.CodeGen.Strategies.Statements;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// The DENOMINATOR of language coverage: every concrete expression and statement node type the
/// embedded Roslyn can produce, each with an explicit answer to "what does eqc do with this?".
/// Coverage used to be measured by corpus — by whatever snippets the tests happened to contain —
/// which can only confirm what someone already thought of. This enumerates what the LANGUAGE
/// allows: a Roslyn bump that introduces a node type lands here as "unclassified" and fails the
/// build, so the answer is decided consciously instead of discovered by a user's miscompile.
/// </summary>
public class SyntaxSurfaceCoverageTests
{
    private abstract record Coverage;

    /// <summary>A registered conversion strategy claims the node.</summary>
    private sealed record ByStrategy(Type Strategy) : Coverage;

    /// <summary>Consumed INSIDE another construct's conversion — never dispatched on its own.</summary>
    private sealed record LoweredBy(Type Owner) : Coverage;

    /// <summary>Deliberately unsupported, or known-uncovered: reaching it raises this EQ code.</summary>
    private sealed record FailsWith(string Code) : Coverage;

    /// <summary>Consumed as a TYPE position (annotations, casts) — never converted as a value.</summary>
    private sealed record TypePosition : Coverage;

    private static readonly Dictionary<Type, Coverage> Classification = new()
    {
        // ---- Expressions: claimed by a registered strategy --------------------------------------
        [typeof(AnonymousMethodExpressionSyntax)] = new ByStrategy(typeof(AnonymousMethodExpressionStrategy)),
        [typeof(AnonymousObjectCreationExpressionSyntax)] = new ByStrategy(typeof(AnonymousObjectCreationStrategy)),
        [typeof(ArrayCreationExpressionSyntax)] = new ByStrategy(typeof(ArrayCreationStrategy)),
        [typeof(ImplicitArrayCreationExpressionSyntax)] = new ByStrategy(typeof(ArrayCreationStrategy)),
        [typeof(AssignmentExpressionSyntax)] = new ByStrategy(typeof(AssignmentExpressionStrategy)),
        [typeof(AwaitExpressionSyntax)] = new ByStrategy(typeof(AwaitExpressionStrategy)),
        [typeof(BaseExpressionSyntax)] = new ByStrategy(typeof(BaseExpressionStrategy)),
        [typeof(BinaryExpressionSyntax)] = new ByStrategy(typeof(BinaryExpressionStrategy)),
        [typeof(CastExpressionSyntax)] = new ByStrategy(typeof(CastExpressionStrategy)),
        [typeof(CheckedExpressionSyntax)] = new ByStrategy(typeof(CheckedExpressionStrategy)),
        [typeof(CollectionExpressionSyntax)] = new ByStrategy(typeof(CollectionExpressionStrategy)),
        [typeof(ConditionalAccessExpressionSyntax)] = new ByStrategy(typeof(ConditionalAccessStrategy)),
        [typeof(ConditionalExpressionSyntax)] = new ByStrategy(typeof(ConditionalExpressionStrategy)),
        [typeof(DeclarationExpressionSyntax)] = new ByStrategy(typeof(DeclarationExpressionStrategy)),
        [typeof(DefaultExpressionSyntax)] = new ByStrategy(typeof(DefaultKeywordStrategy)),
        [typeof(ElementAccessExpressionSyntax)] = new ByStrategy(typeof(ElementAccessStrategy)),
        [typeof(FieldExpressionSyntax)] = new ByStrategy(typeof(FieldExpressionStrategy)),
        [typeof(GenericNameSyntax)] = new ByStrategy(typeof(GenericNameStrategy)),
        [typeof(IdentifierNameSyntax)] = new ByStrategy(typeof(IdentifierStrategy)),
        [typeof(ImplicitObjectCreationExpressionSyntax)] = new ByStrategy(typeof(ObjectCreationStrategy)),
        [typeof(InitializerExpressionSyntax)] = new ByStrategy(typeof(InitializerExpressionStrategy)),
        [typeof(InterpolatedStringExpressionSyntax)] = new ByStrategy(typeof(InterpolatedStringStrategy)),
        [typeof(InvocationExpressionSyntax)] = new ByStrategy(typeof(InvocationStrategy)),
        [typeof(IsPatternExpressionSyntax)] = new ByStrategy(typeof(IsPatternStrategy)),
        [typeof(LiteralExpressionSyntax)] = new ByStrategy(typeof(LiteralExpressionStrategy)),
        [typeof(MemberAccessExpressionSyntax)] = new ByStrategy(typeof(MemberAccessStrategy)),
        [typeof(ObjectCreationExpressionSyntax)] = new ByStrategy(typeof(ObjectCreationStrategy)),
        [typeof(ParenthesizedExpressionSyntax)] = new ByStrategy(typeof(ParenthesizedExpressionStrategy)),
        [typeof(ParenthesizedLambdaExpressionSyntax)] = new ByStrategy(typeof(LambdaExpressionStrategy)),
        [typeof(PostfixUnaryExpressionSyntax)] = new ByStrategy(typeof(UnaryExpressionStrategy)),
        [typeof(PrefixUnaryExpressionSyntax)] = new ByStrategy(typeof(UnaryExpressionStrategy)),
        [typeof(RangeExpressionSyntax)] = new ByStrategy(typeof(RangeExpressionStrategy)),
        [typeof(SimpleLambdaExpressionSyntax)] = new ByStrategy(typeof(LambdaExpressionStrategy)),
        [typeof(SizeOfExpressionSyntax)] = new ByStrategy(typeof(SizeOfExpressionStrategy)),
        [typeof(StackAllocArrayCreationExpressionSyntax)] = new ByStrategy(typeof(StackAllocArrayCreationStrategy)),
        [typeof(SwitchExpressionSyntax)] = new ByStrategy(typeof(SwitchExpressionStrategy)),
        [typeof(ThisExpressionSyntax)] = new ByStrategy(typeof(ThisExpressionStrategy)),
        [typeof(ThrowExpressionSyntax)] = new ByStrategy(typeof(ThrowExpressionStrategy)),
        [typeof(TupleExpressionSyntax)] = new ByStrategy(typeof(TupleExpressionStrategy)),
        [typeof(TypeOfExpressionSyntax)] = new ByStrategy(typeof(TypeofExpressionStrategy)),
        [typeof(WithExpressionSyntax)] = new ByStrategy(typeof(WithExpressionStrategy)),

        // ---- Expressions: consumed inside another construct's conversion ------------------------
        [typeof(ElementBindingExpressionSyntax)] = new LoweredBy(typeof(ConditionalAccessStrategy)),
        [typeof(MemberBindingExpressionSyntax)] = new LoweredBy(typeof(ConditionalAccessStrategy)),
        [typeof(ImplicitElementAccessSyntax)] = new LoweredBy(typeof(InitializerExpressionStrategy)),

        // ---- Expressions: deliberately fenced (no browser equivalent) ---------------------------
        [typeof(MakeRefExpressionSyntax)] = new FailsWith("EQ2001"),
        [typeof(RefTypeExpressionSyntax)] = new FailsWith("EQ2001"),
        [typeof(RefValueExpressionSyntax)] = new FailsWith("EQ2001"),
        [typeof(PointerTypeSyntax)] = new FailsWith("EQ2001"),
        [typeof(FunctionPointerTypeSyntax)] = new FailsWith("EQ2001"),

        // ---- Expressions: KNOWN-uncovered — dispatching one raises the generic error. Every entry
        // here is a visible candidate, not a shrug: promote it to a strategy or an EQ2xxx fence.
        [typeof(QueryExpressionSyntax)] = new ByStrategy(typeof(QueryExpressionStrategy)), // from/where/orderby/select/group lower onto the method-syntax strategies; join/let/into/second-from are EQ2008
        [typeof(RefExpressionSyntax)] = new FailsWith("EQ1001"),            // `ref x` as a value
        [typeof(UnsafeExpressionSyntax)] = new FailsWith("EQ1001"),         // C# 15 unsafe(expr) — pointer territory
        [typeof(ImplicitStackAllocArrayCreationExpressionSyntax)] = new FailsWith("EQ1001"),
        [typeof(AliasQualifiedNameSyntax)] = new FailsWith("EQ1001"),       // global::A.B as a receiver
        [typeof(PredefinedTypeSyntax)] = new FailsWith("EQ1001"),           // a bare `char`/`int` receiver a gate declined

        // ---- Name/type nodes only ever consumed in TYPE positions -------------------------------
        [typeof(ArrayTypeSyntax)] = new TypePosition(),
        [typeof(NullableTypeSyntax)] = new TypePosition(),
        [typeof(OmittedArraySizeExpressionSyntax)] = new TypePosition(),
        [typeof(OmittedTypeArgumentSyntax)] = new TypePosition(),
        [typeof(QualifiedNameSyntax)] = new TypePosition(),                 // expression positions parse as MemberAccess
        [typeof(RefTypeSyntax)] = new TypePosition(),
        [typeof(ScopedTypeSyntax)] = new TypePosition(),
        [typeof(TupleTypeSyntax)] = new TypePosition(),

        // ---- Statements -------------------------------------------------------------------------
        [typeof(BlockSyntax)] = new LoweredBy(typeof(CSharpToJsConverter)),
        [typeof(BreakStatementSyntax)] = new ByStrategy(typeof(BreakStatementStrategy)),
        [typeof(CheckedStatementSyntax)] = new ByStrategy(typeof(CheckedStatementStrategy)),
        [typeof(ContinueStatementSyntax)] = new ByStrategy(typeof(ContinueStatementStrategy)),
        [typeof(DoStatementSyntax)] = new ByStrategy(typeof(DoWhileStatementStrategy)),
        [typeof(EmptyStatementSyntax)] = new ByStrategy(typeof(EmptyStatementStrategy)),
        [typeof(ExpressionStatementSyntax)] = new ByStrategy(typeof(ExpressionStatementStrategy)),
        [typeof(FixedStatementSyntax)] = new ByStrategy(typeof(FixedStatementStrategy)),
        [typeof(ForEachStatementSyntax)] = new ByStrategy(typeof(ForEachStatementStrategy)),
        [typeof(ForEachVariableStatementSyntax)] = new ByStrategy(typeof(ForEachVariableStatementStrategy)),
        [typeof(ForStatementSyntax)] = new ByStrategy(typeof(ForStatementStrategy)),
        [typeof(GotoStatementSyntax)] = new FailsWith("EQ2002"),
        [typeof(IfStatementSyntax)] = new ByStrategy(typeof(IfStatementStrategy)),
        [typeof(LabeledStatementSyntax)] = new ByStrategy(typeof(LabeledStatementStrategy)),
        [typeof(LocalDeclarationStatementSyntax)] = new ByStrategy(typeof(LocalDeclarationStrategy)),
        [typeof(LocalFunctionStatementSyntax)] = new ByStrategy(typeof(LocalFunctionStatementStrategy)),
        [typeof(LockStatementSyntax)] = new ByStrategy(typeof(LockStatementStrategy)),
        [typeof(ReturnStatementSyntax)] = new ByStrategy(typeof(ReturnStatementStrategy)),
        [typeof(SwitchStatementSyntax)] = new ByStrategy(typeof(SwitchStatementStrategy)),
        [typeof(ThrowStatementSyntax)] = new ByStrategy(typeof(ThrowStatementStrategy)),
        [typeof(TryStatementSyntax)] = new ByStrategy(typeof(TryStatementStrategy)),
        [typeof(UnsafeStatementSyntax)] = new ByStrategy(typeof(UnsafeStatementStrategy)),
        [typeof(UsingStatementSyntax)] = new ByStrategy(typeof(UsingStatementStrategy)),
        [typeof(WhileStatementSyntax)] = new ByStrategy(typeof(WhileStatementStrategy)),
        [typeof(YieldStatementSyntax)] = new ByStrategy(typeof(YieldStatementStrategy)),
    };

    private static IReadOnlyList<Type> ConvertibleNodeTypes() =>
        typeof(ExpressionSyntax).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && (typeof(ExpressionSyntax).IsAssignableFrom(type)
                    || typeof(StatementSyntax).IsAssignableFrom(type)))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void EveryConvertibleNodeType_HasAnExplicitClassification()
    {
        var unclassified = ConvertibleNodeTypes()
            .Where(type => !Classification.ContainsKey(type))
            .Select(type => type.Name)
            .ToArray();

        Assert.True(unclassified.Length == 0,
            "Unclassified syntax node types (decide what eqc does with each):\n  "
            + string.Join("\n  ", unclassified));
    }

    [Fact]
    public void NoClassificationIsStale()
    {
        var enumerated = ConvertibleNodeTypes().ToHashSet();
        var stale = Classification.Keys
            .Where(type => !enumerated.Contains(type))
            .Select(type => type.Name)
            .ToArray();

        Assert.True(stale.Length == 0,
            "Classified types that are no longer convertible nodes:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>
    /// A ByStrategy claim must point at a strategy the converter actually REGISTERS — the map keys
    /// are compile-checked (typeof survives renames), and this closes the other half: an entry
    /// whose strategy was unregistered would otherwise go on promising coverage that no longer
    /// exists. Reflection into the private registries, because that is where the truth lives.
    /// </summary>
    [Fact]
    public void EveryClaimedStrategy_IsActuallyRegistered()
    {
        var converter = new CSharpToJsConverter();
        var registered = new HashSet<Type>();
        foreach (var registryField in new[] { "_strategyRegistry", "_statementRegistry" })
        {
            var registry = typeof(CSharpToJsConverter)
                .GetField(registryField, BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(converter)!;
            var strategies = (System.Collections.IEnumerable)registry.GetType()
                .GetField("_strategies", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(registry)!;
            foreach (var strategy in strategies) registered.Add(strategy.GetType());
        }

        var unregistered = Classification.Values.OfType<ByStrategy>()
            .Select(claim => claim.Strategy)
            .Distinct()
            .Where(strategy => !registered.Contains(strategy))
            .Select(strategy => strategy.Name)
            .ToArray();

        Assert.True(unregistered.Length == 0,
            "Classifications point at strategies the converter never registers:\n  "
            + string.Join("\n  ", unregistered));
    }
}
