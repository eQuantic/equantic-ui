using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>[RuntimeProvided]</c> static helpers are excluded from generated local modules, including
/// when eqc has no project semantic model and must fall back to the dependency scan. The probe is a
/// component by its base: a class over nothing is a plain class whatever its methods are called (#1164),
/// and the fallback pinned here is the component path's.
/// </summary>
public class RuntimeProvidedClassTests
{
    /// <summary>The plain-class path reads the same fallback (#1170): its module wrote the call with no
    /// import, a ReferenceError in the browser, where the same call in a component imports the helper.</summary>
    [Fact]
    public void RuntimeProvidedStaticHelper_FallbackImportsRuntime_FromAPlainClass()
    {
        var dir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir, "PretendHelper.cs"), """
                [RuntimeProvided]
                public static class PretendHelper
                {
                    public static int MinWidth() => 64;
                }
                """);
            var probePath = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(probePath, """
                public class Probe
                {
                    public int Measure() => PretendHelper.MinWidth();
                }
                """);
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);

            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            var probe = compiler.CompileFile(probePath).Single(result => result.ComponentName == "Probe");

            probe.Success.Should().BeTrue(string.Join("; ", probe.Errors.Select(error => error.Message)));
            ImportLineFor(probe.TypeScript, "PretendHelper").Should().Contain("from \"@equantic/runtime\"");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The parser and the scan read one rule where a base does not bind (#1170): this class was
    /// written as a plain class and filed by the scan as a component.</summary>
    [Theory]
    [InlineData("class Legacy : MissingBase { private int Build(int c) => c; }", false)]
    [InlineData("class Legacy : MissingBase { public static int Render() => 1; }", false)]
    [InlineData("class Legacy : MissingBase { public object CreateState() => null; }", false)]
    [InlineData("class Legacy : MissingBase { public int Build(int c) => c; }", true)]
    [InlineData("class Legacy : StatefulComponent { public object CreateState() => null; }", true)]
    [InlineData("class Legacy : StatefulComponent<int> { }", true)]
    [InlineData("class Legacy : eQuantic.UI.Components.StatelessComponent<int> { }", true)]
    public void TheScanFilesAClass_AsTheParserWritesIt(string source, bool component)
    {
        var dir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Legacy.cs"), source);
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);

            // A module either way, so the scan read the file: what it is filed as is the question.
            resolver.IsModule("Legacy").Should().BeTrue();
            resolver.GetAllPlainClasses().Contains("Legacy").Should().Be(!component);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RuntimeProvidedStaticHelper_FallbackImportsRuntime_NotLocalModule()
    {
        var dir = CreateTempDirectory();
        try
        {
            var helperPath = Path.Combine(dir, "PretendHelper.cs");
            File.WriteAllText(helperPath, """
                [RuntimeProvided]
                public static class PretendHelper
                {
                    public const int MinWidth = 64;
                }
                """);
            var probePath = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(probePath, """
                public class Probe : StatelessComponent
                {
                    public int Build() => PretendHelper.MinWidth;
                }
                """);
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            resolver.GetRuntimeProvidedTypes().Should().Contain("PretendHelper");
            resolver.GetAllStaticHelpers().Should().NotContain("PretendHelper");

            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            var probe = compiler.CompileFile(probePath).Single(result => result.ComponentName == "Probe");

            probe.Success.Should().BeTrue(string.Join("; ", probe.Errors.Select(error => error.Message)));
            ImportLineFor(probe.TypeScript, "PretendHelper")
                .Should().Contain("from \"@equantic/runtime\"");
            probe.TypeScript.Should().NotContain("from \"./PretendHelper\"");
            probe.TypeScript.Should().NotContain("'minWidth'");

            File.WriteAllText(helperPath, """
                public static class PretendHelper
                {
                    public const int MinWidth = 64;
                }
                """);

            var withoutAttribute = new ComponentDependencyResolver();
            withoutAttribute.ScanSourceDirectories([dir]);
            withoutAttribute.GetRuntimeProvidedTypes().Should().NotContain("PretendHelper");
            withoutAttribute.GetAllStaticHelpers().Should().Contain("PretendHelper");
            var localCompiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            localCompiler.SetDependencyResolver(withoutAttribute);
            var localProbe = localCompiler.CompileFile(probePath)
                .Single(result => result.ComponentName == "Probe");

            localProbe.Success.Should().BeTrue(string.Join("; ", localProbe.Errors.Select(error => error.Message)));
            ImportLineFor(localProbe.TypeScript, "PretendHelper")
                .Should().Contain("from \"./PretendHelper\"");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RuntimeProvidedStaticMethod_FallbackPreservesTypeReceiver()
    {
        var dir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir, "PretendHelper.cs"), """
                [RuntimeProvided]
                public static class PretendHelper
                {
                    public static int MinWidth() => 64;
                }
                """);
            var probePath = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(probePath, """
                public class Probe : StatelessComponent
                {
                    public int Build() => PretendHelper.MinWidth();
                }
                """);

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            var probe = compiler.CompileFile(probePath).Single(result => result.ComponentName == "Probe");

            probe.Success.Should().BeTrue(string.Join("; ", probe.Errors.Select(error => error.Message)));
            ImportLineFor(probe.TypeScript, "PretendHelper")
                .Should().Contain("from \"@equantic/runtime\"");
            probe.TypeScript.Should().NotContain("this.pretendHelper");
            probe.TypeScript.Should().NotContain("from \"./PretendHelper\"");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string ImportLineFor(string typeScript, string name) =>
        typeScript.Split('\n').Single(line => line.StartsWith("import") && line.Contains(name));

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-runtime-provided-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
