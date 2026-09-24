using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A struct is never null in C#: a record struct built with no arguments holds its members' zeros,
/// and a member of struct type is that struct's zero, not a null reference. The twin's constructor
/// defaults its parameters to the same thing — or a <c>new CodeGrid()</c> on the web held a null
/// Point and failed at its first read (found in review, #359).
/// </summary>
public class StructDefaultTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;

        public enum Tone { Quiet, Loud }

        public readonly record struct Cell(int Row, int Column);

        public readonly record struct Grid(Point Origin, Size Pitch, Cell Home, Tone Tone, char Mark);

        public struct Nothing { }

        public readonly record struct Wrap(Nothing Inner, int Count);
        """;

    private static string Emit(string name) => TypeScriptOf(Source, "Grid.cs", name);

    /// <summary>Compiled the way an app build compiles it: with the framework's own assemblies among
    /// the references, so a vocabulary or engine type is a SYMBOL the rules can ask about — the
    /// standalone parse cannot see one, and would test the fallback instead of the rule.</summary>
    private static string TypeScriptOf(string source, string file, string component)
    {
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source, path: file);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (Microsoft.CodeAnalysis.MetadataReference)Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p))
            .Append(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(eQuantic.UI.Code.CodeEditorController).Assembly.Location));
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Probe", [tree], references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: Microsoft.CodeAnalysis.NullableContextOptions.Enable));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, file).Single(r => r.ComponentName == component);
        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }


    [Fact]
    public void AStructMemberDefaultsToThatStructsZero()
    {
        var ts = Emit("Grid");

        // A vocabulary struct whose twin says it zero-constructs ([ZeroConstructs]) …
        ts.Should().Contain("origin: any = new Point()");
        ts.Should().Contain("pitch: any = new Size()");
        // … and a struct the compiler emits itself, whose own members default the same way.
        ts.Should().Contain("home: any = new Cell()");
    }

    private const string DefaultsSource = """
        using eQuantic.UI.Primitives;

        public readonly record struct Cell(int Row, int Column);

        public sealed record Defaults(int Seed)
        {
            public Cell Home() => default;
            public Point Origin() => default;
            public BoxStyle Style() => default;
            public SizeValue Width() => default;
            public SizeValue? MaybeWidth() => default;
        }
        """;

    /// <summary>
    /// A <c>default</c> literal is its type's zero wherever the twin can build one (#380), and where
    /// it cannot, a hand-written vocabulary twin not marked [ZeroConstructs], it is the twin's own
    /// default, <c>undefined</c>: the twin's constructor defaults (<c>style: BoxStyle = new
    /// BoxStyle()</c>) and its <c>!== undefined</c> checks apply for undefined and never for null.
    /// Only a nullable is null.
    /// </summary>
    [Theory]
    [InlineData("home", "new Cell()")]
    [InlineData("origin", "new Point()")]
    [InlineData("style", "undefined")]
    [InlineData("width", "undefined")]
    [InlineData("maybeWidth", "null")]
    public void ADefaultLiteral_IsTheZeroTheTwinCanBuild(string method, string expected)
    {
        var ts = TypeScriptOf(DefaultsSource, "Defaults.cs", "Defaults");

        ts.Should().MatchRegex($@"{method}\(\)[^{{]*\{{\s*return {System.Text.RegularExpressions.Regex.Escape(expected)};");
    }

    private const string ZerosSource = """
        using eQuantic.UI.Primitives;

        public readonly record struct Cell(int Row, int Column);

        public struct Counter { public int Step = 2; public long Total; public Counter() { } }

        public static class Board
        {
            public static Cell[] Row(int n) => new Cell[n];
            public static Cell Origin() => default;
            public static Counter Fresh() => default;
            public static BoxStyle[] Styles(int n) => new BoxStyle[n];
        }
        """;

    /// <summary>
    /// A struct whose construction gives a member more than its zero (an initializer, an explicit
    /// parameterless constructor) is not zeroed by its twin's bare <c>new T()</c>, which runs them:
    /// its zero passes every member's own (found in review, #405).
    /// </summary>
    [Fact]
    public void AStructThatConstructsBeyondZero_IsZeroedMemberByMember()
    {
        var ts = TypeScriptOf(ZerosSource, "Board.cs", "Board");

        ts.Should().MatchRegex(@"fresh\(\)[^{]*\{\s*return new Counter\(0, \$eq\.num\.long\(0\)\);");
    }

    /// <summary>A vocabulary struct whose twin cannot build its zero fills an array with its twin's
    /// own default, undefined, as a default literal of it does: its constructor's defaults apply for
    /// undefined and never for null (found in review, #405).</summary>
    [Fact]
    public void AnArrayOfAStructWithNoZeroTwin_HoldsTheTwinsOwnDefault()
    {
        var ts = TypeScriptOf(ZerosSource, "Board.cs", "Board");

        ts.Should().Contain("new Array(n).fill(undefined)");
    }

    /// <summary>The struct a zero names is IMPORTED, even compiled on its own (no per-app scan, the
    /// playground's mode): the constructor text alone was once all this checked, and the module it
    /// pinned threw "Cell is not defined" at its first default (found in review, #359).</summary>
    [Fact]
    public void TheAppStructAZeroNamesIsImported_WithoutAScan()
    {
        var ts = Emit("Grid");

        ts.Should().Contain("import { Cell } from \"./Cell\"");
    }

    [Fact]
    public void AnEnumAndACharDefaultToTheirZeros_NotToNull()
    {
        var ts = Emit("Grid");

        ts.Should().Contain("tone: any = 'quiet'");
        ts.Should().Contain("mark: any = '\\0'");
    }

    /// <summary>An EMPTY struct has no twin (the emitter refuses it), so `new Nothing()` would name
    /// a class nothing wrote (found in review, #359). Its member holds undefined, the answer for a
    /// struct no twin can build: C# has no null struct (#405).</summary>
    [Fact]
    public void AStructWithNoTwinIsNeverConstructed()
    {
        var ts = Emit("Wrap");

        ts.Should().Contain("inner: any = undefined");
        ts.Should().NotContain("new Nothing()");
    }

    [Fact]
    public void TheStructItNamesIsImported()
    {
        var ts = Emit("Grid");

        ts.Should().MatchRegex(@"import \{[^}]*\bPoint\b[^}]*\} from ""@equantic/runtime""");
        ts.Should().MatchRegex(@"import \{[^}]*\bSize\b[^}]*\} from ""@equantic/runtime""");
    }
}
