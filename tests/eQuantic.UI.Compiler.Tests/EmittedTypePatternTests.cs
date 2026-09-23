using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type pattern over a type whose twin the compiler EMITS is an <c>instanceof</c> test. The code
/// engine is transpiled whole, so every class and record in it is a real JavaScript class on the
/// other side — but the type-test path knew only the vocabulary's nodes, and `value is
/// CodeEditorController` lowered to `value != null`, true for ANY object (found in review, #359).
/// The app's own structs are the same case: their twins are emitted classes too, and the path let
/// only classes through.
/// </summary>
public class EmittedTypePatternTests
{
    private const string Source = """
        using eQuantic.UI.Code;
        using eQuantic.UI.Primitives;

        public sealed class Probe
        {
            public string What(object value)
            {
                if (value is CodeEditorController) return "editor";
                if (value is CodeRange range) return "range " + range.Anchor.Line;
                return "other";
            }
        }
        """;

    [Fact]
    public void AnEngineClassAndAnEngineRecordAreTestedByInstanceof()
    {
        var ts = TypeScriptOf(Source, "Probe.cs", "Probe");

        ts.Should().Contain("instanceof CodeEditorController");
        ts.Should().Contain("instanceof CodeRange");
        ts.Should().NotContain("value != null");
        // The class it tests against has to be in scope, or the test throws a ReferenceError.
        ts.Should().MatchRegex(@"import \{[^}]*\bCodeEditorController\b[^}]*\} from ""@equantic/runtime""");
    }

    [Fact]
    public void AnAppStructIsTestedByInstanceof()
    {
        var ts = TypeScriptOf("""
            public readonly record struct Span2(int Start, int Length);

            public sealed class Probe
            {
                public int What(object value) => value is Span2 span ? span.Length : -1;
            }
            """, "Probe.cs", "Probe");

        ts.Should().Contain("instanceof Span2");
        ts.Should().NotContain("value != null");
    }

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
}
