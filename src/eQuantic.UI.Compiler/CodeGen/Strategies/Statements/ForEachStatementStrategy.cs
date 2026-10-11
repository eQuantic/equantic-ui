using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>foreach (var x in xs)</c> → <c>for (const x of xs)</c>; <c>await foreach</c> →
/// <c>for await</c>. A dictionary is iterable as it is, its runtime class enumerating its pairs.</summary>
public class ForEachStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForEachStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var foreachStmt = (ForEachStatementSyntax)node;
        var item = foreachStmt.Identifier.Text.ToJsIdentifier();
        var declared = ExpressionVariableScanner.Declarations(foreachStmt.Expression, context.TypeAnnotations);
        // A string enumerates its chars, the UTF-16 code units, where JavaScript's iterator gives code
        // points: `foreach (var c in "a😀b")` counted three where .NET counts four (#524). A type that
        // may hold one (`object`, `IEnumerable`, `IEnumerable<char>`) asks the value what it is.
        var source = context.Converter.ConvertIr(foreachStmt.Expression);
        var sourceType = context.SemanticHelper.GetType(foreachStmt.Expression);
        // A class's public GetEnumerator() beside the interface's, which C#'s foreach binds and its
        // iteration does not, is called as the loop binds it (IterableTwin.ForEachSource).
        var bound = foreachStmt.AwaitKeyword.Value == null && context.SemanticHelper.ForEachInfo(foreachStmt) is { } info
            ? IterableTwin.ForEachSource(info, sourceType, source, context.UsedHelpers)
            : null;
        var collection = JsExprWriter.Write(sourceType switch
        {
            _ when bound is not null => bound,
            { SpecialType: SpecialType.System_String } => JsExpr.Call(JsExpr.Member(source, "split"), JsExpr.Literal("''")),
            _ when MayHoldAString(sourceType) => Enumerable(source, context),
            _ => source,
        });

        var body = context.Converter.ConvertStatementIr(foreachStmt.Statement);
        var loopType = foreachStmt.AwaitKeyword.Value != null ? "for await" : "for";

        // The ELEMENT converts to the loop variable's type, one item at a time — `foreach (long l
        // in ints)` makes each int a BigInt, `foreach (int code in chars)` each char its code unit,
        // `foreach (Money m in ints)` calls the type's conversion. The syntax shows none of it; the
        // bound tree reports the conversion (ForEachStatementInfo), and ValueFlow's table applies
        // it. A conversion that changes nothing on this side keeps the plain loop.
        JsStatement loop;
        if (ElementConversion(foreachStmt, item, context) is { } converted)
        {
            var statements = new List<JsStatement> { JsStatement.Const(item, converted) };
            statements.AddRange(body is JsBlock block ? block.Statements : new[] { body });
            loop = JsStatement.Headed($"{loopType} (const ${item} of {collection})", JsStatement.Block(statements));
        }
        else
        {
            loop = JsStatement.Headed($"{loopType} (const {item} of {collection})", body);
        }

        // What the collection expression declares (`foreach (var c in Parse(s, out var n) …)`) is
        // the loop's own: Roslyn scopes it to the statement, so two sibling loops may repeat a name,
        // and a block around the loop is where it is declared. The collection is read once, so one
        // variable per loop is .NET's answer too.
        return declared.Length == 0 ? loop : JsStatement.Block([JsStatement.Raw(declared.TrimEnd()), loop]);
    }

    private static JsExpr? ElementConversion(ForEachStatementSyntax foreachStmt, string item, ConversionContext context)
    {
        if (context.SemanticHelper.ForEachInfo(foreachStmt) is not { } info) return null;
        var conversion = info.ElementConversion;
        if (!conversion.Exists || conversion.IsIdentity) return null;
        if (context.SemanticHelper.GetDeclaredSymbol(foreachStmt) is not ILocalSymbol variable) return null;

        var element = JsExpr.Identifier("$" + item);
        var applied = ValueFlow.Apply(conversion, info.ElementType, variable.Type, null, null, element, context);
        return JsExprWriter.Write(applied) == "$" + item ? null : applied;
    }

    /// <summary>Whether a value of this static type may be a string: <c>object</c>, <c>dynamic</c>,
    /// the non-generic <c>IEnumerable</c>, and <c>IEnumerable&lt;char&gt;</c>.</summary>
    private static bool MayHoldAString(ITypeSymbol? type) => type switch
    {
        { SpecialType: SpecialType.System_Object or SpecialType.System_Collections_IEnumerable } => true,
        { TypeKind: TypeKind.Dynamic } => true,
        INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } sequence =>
            sequence.TypeArguments[0].SpecialType == SpecialType.System_Char,
        _ => false,
    };

    private static JsExpr Enumerable(JsExpr source, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.LinqEnumerable), source);
    }

    public int Priority => 0;
}
