using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>try</c>/<c>catch</c>/<c>finally</c>. JavaScript takes one <c>catch</c> and no exception type, so
/// the C# clauses become ONE catch that tries them in order, as C# does: each by its type, the test a
/// type pattern writes (<see cref="PatternConverter.TypeCheck"/>), and by its filter, and the first
/// that takes the exception runs; one that none takes is thrown on.
/// <code>
/// } catch ($e) {
///     let e = $e;
///     if ($eq.exceptions.is($e, 'System.InvalidOperationException')) { … }
///     else if ($eq.exceptions.is($e, 'System.ArgumentException') &amp;&amp; $eq.exceptions.filter(() => e.message === 'b')) { … }
///     else { throw $e; }
/// }
/// </code>
/// <para>
/// Each clause was its own JavaScript catch: two of them were a SyntaxError that cost the whole
/// module, and one took every exception, its type never tested and its filter dropped (#474).
/// </para>
/// <para>
/// A clause that takes everything (no type, or <c>System.Exception</c>) with no filter, alone, is the
/// catch it always was, <c>catch (e)</c> or <c>catch</c> with no binding, unless it rethrows: a
/// <c>throw;</c> rethrows the exception CAUGHT, which a variable the block assigns no longer holds, so
/// a catch whose clause rethrows binds it as <see cref="Caught"/>.
/// </para>
/// <para>
/// One difference stays, the platform's: .NET runs a filter BEFORE the <c>finally</c> blocks between
/// the throw and the clause (a first pass that only looks for a handler), and JavaScript has unwound
/// them by the time its catch runs.
/// </para>
/// </summary>
public class TryStatementStrategy : IStatementStrategy
{
    /// <summary>The name the exception is caught under, which no C# identifier can take: every clause's
    /// variable is bound to it, and a <c>throw;</c> in a clause rethrows it.</summary>
    internal const string Caught = "$e";

    public int Priority => 0;

    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is TryStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var tryStmt = (TryStatementSyntax)node;
        var converter = context.Converter;

        var body = converter.ConvertBlockIr(tryStmt.Block);
        return JsStatement.Try(
            body,
            tryStmt.Catches.Count == 0 ? null : Catch(tryStmt.Catches, context),
            tryStmt.Finally is null ? null : converter.ConvertBlockIr(tryStmt.Finally.Block));
    }

    private static JsCatch Catch(SyntaxList<CatchClauseSyntax> clauses, ConversionContext context)
    {
        if (clauses is [var only] && only.Filter is null && TakesEverything(only, context) && !Rethrows(only))
            return new JsCatch(Binding(VariableName(only), context), context.Converter.ConvertBlockIr(only.Block));

        var statements = new List<JsStatement>();
        // Every clause's variable is the exception caught, bound before any clause is tried, since a
        // filter reads it; a name two clauses share is bound once.
        var variables = clauses.Select(VariableName).OfType<string>().Distinct().ToList();
        statements.AddRange(variables.Select(name => JsStatement.Let(name, "", JsExpr.Identifier(Caught))));
        // What a filter declares (`when (int.TryParse(e.Message, out var n))`) lives in its clause; one
        // slot serves every clause that declares the name, since only one clause runs its block.
        var declared = ExpressionVariableScanner.Declarations(
            clauses.SelectMany(clause => ExpressionVariableScanner.Names(clause.Filter?.FilterExpression))
                .Where(name => !variables.Contains(name)),
            context.TypeAnnotations);
        if (declared.Length > 0) statements.Add(JsStatement.Raw(declared.TrimEnd()));

        var dispatch = Dispatch(clauses, 0, context);
        if (dispatch is JsBlock block) statements.AddRange(block.Statements);
        else statements.Add(dispatch);
        return new JsCatch(Binding(Caught, context), JsStatement.Block(statements));
    }

    /// <summary>
    /// The clauses from <paramref name="index"/> on, as C# tries them: the first whose type and filter
    /// take the exception runs its block, and an exception none takes is thrown on, the one caught.
    /// A clause that takes everything is the last one C# allows (CS0160, CS1017).
    /// </summary>
    private static JsStatement Dispatch(SyntaxList<CatchClauseSyntax> clauses, int index, ConversionContext context)
    {
        if (index == clauses.Count) return JsStatement.Block([JsStatement.Throw(JsExpr.Identifier(Caught))]);
        var clause = clauses[index];
        var block = context.Converter.ConvertBlockIr(clause.Block);
        return Test(clause, context) is { } test
            ? JsStatement.If(test, block, Dispatch(clauses, index + 1, context))
            : block;
    }

    /// <summary>
    /// Whether a clause takes the exception: its type, unless it takes every type, and its filter.
    /// The filter runs as .NET runs it, where one that throws has answered false
    /// (<see cref="Eq.ExceptionFilter"/>); it is the one function this lowering writes around C#, and
    /// C# allows no <c>await</c> in a filter (CS7094). Null for a clause that takes everything.
    /// </summary>
    private static JsExpr? Test(CatchClauseSyntax clause, ConversionContext context)
    {
        JsExpr? type = TakesEverything(clause, context)
            ? null
            : JsExpr.Opaque(PatternConverter.TypeCheck(clause.Declaration!.Type, Caught, context));
        if (clause.Filter is not { } filter) return type;

        context.UsedHelpers.Add(Eq.Import);
        var filtered = JsExpr.Call(JsExpr.Identifier(Eq.ExceptionFilter),
            JsExpr.Arrow("", context.Converter.ConvertIr(filter.FilterExpression)));
        return type is null ? filtered : JsExpr.Binary(type, "&&", filtered);
    }

    /// <summary>A clause with no type, or of <c>System.Exception</c>, by its symbol: every thrown value
    /// is one. The name decides only where the model cannot be asked.</summary>
    private static bool TakesEverything(CatchClauseSyntax clause, ConversionContext context)
    {
        if (clause.Declaration is not { } declaration) return true;
        if (context.SemanticHelper.GetSymbol(declaration.Type) is ITypeSymbol type) return ExceptionTypes.IsRoot(type);
        return context.CanGuess(declaration.Type) && declaration.Type.ToString() is "Exception" or "System.Exception";
    }

    /// <summary>Whether the clause's own block rethrows (<c>throw;</c>), a nested clause's aside.</summary>
    private static bool Rethrows(CatchClauseSyntax clause) =>
        clause.Block.DescendantNodes().OfType<ThrowStatementSyntax>()
            .Any(rethrow => rethrow.Expression is null && rethrow.FirstAncestorOrSelf<CatchClauseSyntax>() == clause);

    /// <summary>The name every reference reads (ToJsIdentifier, so <c>@class</c> is renamed), or null
    /// for a clause with no variable.</summary>
    private static string? VariableName(CatchClauseSyntax clause) =>
        clause.Declaration?.Identifier.Text is { Length: > 0 } caught ? caught.ToJsIdentifier() : null;

    /// <summary>
    /// The catch's binding, ANNOTATED <c>any</c> where the module is type-checked: a catch binding is
    /// <c>unknown</c> there, and C# hands a typed exception, so a body reading <c>error.Message</c> did
    /// not compile. With no name, the optional catch binding.
    /// </summary>
    private static string Binding(string? name, ConversionContext context) =>
        name is null ? "" : context.TypeAnnotations ? $"({name}: any)" : $"({name})";
}
