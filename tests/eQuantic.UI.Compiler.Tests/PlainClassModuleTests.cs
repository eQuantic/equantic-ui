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
/// parser through a real compilation, the resolver through its scan of the directory. They must answer
/// alike for each, and each kind answers as the rule states.
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
        // An exception or an attribute is one by its CHAIN of bases, which the base a class names does
        // not say: three levels over `Exception`, across files, and two over `Attribute` with names
        // that say nothing. A class named like an exception that derives from none is a class.
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
        ["Hollow.cs"] = "public partial class Hollow { }",
    };

    private static readonly string[] Modules =
        ["Mute", "ChainBase", "Echo", "Message", "Ping", "Marker", "Filled", "Helpers", "Outer", "Card", "Split",
         "FakeException"];

    private static readonly string[] NotModules =
        ["FooAttribute", "TaggedAttribute", "NotFoundException", "Oops", "Stays", "Provided", "Inner",
         "ServerBase", "OverServer", "OverOverServer", "Hollow", "Failure", "Retry", "LastRetry", "Mark", "Underline"];

    [Fact]
    public void TheParserAndTheResolver_AnswerAlikeForEveryKindOfClass()
    {
        var dir = Directory.CreateTempSubdirectory("eq-plain-class-module-").FullName;
        try
        {
            var trees = Files.Select(file =>
            {
                var path = Path.Combine(dir, file.Key);
                File.WriteAllText(path, file.Value);
                return CSharpSyntaxTree.ParseText(file.Value, ParseDefaults.Options, path: path);
            }).ToList();
            var compilation = CSharpCompilation.Create("App", trees,
                ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                    .Split(Path.PathSeparator)
                    .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Select(p => TestReferences.Of(p))
                    .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var written = Files.Keys
                .SelectMany(file => compiler.CompileFile(Path.Combine(dir, file)))
                .Where(result => result.TypeScript.Length > 0)
                .Select(result => result.ComponentName)
                .ToHashSet();

            var declared = trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Select(declaration => declaration.Identifier.Text)
                .Distinct()
                .ToList();
            declared.Should().BeEquivalentTo(Modules.Concat(NotModules), "every class of the files is named below");

            var disagreements = declared
                .Where(name => written.Contains(name) != resolver.IsModule(name))
                .Select(name => $"{name}: the parser {(written.Contains(name) ? "wrote" : "did not write")} a module, "
                    + $"and the resolver answered {resolver.IsModule(name)}")
                .ToList();
            disagreements.Should().BeEmpty("the parser and the resolver read one rule");

            written.Should().Contain(Modules);
            written.Should().NotContain(NotModules);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
