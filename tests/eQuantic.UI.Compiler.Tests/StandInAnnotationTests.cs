using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An annotation names a type the module can resolve. A type whose C# name names nothing in
/// TypeScript crosses as its stand-in (<c>TsStandIn</c>): an exception as the JavaScript Error that
/// carries its .NET types (<c>utils/exceptions.ts</c>), an interface as <c>any</c>, an enum as its
/// members and a delegate as its function. The code engine's completion was the first twin with a
/// list of providers, an event of exceptions and a list of them, and named
/// <c>ICodeCompletionProvider</c> and <c>Exception</c>, which no module defines (#296); every path
/// that writes an annotation answered a different part of the rule, so the module below goes through
/// all of them.
/// </summary>
public class StandInAnnotationTests
{
    private static string Emit(string members) => TestHelper.ConvertClass(members);

    [Fact]
    public void AnExceptionInsideADelegate_IsAnError()
    {
        var js = Emit("public event Action<Exception>? Failed;");

        js.Should().Contain("(exception: Error) => void");
        js.Should().NotContain(": Exception");
    }

    [Fact]
    public void AnExceptionParameter_IsAnError()
    {
        var js = Emit("public string Describe(InvalidOperationException error) => error.Message;");

        js.Should().Contain("error: Error");
    }

    [Fact]
    public void AnEmptyListOfExceptions_OrOfAnInterface_NamesWhatTheModuleHas()
    {
        var js = Emit("""
                public int Count()
                {
                    var errors = new List<Exception>();
                    var owners = new List<IDisposable>();
                    return errors.Count + owners.Count;
                }
            """);

        js.Should().Contain("let errors: Error[] = []");
        js.Should().Contain("let owners: any[] = []");
    }

    // ---- every path, one module --------------------------------------------------------------------

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;

        public interface IThing { int Size { get; } }
        public sealed class Thing : IThing { public int Size => 1; }
        public delegate int Measure(string text);
        public enum Shade { Light, Dark }

        public sealed record Failure(Exception Error, IThing Owner, Shade Shade, Measure Measure, Exception? Cause)
        {
            public bool Caused(Exception error, Measure measure) => error == Cause && measure == Measure;
        }

        public sealed class Probe
        {
            public int Run()
            {
                var actions = new List<Action>();
                var measures = new List<Measure>();
                var shades = new List<Shade>();
                var days = new List<DayOfWeek>();
                var counts = new List<long>();
                IThing thing = new Thing();
                Exception error = new InvalidOperationException("x");
                IThing? none = null;
                Exception? last = null;
                Measure? measure = null;
                int Weigh(Exception failure, IThing owner, Shade shade) => failure.Message.Length + owner.Size;
                return actions.Count + measures.Count + shades.Count + days.Count + counts.Count + thing.Size
                    + (none?.Size ?? 0) + (last is null ? 0 : 1) + (measure is null ? 0 : 1) + error.Message.Length
                    + Weigh(error, thing, Shade.Dark);
            }
        }
        """;

    private static Dictionary<string, string> Module()
    {
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(Source, "Probe.cs"));
        return compiler.CompileSource(Source, "Probe.cs").ToDictionary(result => result.ComponentName, result => result.TypeScript);
    }

    [Fact]
    public void NoAnnotation_NamesATypeTheModuleCannotResolve()
    {
        foreach (var (name, twin) in Module())
        {
            // A record's toString prints its members by their C# names, which is text and no type.
            var code = Regex.Replace(twin, @"toString\(\) \{ return `[^`]*`; \}", "");
            Regex.Matches(code, @"(?<![\w$.'])(Exception|IThing|Measure|Shade|DayOfWeek|Action)(?![\w$'])")
                .Select(match => code.Substring(Math.Max(0, match.Index - 30), Math.Min(60, code.Length - Math.Max(0, match.Index - 30))))
                .Should().BeEmpty($"{name} names only what its module has");
        }
    }

    /// <summary>
    /// The build context a helper class takes is the runtime's <c>BuildContext</c>, which the helper's
    /// module imports (#632). It was <c>RenderContext</c>, a name only the app-facing exports carry,
    /// and nothing imported it, so the shared library's first helper to take the context failed the
    /// runtime's own build.
    /// </summary>
    [Fact]
    public void TheBuildContext_IsTheRuntimesBuildContext_AndAHelperImportsIt()
    {
        const string helper = """
            using eQuantic.UI.Primitives;

            public static class Measures
            {
                public static float LineOf(ComponentContext context, TypeStyle style) =>
                    style.ScaledLineHeight(context.TypeScale);
            }
            """;
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(helper, "Measures.cs"));
        var twin = compiler.CompileSource(helper, "Measures.cs")
            .Single(result => result.ComponentName == "Measures").TypeScript;

        twin.Should().Contain("lineOf(context: BuildContext, ");
        twin.Should().MatchRegex(@"import \{[^}]*\bBuildContext\b[^}]*\} from");
        twin.Should().NotContain("RenderContext");
    }

    [Fact]
    public void ARecord_DeclaresItsMembersAndParameters_ByWhatTheyCrossAs()
    {
        var record = Module()["Failure"];

        record.Should().Contain("declare error: Error;");
        record.Should().Contain("declare owner: any;");
        record.Should().Contain("declare shade: string;");
        record.Should().Contain("declare measure: (text: string) => number;");
        record.Should().Contain("declare cause: Error | null;");
        record.Should().Contain("caused(error: Error, measure: (text: string) => number)");
    }

    [Fact]
    public void ALocal_IsAnnotatedByWhatItsTypeCrossesAs()
    {
        var probe = Module()["Probe"];

        probe.Should().Contain("let actions: (() => void)[] = []");
        probe.Should().Contain("let measures: ((text: string) => number)[] = []");
        probe.Should().Contain("let shades: string[] = []");
        probe.Should().Contain("let days: string[] = []");
        probe.Should().Contain("let counts: bigint[] = []", "a long is a bigint, where this called it a number");
        probe.Should().Contain("let thing: any = new Thing()");
        probe.Should().Contain("let error: Error = ");
        probe.Should().Contain("let none: any = null");
        probe.Should().Contain("let last: Error | null = null");
        probe.Should().Contain("let measure: ((text: string) => number) | null = null");
        probe.Should().Contain("(failure: Error, owner: any, shade: string) =>");
    }
}
