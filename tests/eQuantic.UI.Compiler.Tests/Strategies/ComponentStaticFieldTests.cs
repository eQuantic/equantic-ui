using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Static/const data fields on a component must be emitted as <c>static</c> class members (with transpiled
/// initializers), referenced as <c>ClassName.field</c> (never <c>this.</c>), and <c>.Count</c> must map to
/// <c>.length</c> — not the bogus enum literal <c>'count'</c>. A developer can legitimately author page
/// data this way; the transpiler must cover it.
/// </summary>
public class ComponentStaticFieldTests
{
    private readonly ITestOutputHelper _out;
    public ComponentStaticFieldTests(ITestOutputHelper o) => _out = o;

    [Fact]
    public void StaticField_OnComponent()
    {
        var src = @"
using System.Collections.Generic;
using eQuantic.UI.Web;
using eQuantic.UI.Web.Components;
namespace App;
public class Widget : StatelessComponent {
    private static readonly List<string> Items = new() { ""a"", ""b"", ""c"" };
    private const int Limit = 5;
    public override IComponent Build(RenderContext context) {
        var box = new Box();
        foreach (var it in Items) box.Children.Add(new Text(it));
        return new Box { Children = { new Text($""{Items.Count} of {Limit}"") } };
    }
}";
        var result = new ComponentCompiler().CompileSource(src).Single(r => r.ComponentName == "Widget");
        _out.WriteLine("=== Widget.ts ===");
        _out.WriteLine(result.TypeScript);
        // Static field emitted as a static class member, with its initializer transpiled.
        result.TypeScript.Should().Contain("static items");
        result.TypeScript.Should().Contain("static limit");
        // Static access qualified by the class name, never `this.` (which would be undefined at runtime).
        result.TypeScript.Should().Contain("Widget.items");
        result.TypeScript.Should().NotContain("this.items");
        result.TypeScript.Should().NotContain("this.limit");
        // .Count on the static collection → .length (and NOT the bogus enum literal 'count').
        result.TypeScript.Should().Contain("Widget.items.length");
        result.TypeScript.Should().NotContain("'count'");
    }
}
