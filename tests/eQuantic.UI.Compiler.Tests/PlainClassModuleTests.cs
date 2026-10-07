using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Which classes get a module of their own is ONE rule, read by the parser, which writes the module,
/// and by the dependency resolver, which decides who imports it (#423). Two copies disagreed: the parser
/// skipped a class that declared no member while the resolver had every class with a base list
/// imported, so `new Mute()` named a module no build wrote and the bundle could not resolve it.
/// <para>
/// Every kind of class an app declares is here, across files, the way an app's build sees them: the
/// parser through a real compilation, the resolver through its scan of the directory and the same
/// compilation, which eqc hands to both. They must answer alike for each, and each kind answers as the
/// rule states. A host with no compilation at all walks the chain by name, the parser through the
/// resolver's scan, and the two must answer alike there too.
/// </para>
/// </summary>
public class PlainClassModuleTests
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["Kinds.cs"] = """
            using System;
            using eQuantic.UI.Primitives;

            public interface IGreeting { string Greet() => "hello"; }
            public class Mute : IGreeting { }
            public abstract class ChainBase { public string Say() => "chain"; }
            public class Echo : ChainBase { }
            public abstract class Message { }
            public class Ping : Message { }
            public class Marker { }
            public class Filled { public int Value; }

            public class FooAttribute : Attribute { }
            public class TaggedAttribute : FooAttribute { public string Tag { get; set; } = ""; }
            public class NotFoundException : Exception { }
            public class Oops : Exception { public int Code { get; set; } }

            [ServerOnly] public class Stays { public int Value; }
            [RuntimeProvided] public class Provided { }

            public static class Helpers { public static int One() => 1; }
            public class Outer { public class Inner { public int Value; } public int Value; }

            public sealed class Card : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text("card", TypeRole.BodyM);
            }
            """,
        // A base that stays on the server, declared in a file of its own: the class over it is in
        // another, where only the model (the parser) or the scan's names (the resolver) can see it.
        ["Server.cs"] = """
            using eQuantic.UI.Primitives;

            [ServerOnly] public class ServerBase { public int Secret; }
            public class OverServer : ServerBase { public int Shown; }
            public class OverOverServer : OverServer { }
            """,
        // A partial type is one type: the half that declares nothing is not a module of its own.
        // An attribute is one by its CHAIN of bases, which the base a class names does not say: two
        // over `Attribute` with names that say nothing. An exception class of the app's is a class
        // (#611), three levels over `Exception` across files, and so is one named like an exception
        // that derives from none.
        ["Failure.cs"] = "using System; public class Failure : Exception { }",
        ["Retry.cs"] = """
            public class Retry : Failure { }
            public class LastRetry : Retry { public int Attempts; }
            public class Mark : System.Attribute { }
            public class Underline : Mark { }
            public class FakeException { public int Code; }
            """,
        ["SplitA.cs"] = "public partial class Split { public int Value; }",
        ["SplitB.cs"] = "public interface ISplit { } public partial class Split : ISplit { }",
        // ...but ALONE it is the whole type: `partial` asks for no second declaration, and a lone
        // `partial class Hollow { }` is constructed in C# (Copilot's review of #608).
        ["Hollow.cs"] = "public partial class Hollow { }",
        // Modules share ONE flat namespace, so a name is a module when any declaration of it is one: a
        // nested class the rule refuses shares its name with a component in another file, which is a
        // module all the same. The refused name vetoed it, and the import of `Header` went missing.
        ["Header.cs"] = """
            using eQuantic.UI.Primitives;

            public sealed class Header : StatelessComponent
            {
                public string Title { get; set; } = "top";
                public override VisualNode Build(ComponentContext context) => new Text(Title, TypeRole.BodyM);
            }
            """,
        ["Api.cs"] = "public class Api { public class Header { public string Name = \"nested\"; } public int Version = 1; }",
        // A nested STATIC class is its owner's scope whatever its methods are called: a helper named
        // `Build` made the scan take it for a component, a module nobody writes.
        ["Shell.cs"] = "public class Shell { public static class Copy { public static string Build(string s) => s; } public string Text => Copy.Build(\"x\"); }",
        // Each declaration is judged on its own, by its symbol: a server-only class shares its simple
        // name with a class in another namespace, which the class over it extends. The scan's set of
        // server-only NAMES kept that class out while the parser wrote it.
        ["ServerSettings.cs"] = """
            namespace App.Server
            {
                [eQuantic.UI.Primitives.ServerOnly] public class Settings { public string ConnectionString = ""; }
            }
            """,
        ["UiSettings.cs"] = """
            namespace App.Ui
            {
                public class Settings { public int Theme = 1; }
                public class UserSettings : Settings { public int Font = 14; }
            }
            """,
        // An interface in a base list is no base class, whatever its name says: the resolver judged
        // `IProductAttribute` as an attribute by its suffix, and kept a module the parser wrote out
        // of every import.
        ["Color.cs"] = """
            public interface IProductAttribute { }
            public class ColorAttribute : IProductAttribute { public string Name = "red"; }
            """,
    };

    private static readonly string[] Modules =
        ["Mute", "ChainBase", "Echo", "Message", "Ping", "Marker", "Filled", "Helpers", "Outer", "Card", "Split",
         "FakeException", "Header", "Api", "Settings", "UserSettings", "ColorAttribute", "Shell", "Hollow",
         "NotFoundException", "Oops", "Failure", "Retry", "LastRetry"];

    private static readonly string[] NotModules =
        ["FooAttribute", "TaggedAttribute", "Stays", "Provided", "Inner",
         "ServerBase", "OverServer", "OverOverServer", "Mark", "Underline", "Copy"];

    [Fact]
    public void TheParserAndTheResolver_AnswerAlikeForEveryKindOfClass() =>
        AssertTheRule(Files, library: null, projectCompilation: true, Modules, NotModules);

    /// <summary>
    /// A base from a LIBRARY the app references is judged as what it is there, by its symbol, and never
    /// by its name: a plain class named like an attribute is a class, an attribute named like nothing is
    /// an attribute, and a server-only class says so in its metadata. The resolver judged them by their
    /// names, so it refused the first while the parser wrote it, and imported the others, which nothing
    /// wrote. A class over a library's exception is a class, as one over the app's own is (#611).
    /// </summary>
    [Fact]
    public void ABaseFromALibrary_IsJudgedAsWhatItIsThere() =>
        AssertTheRule(
            new Dictionary<string, string>
            {
                ["Color.cs"] = "public class ColorAttribute : ProductAttribute { public string Hex = \"#fff\"; }",
                ["Order.cs"] = "public class OrderFailed : DomainError { public int OrderId; }",
                ["Over.cs"] = "public class OverServer : ServerBase { public int Shown; }",
                ["Underline.cs"] = "public class Underline : Emphasis { public int Weight; }",
            },
            library: """
                public class ProductAttribute { public string Name = "p"; }
                public class DomainError : System.Exception { }
                public class Emphasis : System.Attribute { }
                [eQuantic.UI.Primitives.ServerOnly] public class ServerBase { public int Secret; }
                """,
            projectCompilation: true,
            modules: ["ColorAttribute", "OrderFailed"],
            notModules: ["OverServer", "Underline"]);

    /// <summary>
    /// A host with no compilation at all walks the chain by NAME, and the parser walks the resolver's
    /// scan, which reaches the other file: it walked its own file only, stopped at a base another file
    /// declares, and wrote a module the resolver refused. An attribute two levels down, across files, is
    /// none, and an exception class of the app's is a class there too (#611).
    /// </summary>
    [Fact]
    public void WithoutACompilation_TheParserWalksTheResolversScan() =>
        AssertTheRule(
            new Dictionary<string, string>
            {
                ["Failure.cs"] = "public class Failure : System.Exception { }",
                ["Retry.cs"] = "public class Retry : Failure { public int Attempts; }",
                ["Mark.cs"] = "public class Mark : System.Attribute { }",
                ["Underline.cs"] = "public class Underline : Mark { public int Weight; }",
                ["Color.cs"] = "public interface IProductAttribute { } public class ColorAttribute : IProductAttribute { public string Name = \"red\"; }",
            },
            library: null,
            projectCompilation: false,
            modules: ["ColorAttribute", "Failure", "Retry"],
            notModules: ["Mark", "Underline"]);

    private static void AssertTheRule(IReadOnlyDictionary<string, string> files, string? library, bool projectCompilation,
        string[] modules, string[] notModules)
    {
        var dir = Directory.CreateTempSubdirectory("eq-plain-class-module-").FullName;
        try
        {
            var trees = files.Select(file =>
            {
                var path = Path.Combine(dir, file.Key);
                File.WriteAllText(path, file.Value);
                return CSharpSyntaxTree.ParseText(file.Value, ParseDefaults.Options, path: path);
            }).ToList();
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => TestReferences.Of(p))
                .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
                .ToList();
            // The library is an assembly, as a package's is: metadata, never a directory the scan reads.
            if (library is not null) references.Add(Assembly("Lib", library, references));
            var compilation = CSharpCompilation.Create("App", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

            // As eqc does it: the compilation the compiler's model is built from, to both.
            var resolver = new ComponentDependencyResolver(projectCompilation ? compilation : null);
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            if (projectCompilation) compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var written = files.Keys
                .SelectMany(file => compiler.CompileFile(Path.Combine(dir, file)))
                .Where(result => result.TypeScript.Length > 0)
                .Select(result => result.ComponentName)
                .ToHashSet();

            var declared = trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Select(declaration => declaration.Identifier.Text)
                .Distinct()
                .ToList();
            declared.Should().BeEquivalentTo(modules.Concat(notModules), "every class of the files is named below");

            var disagreements = declared
                .Where(name => written.Contains(name) != resolver.IsModule(name))
                .Select(name => $"{name}: the parser {(written.Contains(name) ? "wrote" : "did not write")} a module, "
                    + $"and the resolver answered {resolver.IsModule(name)}")
                .ToList();
            disagreements.Should().BeEmpty("the parser and the resolver read one rule");

            written.Should().Contain(modules);
            written.Should().NotContain(notModules);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The source compiled to an assembly's image, referenced as a built package is.</summary>
    private static MetadataReference Assembly(string name, string source, IEnumerable<MetadataReference> references)
    {
        var library = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, ParseDefaults.Options)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var emitted = library.Emit(image);
        emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return MetadataReference.CreateFromImage(image.ToArray());
    }
}
