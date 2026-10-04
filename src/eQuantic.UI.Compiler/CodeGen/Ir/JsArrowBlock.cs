namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// An arrow function with a block body: <c>(parameters) => { … }</c>. The block is the statement
/// IR, so each statement keeps the C# it came from and maps to its own line wherever a writer places
/// the arrow (#384). It is laid out in <see cref="Layout"/> at <see cref="Depth"/>, where the lambda
/// stands in the C#: a string seam writes the arrow long before the statement that holds it is
/// placed, and knows nothing of that depth. It binds at assignment level, as <see cref="JsArrow"/>
/// does.
/// </summary>
public sealed record JsArrowBlock(string Parameters, JsStatement Block, bool IsAsync, JsLayout Layout, int Depth) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Assignment;
}
