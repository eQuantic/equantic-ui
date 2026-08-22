using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// The dispose a <c>using</c> promises, in the one shape both forms share: the statement form
/// (<c>using (var r = …) { }</c>) and the declaration form (<c>using var r = …;</c>, which owns
/// the REST of its block). A resource that never became disposable — a null, a twin without the
/// method — is skipped rather than thrown at, the way the runtime has always treated it.
/// </summary>
public static class UsingLowering
{
    public static JsStatement Dispose(string resource, bool isAsync)
    {
        var sync = JsStatement.If(
            JsExpr.Opaque($"{resource} && typeof {resource}.dispose === 'function'"),
            JsStatement.Block(new[] { JsStatement.Expression(JsExpr.Call(JsExpr.Member(JsExpr.Identifier(resource), "dispose"))) }),
            null);
        if (!isAsync) return sync;
        return JsStatement.If(
            JsExpr.Opaque($"{resource} && typeof {resource}.disposeAsync === 'function'"),
            JsStatement.Block(new[] { JsStatement.Expression(JsExpr.Opaque($"await {resource}.disposeAsync()")) }),
            sync);
    }
}
