using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A class is a COMPONENT by what it derives from, wherever the compilation can say: the parser, which writes
/// its twin, and the dependency resolver, which decides how it is imported, both ask the chain of bases (#1164).
/// They asked the NAME of a method instead, even with the compilation in hand, so a builder whose last step is
/// <c>Build(bool announced)</c> was written as a component: its twin extended <c>StatelessComponent</c>, took a
/// component's constructor and type id, and typed <c>announced</c> as a <c>BuildContext</c>, which <c>tsc</c>
/// refused at the first call. The name of a method decides only where a base on the chain does not bind.
/// </summary>
public class ComponentByItsBasesTests
{
    [Fact]
    public void AClassOverNoComponent_IsAPlainClass_WhateverItsMethodsAreCalled()
    {
        var (modules, resolver) = Transpile("""
            using eQuantic.UI.Primitives;

            public sealed class NoteBuilder
            {
                private readonly string _said;
                public NoteBuilder(string said) { _said = said; }
                public VisualNode Build(bool announced) => new Text(announced ? _said : "", TypeRole.BodyM);
            }

            public sealed class Painter
            {
                public string Render() => "painted";
            }

            public sealed class Note : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new NoteBuilder(new Painter().Render()).Build(announced: true);
            }
            """);

        foreach (var plain in new[] { "NoteBuilder", "Painter" })
        {
            modules.Should().ContainKey(plain);
            modules[plain].Should().NotContain("extends", $"{plain} derives from nothing but object");
            modules[plain].Should().NotContain("$typeId", "a component's type id is no plain class's");
            resolver.GetAllPlainClasses().Should().Contain(plain, "the resolver reads the same chain");
        }
        modules["NoteBuilder"].Should().Contain("build(announced: boolean)");
        modules["Note"].Should().Contain("extends StatelessComponent", "a component is one by its base");
        resolver.GetAllPlainClasses().Should().NotContain("Note");
    }

    /// <summary>
    /// Where the compilation cannot bind a base on the chain (a reference it lacks), it cannot say what the class
    /// derives from, and the declaration's own words decide, as they always did: a class that overrides
    /// <c>Build</c> over an unknown base is still taken for a component.
    /// </summary>
    [Fact]
    public void AClassOverABaseTheCompilationCannotBind_IsStillFoundByItsBuild()
    {
        var (modules, resolver) = Transpile("""
            using eQuantic.UI.Primitives;

            public sealed class Legacy : MissingBase
            {
                public override VisualNode Build(ComponentContext context) => new Text("legacy", TypeRole.BodyM);
            }
            """, unbound: true);

        modules.Should().ContainKey("Legacy");
        modules["Legacy"].Should().Contain("$typeId", "the component path writes it");
        resolver.GetAllPlainClasses().Should().NotContain("Legacy");
        resolver.IsModule("Legacy").Should().BeTrue();
    }

    /// <summary>The source transpiled as eqc transpiles an app: one compilation, handed to the parser and to the
    /// resolver, which scans the file's directory. With <paramref name="unbound"/>, the compilation is expected to
    /// miss a base, and only that.</summary>
    private static (Dictionary<string, string> Modules, ComponentDependencyResolver Resolver) Transpile(string source,
        bool unbound = false)
    {
        var dir = Directory.CreateTempSubdirectory("eq-component-by-its-bases-").FullName;
        try
        {
            var path = Path.Combine(dir, "Source.cs");
            File.WriteAllText(path, source);
            var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: path);
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => TestReferences.Of(p))
                .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
                .ToList();
            var compilation = CSharpCompilation.Create("App", [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.Id).Distinct().ToList();
            if (unbound) errors.Should().Contain("CS0246", "the base is one the compilation cannot bind");
            else errors.Should().BeEmpty();

            var resolver = new ComponentDependencyResolver(compilation);
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var modules = compiler.CompileFile(path)
                .Where(result => result.TypeScript.Length > 0)
                .ToDictionary(result => result.ComponentName, result => result.TypeScript);
            return (modules, resolver);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
