using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>The arrow node and the headed/try/switch/const statements, in both layouts.</summary>
public class JsStatementWriterStructureTests
{
    private static JsExpr Name(string n) => JsExpr.Identifier(n);
    private static JsStatement Call(string f) => JsStatement.Expression(JsExpr.Call(Name(f)));
    private static JsStatement Block(params JsStatement[] s) => JsStatement.Block(s);

    [Fact]
    public void Arrow_ExpressionBody_ParenthesizesAnObjectLiteral()
    {
        // `=> { a: 1 }` is a block with a label in it and returns undefined.
        JsExprWriter.Write(JsExpr.Arrow("s", JsExpr.Opaque("{ len: s.length }"))).Should().Be("(s) => ({ len: s.length })");
        JsExprWriter.Write(JsExpr.Arrow("x", JsExpr.Binary(Name("x"), "+", JsExpr.Literal("1")))).Should().Be("(x) => x + 1");
        JsExprWriter.Write(JsExpr.Arrow("", Name("v"), isAsync: true)).Should().Be("async () => v");
    }

    [Fact]
    public void Arrow_BlockBody_IsPlacedVerbatim_AndBindsLoosest()
    {
        JsExprWriter.Write(JsExpr.ArrowBlock("a, b", "{\n    return a;\n}")).Should().Be("(a, b) => {\n    return a;\n}");
        // As an operand the arrow is fenced: its body would otherwise swallow the rest.
        JsExprWriter.Write(JsExpr.Binary(JsExpr.Arrow("x", Name("x")), "||", Name("f"))).Should().Be("((x) => x) || f");
        JsExprWriter.Write(JsExpr.Call(JsExpr.Member(JsExpr.Arrow("x", Name("x")), "call"))).Should().Be("((x) => x).call()");
    }

    [Fact]
    public void Const_And_Headed()
    {
        var stmt = JsStatement.Headed("for (const x of xs)", Block(Call("f")));
        JsStatementWriter.Write(stmt, JsLayout.Compact).Should().Be("for (const x of xs) {f();}");
        JsStatementWriter.Write(stmt, JsLayout.Pretty).Should().Be("for (const x of xs) {\n    f();\n}");
        JsStatementWriter.Write(JsStatement.Const("row", JsExpr.ArrowBlock("i", "{\n    return i;\n}")), JsLayout.Pretty)
            .Should().Be("const row = (i) => {\n    return i;\n};");
        JsStatementWriter.Write(JsStatement.Headed("outer:", JsStatement.Headed("while (c)", Block(JsStatement.Break("outer")))), JsLayout.Compact)
            .Should().Be("outer: while (c) {break outer;}");
    }

    [Fact]
    public void Try_Catch_Finally()
    {
        var stmt = JsStatement.Try(Block(Call("a")), new[] { new JsCatch("(e: any)", Block(Call("b"))) }, Block(Call("c")));
        JsStatementWriter.Write(stmt, JsLayout.Compact).Should().Be("try {a();} catch (e: any) {b();} finally {c();}");
        JsStatementWriter.Write(stmt, JsLayout.Pretty)
            .Should().Be("try {\n    a();\n} catch (e: any) {\n    b();\n} finally {\n    c();\n}");
        var bare = JsStatement.Try(Block(Call("a")), new[] { new JsCatch("", Block()) }, null);
        JsStatementWriter.Write(bare, JsLayout.Compact).Should().Be("try {a();} catch {}");
    }

    [Fact]
    public void Switch_LabelsOneLevelIn_StatementsAnother()
    {
        var stmt = JsStatement.Switch(Name("k"), new[]
        {
            new JsCase(new[] { "case 1", "case 2" }, new[] { Call("a"), JsStatement.Break(null) }),
            new JsCase(new[] { "default" }, new[] { Call("d") }),
        });
        JsStatementWriter.Write(stmt, JsLayout.Compact)
            .Should().Be("switch (k) { case 1: case 2: a(); break; default: d(); }");
        JsStatementWriter.Write(stmt, JsLayout.Pretty)
            .Should().Be("switch (k) {\n    case 1:\n    case 2:\n        a();\n        break;\n    default:\n        d();\n}");
    }
}
