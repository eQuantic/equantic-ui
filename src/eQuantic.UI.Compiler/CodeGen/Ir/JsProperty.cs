namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>One member of a <see cref="JsObject"/>: its key in its final spelling (a name, a quoted
/// string, a computed <c>[key]</c>) and its value, fenced by the commas around it.</summary>
public sealed record JsProperty(string Key, JsExpr Value);
