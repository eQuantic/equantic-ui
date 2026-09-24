namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary><c>const name = (parameters) => { … };</c> — an arrow with a block body bound to a name,
/// written as a statement (a C# local function), so its body is laid out and marked by the
/// statement writer like any other block (#293).</summary>
public sealed record JsConstArrow(string Name, string Parameters, bool IsAsync, JsStatement Body) : JsStatement;
