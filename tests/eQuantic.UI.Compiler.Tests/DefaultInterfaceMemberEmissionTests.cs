using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A default interface member reaches the twin of every class that relies on it (#414), whichever
/// emitter writes the class: a plain class, as <c>PlainTextLanguage</c> is, and a component. The
/// record and struct path is executed on both sides by <c>DefaultInterfaceMemberConformanceTests</c>.
/// </summary>
public class DefaultInterfaceMemberEmissionTests
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["IStyled.cs"] = """
            namespace App;
            public interface IStyled
            {
                string Name { get; }
                Spacing Gaps => new Spacing(4);
                string Caption() => Name + "!";
            }
            """,
        // Spacing is named only by the interface's default, never by a class that relies on it.
        ["Spacing.cs"] = "namespace App; public sealed record Spacing(int Width);",
        ["Plain.cs"] = """
            namespace App;
            public sealed class Plain : IStyled
            {
                public string Name => "plain";
            }
            """,
        ["Custom.cs"] = """
            namespace App;
            public sealed class Custom : IStyled
            {
                public string Name => "custom";
                public Spacing Gaps => new Spacing(8);
            }
            """,
        ["Badge.cs"] = """
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Badge : StatelessComponent, IStyled
            {
                public string Name => "badge";
                public override VisualNode Build(ComponentContext context) => new Text(((IStyled)this).Caption());
            }
            """,
        // An app's own language, against the code engine's ASSEMBLY: ICodeLanguage is metadata here,
        // as it is in every app, and the runtime carries its defaults.
        ["Words.cs"] = """
            using System.Collections.Generic;
            using eQuantic.UI.Code;
            namespace App;
            public sealed class Words : ICodeLanguage
            {
                public string Name => "words";
                public int Tokenize(string line, int state, List<CodeToken> into) => state;
            }
            """,
        // Compiled into a referenced assembly below, not into this compilation.
        ["Voiced.cs"] = """
            namespace App;
            public sealed class Voiced : Lib.IVoice
            {
                public string Word => "hello";
            }
            """,
    };

    private const string Library = """
        namespace Lib;
        public interface IVoice
        {
            string Word { get; }
            string Say() => Word + ".";
        }
        """;

    /// <summary>Compiled the way eqc compiles an app, with <see cref="Library"/> as a referenced
    /// assembly: its interface has no syntax in the app's compilation, only metadata.</summary>
    private static CompilationResult Compile(string component)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-default-members-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToList();
            var library = CSharpCompilation.Create("Lib", [CSharpSyntaxTree.ParseText(Library)], platform,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var image = new MemoryStream();
            library.Emit(image).Success.Should().BeTrue();

            var trees = Files.Select(file =>
            {
                var path = Path.Combine(dir, file.Key);
                File.WriteAllText(path, file.Value);
                return CSharpSyntaxTree.ParseText(file.Value, path: path);
            }).ToList();
            var references = platform
                .Append(MetadataReference.CreateFromImage(image.ToArray()))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Components.CodeEditor).Assembly.Location))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Code.ICodeLanguage).Assembly.Location));
            var compilation = CSharpCompilation.Create("App", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var result = compiler.CompileFile(Path.Combine(dir, component + ".cs"))
                .Single(r => r.ComponentName == component);
            result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
            return result;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void APlainClassTakesTheDefaultsItDoesNotDeclare()
    {
        var ts = Compile("Plain").TypeScript;

        ts.Should().MatchRegex(@"get gaps\(\)(: Spacing)? \{\s*return new Spacing\(4\);");
        // The default reads the class's own member through `this`, as the interface's `Name` does.
        ts.Should().MatchRegex(@"caption\(\)(: string)? \{\s*return \(this\.name \?\? ''\) \+ '!';");
        // Named only by the default: the scan that decides the imports has to read it too.
        ts.Should().Contain("import { Spacing } from \"./Spacing\"");
    }

    [Fact]
    public void AMemberTheClassDeclaresIsNotWrittenTwice()
    {
        var ts = Compile("Custom").TypeScript;

        ts.Should().Contain("new Spacing(8)");
        ts.Should().NotContain("new Spacing(4)");
        ts.Should().MatchRegex(@"caption\(\)");
    }

    [Fact]
    public void AComponentTakesTheDefaultsItDoesNotDeclare()
    {
        var ts = Compile("Badge").TypeScript;

        ts.Should().MatchRegex(@"caption\(\)(: string)? \{\s*return");
        ts.Should().MatchRegex(@"get gaps\(\)");
    }

    /// <summary>
    /// A vocabulary default reaches an APP's class as a delegation to the runtime's copy: the app
    /// compiles against the vocabulary's assembly, where the interface has no body to convert. An
    /// app's language without its own Rules is the #414 crash moved from the SDK's class to the
    /// app's, and a themed app took three warnings on every build (measured on the site).
    /// </summary>
    [Fact]
    public void AVocabularyDefaultIsDelegatedToTheRuntime()
    {
        var result = Compile("Words");

        result.TypeScript.Should().MatchRegex(@"get rules\(\)(: \w+)? \{\s*return ICodeLanguage\.rules\(this\);");
        result.TypeScript.Should().MatchRegex(@"import \{[^}]*\bICodeLanguage\b[^}]*\} from ""@equantic/runtime""");
        result.Warnings.Should().NotContain(warning => warning.Code == "EQ1008");
    }

    /// <summary>A default whose body is compiled into a referenced assembly the runtime does not
    /// carry cannot be written, and the build says so instead of emitting a twin that answers
    /// undefined.</summary>
    [Fact]
    public void ADefaultFromAReferencedAssemblyIsSaid()
    {
        var result = Compile("Voiced");

        result.TypeScript.Should().NotMatchRegex(@"\bsay\(");
        result.Warnings.Should().ContainSingle(warning => warning.Code == "EQ1008")
            .Which.Message.Should().Contain("Voiced relies on the default IVoice.Say")
            .And.Contain("Declare Say in Voiced");
    }
}
