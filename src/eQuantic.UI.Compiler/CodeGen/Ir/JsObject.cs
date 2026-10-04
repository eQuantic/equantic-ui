namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// An object literal, <c>{ key: value, … }</c>: what an object initializer and an anonymous object
/// build, so the lambdas among their values reach the writer as arrows whose blocks map line by line
/// (#492). Spliced as text, an initializer took every line of such a lambda's block with it: a
/// handler a node is configured with is the shape most screens are written in.
/// </summary>
public sealed record JsObject(IReadOnlyList<JsProperty> Properties) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Primary;
}
