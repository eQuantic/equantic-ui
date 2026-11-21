using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using Solution = eQuantic.UI.Compiler.Tests.ReferencedProjects.Solution;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Two types of one name in one module are refused, naming both (EQ1005, #1777). The bundle already
/// refused two of the app's own, whose twins are one file; a type of the app's beside one the runtime
/// carries was the case it could not see. The page imported <c>Text</c> once, from the runtime, and
/// <c>new Shop.Model.Text("y")</c> built the vocabulary's node, a page wrong in the browser and nowhere
/// else.
/// </summary>
public class ATypeNamedLikeTheRuntimesTests
{
    private static List<CompilationResult> Compile(string source)
    {
        const string path = "Probe.cs";
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: path);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => (MetadataReference)TestReferences.Of(file))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Components.UI).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the probe is C# that compiles");
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, path).ToList();
    }

    private const string AppText = "namespace Shop.Model { public sealed record Text(string Body); }";

    [Fact]
    public void APageThatNamesTheAppsTypeAndTheSdksOfOneName_IsRefusedNamingBoth()
    {
        var results = Compile(AppText + """

            namespace Shop.Screens
            {
                using eQuantic.UI.Primitives;

                public sealed class Home : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) =>
                        new Text(new Shop.Model.Text("y").Body, TypeRole.BodyM);
                }
            }
            """);

        var home = results.Single(result => result.ComponentName == "Home");
        home.Success.Should().BeFalse();
        var error = home.Errors.Should().ContainSingle().Which;
        error.Code.Should().Be("EQ1005");
        error.Message.Should().Be("'Text' names two types in the module of 'Home': 'Shop.Model.Text', and the SDK's "
            + "'eQuantic.UI.Primitives.Text'. A module imports a name once, so the code that used one of them would get "
            + "the other in the browser. Rename 'Shop.Model.Text'.");
        error.Line.Should().Be(6, "it is said at the module's own declaration, `public sealed class Home`");
        results.Single(result => result.ComponentName == "Text").Success.Should().BeTrue(
            "the record itself names nothing of the SDK's, and its module is its own");
    }

    [Fact]
    public void TheBuildOfAnApp_StopsAtTheModuleThatNamesBoth()
    {
        // What eqc does with the app's own project, on disk: the error is the build's, at the page's file.
        using var solution = new Solution()
            .App("App", globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Text.cs", "namespace App.Model;\n\npublic sealed record Text(string Body);\n"),
                ("Screens/Home.cs", """
                    using eQuantic.UI.Primitives;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(new App.Model.Text("y").Body, TypeRole.BodyM);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("the page would build the vocabulary's Text where it meant the app's");
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(5,")
            .And.Contain("error EQ1005: 'Text' names two types in the module of 'Home': 'App.Model.Text', and the SDK's "
                + "'eQuantic.UI.Primitives.Text'");
    }

    [Fact]
    public void AComponentOfTheSdkAndARecordOfTheAppsOfItsName_AreRefusedToo()
    {
        var results = Compile("""
            namespace Shop.Model { public sealed record Card(string Title); }

            namespace Shop.Screens
            {
                using eQuantic.UI.Primitives;
                using static eQuantic.UI.Components.UI;

                public sealed class Home : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) =>
                        Card(child: Text(new Shop.Model.Card("t").Title));
                }
            }
            """);

        results.Single(result => result.ComponentName == "Home").Errors
            .Should().ContainSingle(error => error.Code == "EQ1005"
                && error.Message.Contains("'Shop.Model.Card', and the SDK's 'eQuantic.UI.Components.Card'"));
    }

    [Fact]
    public void ATypeOfTheAppsThatUsesTheSdksOfItsOwnName_IsRefused()
    {
        // The module DECLARES `class Text` and imports the runtime's `Text` beside it: two bindings of one
        // name, which no module parses.
        var results = Compile("""
            namespace Shop.Model
            {
                using eQuantic.UI.Primitives;

                public sealed record Text(string Body)
                {
                    public VisualNode Shown() => new eQuantic.UI.Primitives.Text(Body, TypeRole.BodyM);
                }
            }
            """);

        results.Single().Errors.Should().ContainSingle(error => error.Code == "EQ1005"
            && error.Message.StartsWith("'Text' names two types in the module of 'Text': 'Shop.Model.Text', and the SDK's"));
    }

    [Fact]
    public void TheAppsTypeAlone_ImportsItsOwnModule()
    {
        var results = Compile(AppText + """

            namespace Shop
            {
                public static class Helper
                {
                    public static string Of() => new Shop.Model.Text("y").Body;
                }
            }
            """);

        var helper = results.Single(result => result.ComponentName == "Helper");
        helper.Success.Should().BeTrue(string.Join("\n", helper.Errors.Select(error => error.Message)));
        helper.TypeScript.Should().Contain("import { Text } from \"./Text\"",
            "a name the module takes from one place only is no clash, whoever else carries it");
    }

    [Fact]
    public void WhatOnlyServerCodeNames_IsNoNameOfTheModule()
    {
        // The Server Action's body stays on the server, so the app's Text never reaches the module that
        // uses the vocabulary's.
        var results = Compile(AppText + """

            namespace Shop.Screens
            {
                using System.Threading.Tasks;
                using eQuantic.UI.Primitives;

                public sealed class Home : StatelessComponent
                {
                    [ServerAction]
                    public Task<string> Load() => Task.FromResult(new Shop.Model.Text("y").Body);

                    public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
                }
            }
            """);

        var home = results.Single(result => result.ComponentName == "Home");
        home.Errors.Should().NotContain(error => error.Code == "EQ1005");
    }

    [Fact]
    public void TwoTypesOfOneNameInOneFile_AreTheCollision_NotAPartialType()
    {
        var results = Compile("""
            namespace Shop.A { public sealed record Card(string Title); }
            namespace Shop.B { public sealed record Card(int Price); }
            """);

        results.Should().HaveCount(2).And.OnlyContain(result => !result.Success);
        results.SelectMany(result => result.Errors).Should().OnlyContain(error => error.Code == "EQ1005"
            && error.Message.StartsWith("two types are named 'Card' in this file: 'Shop.A.Card' and 'Shop.B.Card'."));
    }

    [Fact]
    public void TheCollisionInOneFile_IsSaidAtTheDeclaration()
    {
        var results = Compile("""
            namespace Shop.A { public sealed record Card(string Title); }
            namespace Shop.B { public sealed record Card(int Price); }
            """);

        results.SelectMany(result => result.Errors).Select(error => (error.Line, error.Column))
            .Should().BeEquivalentTo([(1, 41), (2, 41)], "each error points at the record it refuses, `Card`");
    }

    [Fact]
    public void ANestedTypeAndATopLevelOneOfItsName_AreTwoTwins()
    {
        // `Deck.Card` is written as `Deck$Card`, which meets no top-level `Card`: no collision, and no
        // partial type either.
        var results = Compile("""
            namespace Shop
            {
                public sealed record Card(string Title);

                public static class Deck
                {
                    public sealed record Card(int Price);

                    public static int Price(Card card) => card.Price;
                }
            }
            """);

        results.SelectMany(result => result.Errors).Should().NotContain(error => error.Code == "EQ1005" || error.Code == "EQ2009");
    }

    [Fact]
    public void AnAppsClassNamedLikeTheContextsAnnotation_IsRefused()
    {
        // The runtime exports a component's context under two names: `ComponentContext` as a value and
        // `BuildContext` as an annotation, which a module that takes a context imports.
        var results = Compile("""
            namespace Shop
            {
                using eQuantic.UI.Primitives;

                public sealed class BuildContext { public string Name { get; } = "build"; }

                public static class Labels
                {
                    public static string Of(ComponentContext context) => new BuildContext().Name;
                }
            }
            """);

        results.Single(result => result.ComponentName == "Labels").Errors.Should().ContainSingle(error => error.Code == "EQ1005"
            && error.Message.Contains("'Shop.BuildContext', and the SDK's 'eQuantic.UI.Primitives.ComponentContext'"));
    }

    [Fact]
    public void TheHalvesOfOnePartialType_AreStillThePartialType()
    {
        var results = Compile("""
            namespace Shop
            {
                public static partial class Helper { public static int One() => 1; }
                public static partial class Helper { public static int Two() => 2; }
            }
            """);

        results.SelectMany(result => result.Errors).Should().NotBeEmpty()
            .And.OnlyContain(error => error.Code == "EQ2009");
    }
}
