using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>The module writer's rules on hand-built IR: one line per import with its names
/// ordered, a blank line after the imports when there were any, nothing imported for nothing.</summary>
public class JsModuleWriterTests
{
    [Fact]
    public void Imports_ThenABlankLine_ThenTheBody()
    {
        var module = new JsModule(new[]
        {
            new JsImport(new[] { "VisualNode", "Box", "StatelessComponent" }, "@equantic/runtime"),
            new JsImport(new[] { "Card" }, "./Card"),
        }, "export class Page extends StatelessComponent {\n}\n");

        JsModuleWriter.Write(module).Should().Be(
            "import { Box, StatelessComponent, VisualNode } from \"@equantic/runtime\";\n" +
            "import { Card } from \"./Card\";\n" +
            "\n" +
            "export class Page extends StatelessComponent {\n}\n");
    }

    [Fact]
    public void NoImports_NoBlankLine_TheBodyStartsTheFile()
    {
        var module = new JsModule(Array.Empty<JsImport>(), "export class Plain {\n}\n");
        JsModuleWriter.Write(module).Should().Be("export class Plain {\n}\n");
    }

    [Fact]
    public void AnImportWithNoNames_IsNotWritten()
    {
        var module = new JsModule(new[] { new JsImport(Array.Empty<string>(), "@equantic/runtime") }, "x");
        JsModuleWriter.Write(module).Should().Be("x");
    }
}
