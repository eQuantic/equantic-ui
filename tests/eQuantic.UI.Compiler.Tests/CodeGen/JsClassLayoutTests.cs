using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>The class writer on hand-built IR: the header forms, and the one layout rule for the
/// body — fields contiguous, a blank line before a member with a body and before a field that
/// follows one, nothing after the last member.</summary>
public class JsClassLayoutTests
{
    private static JsStatement Call(string f) => JsStatement.Expression(JsExpr.Call(JsExpr.Identifier(f)));

    private static string Write(JsClass jsClass, bool typeAnnotations = true)
    {
        var builder = new TypeScriptCodeBuilder { TypeAnnotations = typeAnnotations };
        builder.Write(jsClass);
        return builder.ToString();
    }

    [Fact]
    public void TheHeader_CarriesExportAbstractGenericsAndBase()
    {
        var jsClass = new JsClass("Page", "StatelessComponent", ["T"], Export: true, Abstract: true, Members: []);
        Write(jsClass).Should().Be("export abstract class Page<T> extends StatelessComponent {\n}\n\n");
        // Plain JavaScript has no `abstract`; the keyword is dropped, nothing else moves.
        Write(jsClass, typeAnnotations: false).Should().Be("export class Page<T> extends StatelessComponent {\n}\n\n");
        Write(new JsClass("Helper", null, [], Export: false, Abstract: false, Members: []))
            .Should().Be("class Helper {\n}\n\n");
    }

    [Fact]
    public void TheBody_FieldsContiguous_BlankLinesAroundMembersWithBodies()
    {
        var jsClass = new JsClass("C", null, [], Export: true, Abstract: false, Members:
        [
            JsClassMember.Field("declare ", "a", ": number"),
            JsClassMember.Field("", "b", "", "1"),
            JsClassMember.Constructor("", JsStatement.Block([Call("init")])),
            JsClassMember.Getter("", "x", "", JsStatement.Return(JsExpr.ThisMember("a"))),
            JsClassMember.Field("static ", "count", ": number", "0"),
            JsClassMember.Method("", "run", "", "", "", JsStatement.Block([Call("go")])),
        ]);

        Write(jsClass).Should().Be(
            "export class C {\n" +
            "    declare a: number;\n" +
            "    b = 1;\n" +
            "\n" +
            "    constructor() {\n" +
            "        init();\n" +
            "    }\n" +
            "\n" +
            "    get x() {\n" +
            "        return this.a;\n" +
            "    }\n" +
            "\n" +
            "    static count: number = 0;\n" +
            "\n" +
            "    run() {\n" +
            "        go();\n" +
            "    }\n" +
            "}\n" +
            "\n");
    }

    [Fact]
    public void TheCallbackForm_CollectsTheSameClass()
    {
        var builder = new TypeScriptCodeBuilder();
        builder.Class("C", null, c =>
        {
            c.Field("a", "number", isDeclare: true);
            c.Member(JsClassMember.Method("", "run", "", "", "", JsStatement.Block([Call("go")])));
        });

        builder.ToString().Should().Be("export class C {\n    declare a: number;\n\n    run() {\n        go();\n    }\n}\n\n");
    }
}
