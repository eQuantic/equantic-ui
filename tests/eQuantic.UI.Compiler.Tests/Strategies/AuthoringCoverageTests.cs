using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Regression tests graduated from the authoring-coverage sweep: a developer can declare component
/// properties (computed, get/set, static, auto with defaults) and constructors with bodies, and reference
/// primitive types, without the transpiler dropping members or inventing bogus imports.
/// </summary>
public class AuthoringCoverageTests
{
    private const string Header =
        "using System; using System.Collections.Generic; using eQuantic.UI.Core; using eQuantic.UI.Components; namespace App; ";

    private static string Ts(string body) =>
        new ComponentCompiler().CompileSource(Header + body).Single(r => r.ComponentName == "C").TypeScript;

    [Fact]
    public void ComputedGetOnlyProperty_EmitsGetter()
    {
        var ts = Ts("public class C : StatelessComponent { public int N { get; set; } public int Double => N * 2; " +
                    "public override IComponent Build(RenderContext c) => new Text(Double.ToString()); }");
        ts.Should().Contain("get double() { return this.n * 2; }");
    }

    [Fact]
    public void GetAccessorWithBody_EmitsGetter()
    {
        var ts = Ts("public class C : StatelessComponent { public string T { get; set; } " +
                    "public string Upper { get { return T.ToUpper(); } } " +
                    "public override IComponent Build(RenderContext c) => new Text(Upper); }");
        ts.Should().Contain("get upper() {");
        ts.Should().Contain("toUpperCase()");
    }

    [Fact]
    public void StaticComputedProperty_EmitsStaticGetter()
    {
        var ts = Ts("public class C : StatelessComponent { public static string AppName => \"eQ\"; " +
                    "public override IComponent Build(RenderContext c) => new Text(AppName); }");
        ts.Should().Contain("static get appName() { return 'eQ'; }");
    }

    [Fact]
    public void AutoPropertyDefault_AppliedInConstructor()
    {
        var ts = Ts("public class C : StatelessComponent { public string Title { get; set; } = \"Hello\"; " +
                    "public override IComponent Build(RenderContext c) => new Text(Title); }");
        // Default applied only if a prop wasn't supplied (base Object.assign runs first).
        ts.Should().Contain("if (this.title === undefined) this.title = 'Hello';");
    }

    [Fact]
    public void ConstructorBody_IsRun()
    {
        var ts = Ts("public class C : StatelessComponent { private readonly int _id; private readonly string _name; " +
                    "public C(int id, string name) { _id = id; _name = name; } " +
                    "public override IComponent Build(RenderContext c) => new Text($\"{_id} {_name}\"); }");
        ts.Should().Contain("this._id = id");
        ts.Should().Contain("this._name = name");
    }

    [Fact]
    public void PrimitiveTypes_AreNotImported()
    {
        // A property/var of a C# primitive type must never become `import { int } from "./int"`.
        var ts = Ts("public class C : StatelessComponent { public int N { get; set; } public int Double => N * 2; " +
                    "public override IComponent Build(RenderContext c) => new Text(Double.ToString()); }");
        ts.Should().NotContain("from \"./int\"");
        ts.Should().NotContain("from \"./number\"");
    }
}
