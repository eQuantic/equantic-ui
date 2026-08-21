using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// The statement writer's two layouts, on hand-built IR. Compact reproduces the string world byte
/// for byte (a migrated strategy changes nothing until the layout is switched); Pretty puts one
/// statement per line and indents blocks — and never re-indents a raw statement's own lines.
/// </summary>
public class JsStatementWriterTests
{
    private static JsExpr Name(string n) => JsExpr.Identifier(n);
    private static JsStatement Call(string f) => JsStatement.Expression(JsExpr.Call(Name(f)));

    private static readonly JsStatement Sample = JsStatement.Block(new[]
    {
        JsStatement.Let("x", "", JsExpr.Literal("1")),
        JsStatement.If(JsExpr.Binary(Name("x"), ">", JsExpr.Literal("0")),
            JsStatement.Block(new[] { Call("a") }),
            JsStatement.Block(new[] { Call("b"), JsStatement.Return(Name("x")) })),
        JsStatement.While(Name("go"), JsStatement.Block(new[] { JsStatement.Break(null) })),
        JsStatement.Return(null),
    });

    [Fact]
    public void Compact_IsTheStringWorld_ByteForByte()
    {
        JsStatementWriter.Write(Sample, JsLayout.Compact)
            .Should().Be("{let x = 1;if (x > 0) {a();} else {b();return x;}while (go) {break;}return;}");
    }

    [Fact]
    public void Pretty_OneStatementPerLine_BlocksIndented()
    {
        JsStatementWriter.Write(Sample, JsLayout.Pretty).Should().Be(
            "{\n" +
            "    let x = 1;\n" +
            "    if (x > 0) {\n" +
            "        a();\n" +
            "    } else {\n" +
            "        b();\n" +
            "        return x;\n" +
            "    }\n" +
            "    while (go) {\n" +
            "        break;\n" +
            "    }\n" +
            "    return;\n" +
            "}");
    }

    [Fact]
    public void Pretty_StartsAtTheGivenDepth()
    {
        var block = JsStatement.Block(new[] { Call("a") });
        JsStatementWriter.Write(block, JsLayout.Pretty, depth: 2)
            .Should().Be("{\n            a();\n        }");
    }

    [Fact]
    public void BracelessBodies_StayInline_InBothLayouts()
    {
        var stmt = JsStatement.If(Name("c"), JsStatement.Return(null), JsStatement.Continue("outer"));
        JsStatementWriter.Write(stmt, JsLayout.Compact).Should().Be("if (c) return; else continue outer;");
        JsStatementWriter.Write(stmt, JsLayout.Pretty).Should().Be("if (c) return; else continue outer;");
    }

    [Fact]
    public void ElseIf_IsAnIfInTheElsePosition()
    {
        var chain = JsStatement.If(Name("a"), JsStatement.Block(new[] { Call("x") }),
            JsStatement.If(Name("b"), JsStatement.Block(new[] { Call("y") }), null));
        JsStatementWriter.Write(chain, JsLayout.Pretty)
            .Should().Be("if (a) {\n    x();\n} else if (b) {\n    y();\n}");
    }

    [Fact]
    public void ASequence_IsSeveralStatementsInOnePlace_NoBracesOfItsOwn()
    {
        var seq = JsStatement.Sequence(JsStatement.Let("a", "", JsExpr.Literal("1")), JsStatement.Let("b", ": number", JsExpr.Literal("2")));
        JsStatementWriter.Write(seq, JsLayout.Compact).Should().Be("let a = 1;let b: number = 2;");
        JsStatementWriter.Write(JsStatement.Block(new[] { seq }), JsLayout.Pretty)
            .Should().Be("{\n    let a = 1;\n    let b: number = 2;\n}");
    }

    [Fact]
    public void Hoisted_PutsTheDeclarationsInFront_OrNothing()
    {
        JsStatementWriter.Write(JsStatement.Hoisted("let t;", JsStatement.Return(Name("t"))), JsLayout.Compact)
            .Should().Be("let t;return t;");
        JsStatementWriter.Write(JsStatement.Hoisted("", JsStatement.Return(Name("t"))), JsLayout.Compact)
            .Should().Be("return t;");
    }

    [Fact]
    public void EmptyStatements_TakeNoLine_AndAnEmptyBlockStaysClosed()
    {
        var block = JsStatement.Block(new[] { JsStatement.Empty, Call("a"), JsStatement.Empty });
        JsStatementWriter.Write(block, JsLayout.Pretty).Should().Be("{\n    a();\n}");
        JsStatementWriter.Write(JsStatement.Block(Array.Empty<JsStatement>()), JsLayout.Pretty).Should().Be("{}");
        JsStatementWriter.Write(JsStatement.Block(Array.Empty<JsStatement>()), JsLayout.Compact).Should().Be("{}");
    }

    [Fact]
    public void RawText_IsPlacedVerbatim_ItsOwnLinesNeverReindented()
    {
        // A raw statement may span lines (a template literal with real newlines inside): the
        // writer indents where it STARTS and touches nothing after, or it would edit the string.
        var raw = JsStatement.Raw("const s = `a\nb`;");
        JsStatementWriter.Write(JsStatement.Block(new[] { raw, Call("f") }), JsLayout.Pretty)
            .Should().Be("{\n    const s = `a\nb`;\n    f();\n}");
    }
}
