namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// One <c>const name = value;</c> a module declares after its imports: what its code reads at every
/// call and builds once, an enum's table (#547). The value is the JavaScript that builds it, on one
/// line, and names nothing the module imports, so it can stand where a module evaluates first.
/// </summary>
public sealed record JsConstant(string Name, string Value);
