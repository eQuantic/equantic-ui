using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// A SUBSTATEMENT that became several statements — a pattern variable hoists a declaration in
/// front of the statement it belongs to — must still belong to its construct. C# lets a loop body
/// or an <c>if</c> branch go without braces; the writer adds them exactly when the body is a
/// sequence of two or more, and touches nothing else, so a single statement and a block read as
/// they always did. The hole was the same at every substatement position, so the rule lives in
/// the writer, once, rather than in each strategy.
/// </summary>
public class SubstatementBracingTests
{
    private static JsStatement Push(string name) =>
        JsStatement.Expression(JsExpr.Call(JsExpr.Identifier("items.push"), new JsExpr[] { JsExpr.Identifier(name) }));

    private static JsStatement HoistedBody() => JsStatement.Hoisted("let v;", Push("v"));

    [Fact]
    public void ALoopBodyThatHoistsADeclarationIsBraced_InBothLayouts()
    {
        var loop = JsStatement.Headed("for (const x of xs)", HoistedBody());

        JsStatementWriter.Write(loop, JsLayout.Compact, 0)
            .Should().Be("for (const x of xs) {let v;items.push(v);}");

        JsStatementWriter.Write(loop, JsLayout.Pretty, 0).Should().Be(
            "for (const x of xs) {\n" +
            "    let v;\n" +
            "    items.push(v);\n" +
            "}");
    }

    [Fact]
    public void ASingleStatementBodyStaysBraceless()
    {
        var loop = JsStatement.Headed("for (const x of xs)", Push("x"));

        JsStatementWriter.Write(loop, JsLayout.Compact, 0).Should().Be("for (const x of xs) items.push(x);");
        JsStatementWriter.Write(loop, JsLayout.Pretty, 0).Should().Be("for (const x of xs) items.push(x);");
    }

    [Fact]
    public void AnExplicitBlockIsNotBracedTwice()
    {
        var loop = JsStatement.Headed("for (const x of xs)", JsStatement.Block(new[] { JsStatement.Raw("let v;"), Push("v") }));

        JsStatementWriter.Write(loop, JsLayout.Compact, 0).Should().Be("for (const x of xs) {let v;items.push(v);}");
    }

    [Fact]
    public void EveryOtherSubstatementPositionIsBracedTheSameWay()
    {
        var condition = JsExpr.Identifier("go");

        JsStatementWriter.Write(JsStatement.If(condition, HoistedBody(), HoistedBody()), JsLayout.Compact, 0)
            .Should().Be("if (go) {let v;items.push(v);} else {let v;items.push(v);}");

        JsStatementWriter.Write(JsStatement.While(condition, HoistedBody()), JsLayout.Compact, 0)
            .Should().Be("while (go) {let v;items.push(v);}");

        JsStatementWriter.Write(JsStatement.DoWhile(HoistedBody(), condition), JsLayout.Compact, 0)
            .Should().Be("do {let v;items.push(v);} while (go);");
    }
}
