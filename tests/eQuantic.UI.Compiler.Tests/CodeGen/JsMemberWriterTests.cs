using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>The member writer's assembly rules, on hand-built IR: modifiers, keyword, braces,
/// and a body that is either a block laying itself out or text placed one level in.</summary>
public class JsMemberWriterTests
{
    private static JsStatement Call(string f) => JsStatement.Expression(JsExpr.Call(JsExpr.Identifier(f)));
    private static string Write(JsClassMember m) => JsMemberWriter.Write(m, JsLayout.Pretty);

    [Fact]
    public void Fields_WithAndWithoutInitializer()
    {
        Write(JsClassMember.Field("static ", "count", ": number", "0")).Should().Be("static count: number = 0;");
        Write(JsClassMember.Field("declare ", "title", ": string")).Should().Be("declare title: string;");
        Write(JsClassMember.Field("abstract ", "size", ": number")).Should().Be("abstract size: number;");
        Write(JsClassMember.Field("", "items", "")).Should().Be("items;");
    }

    [Fact]
    public void Accessors_GetAndSet()
    {
        Write(JsClassMember.Getter("static ", "name", ": string", JsStatement.Block(new[] { JsStatement.Return(JsExpr.Literal("'x'")) })))
            .Should().Be("static get name(): string {\n    return 'x';\n}");
        Write(JsClassMember.Setter("", "name", "value: string", JsStatement.Block(new[] { Call("store") })))
            .Should().Be("set name(value: string) {\n    store();\n}");
    }

    [Fact]
    public void Methods_AndTheConstructor()
    {
        Write(JsClassMember.Method("static async ", "load", "<T>", "id: number", ": Promise<T>", JsStatement.Block(new[] { Call("go") })))
            .Should().Be("static async load<T>(id: number): Promise<T> {\n    go();\n}");
        Write(JsClassMember.Constructor("props?: any", JsStatement.Block(new[] { Call("init") })))
            .Should().Be("constructor(props?: any) {\n    init();\n}");
    }

    [Fact]
    public void ARawBody_IsPlacedOneLevelIn_LineByLine_AndEmptyStaysClosed()
    {
        // Text the emitter still assembles — with its own lines — goes between the braces exactly
        // as a block's contents would, which is what keeps the two families byte-identical.
        Write(JsClassMember.Method("", "m", "", "", "", JsStatement.Raw("a();\nif (x) {\n    b();\n}")))
            .Should().Be("m() {\n    a();\n    if (x) {\n        b();\n    }\n}");
        Write(JsClassMember.Method("", "m", "", "", "", JsStatement.Raw(""))).Should().Be("m() {}");
        Write(JsClassMember.Method("", "m", "", "", "", JsStatement.Block(Array.Empty<JsStatement>()))).Should().Be("m() {}");
    }

    [Fact]
    public void ARawMember_IsTheSeam()
    {
        Write(JsClassMember.Raw("get x() { return this.$x; }")).Should().Be("get x() { return this.$x; }");
    }
}
