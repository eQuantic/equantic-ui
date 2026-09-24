using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// Where each statement that carries an origin lands in the text the writer produced (#293): the
/// mark is what the source map turns into a segment, so a stack frame or a breakpoint in a member's
/// body leads to the C# statement, not to the member's first line.
/// </summary>
public class JsStatementWriterMarksTests
{
    private static readonly StatementSyntax[] Origins = CSharpSyntaxTree
        .ParseText("class C { void M() { int a = 1; if (a > 0) { a++; } switch (a) { case 1: a--; break; } return; } }")
        .GetRoot().DescendantNodes().OfType<StatementSyntax>().Where(s => s is not BlockSyntax).ToArray();

    private static T Origin<T>(int index = 0) where T : StatementSyntax => Origins.OfType<T>().ElementAt(index);

    private static JsStatement Call(string f) => JsStatement.Expression(JsExpr.Call(JsExpr.Identifier(f)));

    [Fact]
    public void EachStatement_IsMarkedWhereItBegins_NestedOnesIncluded()
    {
        var block = JsStatement.Block([
            JsStatement.Raw("let a = 1;") with { Origin = Origin<LocalDeclarationStatementSyntax>() },
            JsStatement.If(JsExpr.Binary(JsExpr.Identifier("a"), ">", JsExpr.Literal("0")),
                JsStatement.Block([JsStatement.Raw("a++;") with { Origin = Origin<ExpressionStatementSyntax>() }]), null)
                with { Origin = Origin<IfStatementSyntax>() },
            JsStatement.Switch(JsExpr.Identifier("a"),
                [new JsCase(["case 1"], [JsStatement.Raw("a--;") with { Origin = Origin<ExpressionStatementSyntax>(1) }, JsStatement.Break(null)])])
                with { Origin = Origin<SwitchStatementSyntax>() },
            JsStatement.Return(null) with { Origin = Origin<ReturnStatementSyntax>() },
        ]);

        var marks = new List<JsLineMark>();
        var text = JsStatementWriter.WriteMarked(block, JsLayout.Pretty, 0, marks);

        text.Should().Be(JsStatementWriter.Write(block, JsLayout.Pretty),
            "marking a statement changes nothing the writer writes");
        text.Should().Be("{\n    let a = 1;\n    if (a > 0) {\n        a++;\n    }\n    switch (a) {\n        case 1:\n            a--;\n            break;\n    }\n    return;\n}");
        marks.Select(mark => (mark.Line, mark.Column, mark.Origin)).Should().Equal(
            (1, 4, (SyntaxNode)Origin<LocalDeclarationStatementSyntax>()),
            (2, 4, Origin<IfStatementSyntax>()),
            (3, 8, Origin<ExpressionStatementSyntax>()),
            (5, 4, Origin<SwitchStatementSyntax>()),
            (7, 12, Origin<ExpressionStatementSyntax>(1)),
            (10, 4, Origin<ReturnStatementSyntax>()));
    }

    [Fact]
    public void AStatementThatWritesNothing_TakesNoMark()
    {
        var marks = new List<JsLineMark>();
        JsStatementWriter.WriteMarked(JsStatement.Block([JsStatement.Empty with { Origin = Origin<ReturnStatementSyntax>() }, Call("go")]),
            JsLayout.Pretty, 0, marks);
        marks.Should().BeEmpty("an empty statement takes no line, so there is nothing to stop on");
    }

    [Fact]
    public void AMemberBody_IsMarkedFromTheMembersOwnText()
    {
        var marks = new List<JsLineMark>();
        var text = JsMemberWriter.WriteMarked(JsClassMember.Method("", "run", "", "", "",
            JsStatement.Block([Call("first") with { Origin = Origin<LocalDeclarationStatementSyntax>() },
                Call("second") with { Origin = Origin<ReturnStatementSyntax>() }])), JsLayout.Pretty, marks);

        text.Should().Be("run() {\n    first();\n    second();\n}");
        marks.Select(mark => (mark.Line, mark.Column)).Should().Equal((1, 4), (2, 4));
    }

    [Fact]
    public void AStatementEqualsTheSameStatementFromAnywhere()
    {
        // The origin is where it came from, not what it is: nothing that compares statements by
        // value may start telling two identical ones apart.
        (JsStatement.Return(null) with { Origin = Origin<ReturnStatementSyntax>() }).Should().Be(JsStatement.Return(null));
    }
}
