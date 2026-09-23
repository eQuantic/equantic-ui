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
    /// a class nothing wrote: its member keeps the null it always had (found in review, #359).</summary>
    [Fact]
    public void AStructWithNoTwinIsNeverConstructed()
    {
        var ts = Emit("Wrap");

        ts.Should().Contain("inner: any = null");
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
