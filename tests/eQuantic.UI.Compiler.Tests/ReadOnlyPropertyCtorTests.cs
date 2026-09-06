using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// The constructor protocol assigns a parameter onto the same-named property. It must not do that
/// when the twin has no slot to write.
/// <para>
/// A private field, a read-only property over it, and a constructor parameter of the same name is
/// ordinary C# — it is how anything with computed or validated state is written. Emitted naively
/// the class carried <c>get series()</c> AND <c>this.series = series</c>: TS2540 to the type
/// checker, and because a class body is strict-mode code, a TypeError thrown at <c>new</c>. The
/// component rendered perfectly on the server and died at hydration.
/// </para>
/// <para>
/// The value is not lost by skipping. The author's own constructor body writes the field, which is
/// the whole point of a read-only property.
/// </para>
/// </summary>
public class ReadOnlyPropertyCtorTests
{
    private static string TypeScriptOf(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").Single();
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    private const string Head = """
        using System;
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;

        namespace Demo;

        """;

    /// <summary>The reported shape, from the first chart: `_series`, `Series => _series`, `series`.</summary>
    [Fact]
    public void AnExpressionBodiedProperty_GetsNoAssignment_AndTheBodyStillWritesTheField()
    {
        var ts = TypeScriptOf(Head + """
            public sealed class Chart : StatelessComponent
            {
                private readonly IReadOnlyList<string> _series;

                public Chart(IReadOnlyList<string> series) => _series = series;

                public IReadOnlyList<string> Series => _series;

                public override VisualNode Build(ComponentContext context) =>
                    new Text(_series.Count.ToString(), TypeRole.BodyM);
            }
            """);

        Assert.Contains("get series()", ts);
        Assert.DoesNotContain("this.series = series", ts);
        // The parameter still lands, through the constructor the author wrote.
        Assert.Contains("this._series = series", ts);
    }

    /// <summary>The same shape spelled with a bodied getter rather than an expression body.</summary>
    [Fact]
    public void ABodiedGetterWithNoSetter_GetsNoAssignmentEither()
    {
        var ts = TypeScriptOf(Head + """
            public sealed class Gauge : StatelessComponent
            {
                private readonly int _value;

                public Gauge(int value)
                {
                    _value = value;
                }

                public int Value { get { return _value; } }

                public override VisualNode Build(ComponentContext context) =>
                    new Text(_value.ToString(), TypeRole.BodyM);
            }
            """);

        Assert.Contains("get value()", ts);
        Assert.DoesNotContain("this.value = value", ts);
        Assert.Contains("this._value = value", ts);
    }

    /// <summary>
    /// The other half of the rule, so the fix cannot be "never assign": a property the twin emits a
    /// SETTER for is written exactly as before. A computed property is not automatically read-only.
    /// </summary>
    [Fact]
    public void APropertyWithASetter_IsStillAssigned()
    {
        var ts = TypeScriptOf(Head + """
            public sealed class Slider : StatelessComponent
            {
                private int _amount;

                public Slider(int amount)
                {
                    _amount = amount;
                }

                public int Amount
                {
                    get { return _amount; }
                    set { _amount = value; }
                }

                public override VisualNode Build(ComponentContext context) =>
                    new Text(_amount.ToString(), TypeRole.BodyM);
            }
            """);

        Assert.Contains("set amount(", ts);
        Assert.Contains("this.amount = amount", ts);
    }

    /// <summary>
    /// The defect the reported shape was hiding behind: a constructor written with an ARROW had no
    /// block, the emitter read only the block, and the author's wiring never reached the twin. The
    /// C# constructor assigned a field and its twin ran a constructor that did nothing.
    /// </summary>
    [Fact]
    public void AnExpressionBodiedConstructor_StillRunsItsBody()
    {
        var ts = TypeScriptOf(Head + """
            public sealed class Ticker : StatelessComponent
            {
                private readonly int _count;

                public Ticker(int count) => _count = count;

                public override VisualNode Build(ComponentContext context) =>
                    new Text(_count.ToString(), TypeRole.BodyM);
            }
            """);

        Assert.Contains("this._count = count", ts);
    }

    /// <summary>
    /// The RULE, rather than one spelling of it: whatever the emitter writes a getter for, nothing
    /// may assign. Stated over the emitted module itself, so a future change that reaches the same
    /// wrong output by another route fails here even if the string this file asserts elsewhere has
    /// moved on. Both defects are the same sentence in JavaScript: a getter with no setter is not a
    /// slot, and a class body is strict-mode code, so writing to one throws instead of being ignored.
    /// </summary>
    [Fact]
    public void NothingTheEmitterGivesAGetter_IsEverAssigned()
    {
        var ts = TypeScriptOf(Head + """
            public sealed class Panel : StatelessComponent
            {
                private readonly string _caption;
                private readonly int _weight;

                public Panel(string caption, int weight)
                {
                    _caption = caption;
                    _weight = weight;
                }

                public string Caption => _caption;
                public int Weight { get { return _weight; } }
                public bool Wide => _weight > 10;

                public override VisualNode Build(ComponentContext context) =>
                    new Text(_caption, TypeRole.BodyM);
            }
            """);

        var getters = Regex.Matches(ts, @"^\s*get\s+(\w+)\s*\(", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.NotEmpty(getters);

        var setters = Regex.Matches(ts, @"^\s*set\s+(\w+)\s*\(", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        foreach (var name in getters.Where(g => !setters.Contains(g)))
            Assert.DoesNotContain($"this.{name} =", ts);
    }

    /// <summary>And an ordinary auto-property, which the twin fills from outside, keeps its assignment.</summary>
    [Fact]
    public void AnAutoProperty_IsStillAssigned()
    {
        var ts = TypeScriptOf(Head + """
            public sealed class Label : StatelessComponent
            {
                public Label(string caption)
                {
                    Caption = caption;
                }

                public string Caption { get; init; }

                public override VisualNode Build(ComponentContext context) =>
                    new Text(Caption, TypeRole.BodyM);
            }
            """);

        Assert.Contains("this.caption = caption", ts);
    }
}
