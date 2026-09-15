using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>[RuntimeProvided]</c> static helpers are excluded from generated local modules, including
/// when eqc has no project semantic model and must fall back to the dependency scan.
/// </summary>
public class RuntimeProvidedClassTests
{
    [Fact]
    public void RuntimeProvidedStaticHelper_FallbackImportsRuntime_NotLocalModule()
    {
        var dir = CreateTempDirectory();
        try
        {
            var helperPath = Path.Combine(dir, "ButtonStyles.cs");
            File.WriteAllText(helperPath, """
                [RuntimeProvided]
                public static class ButtonStyles
                {
                    public const int MinWidth = 64;
                }
                """);
            var probePath = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(probePath, """
                public class Probe
                {
                    public int Build() => ButtonStyles.MinWidth;
                }
                """);
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            resolver.GetRuntimeProvidedTypes().Should().Contain("ButtonStyles");
            resolver.GetAllStaticHelpers().Should().NotContain("ButtonStyles");

            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            var probe = compiler.CompileFile(probePath).Single(result => result.ComponentName == "Probe");

            probe.Success.Should().BeTrue(string.Join("; ", probe.Errors.Select(error => error.Message)));
            ImportLineFor(probe.TypeScript, "ButtonStyles")
                .Should().Contain("from \"@equantic/runtime\"");
            probe.TypeScript.Should().NotContain("from \"./ButtonStyles\"");
            probe.TypeScript.Should().NotContain("'minWidth'");

            File.WriteAllText(helperPath, """
                public static class ButtonStyles
                {
                    public const int MinWidth = 64;
                }
                """);

            var withoutAttribute = new ComponentDependencyResolver();
            withoutAttribute.ScanSourceDirectories([dir]);
            withoutAttribute.GetRuntimeProvidedTypes().Should().NotContain("ButtonStyles");
            withoutAttribute.GetAllStaticHelpers().Should().Contain("ButtonStyles");
            var localCompiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            localCompiler.SetDependencyResolver(withoutAttribute);
            var localProbe = localCompiler.CompileFile(probePath)
                .Single(result => result.ComponentName == "Probe");

            localProbe.Success.Should().BeTrue(string.Join("; ", localProbe.Errors.Select(error => error.Message)));
            ImportLineFor(localProbe.TypeScript, "ButtonStyles")
                .Should().Contain("from \"./ButtonStyles\"");
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
            File.WriteAllText(Path.Combine(dir, "ButtonStyles.cs"), """
                [RuntimeProvided]
                public static class ButtonStyles
                {
                    public static int MinWidth() => 64;
                }
                """);
            var probePath = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(probePath, """
                public class Probe
                {
                    public int Build() => ButtonStyles.MinWidth();
                }
                """);

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            var probe = compiler.CompileFile(probePath).Single(result => result.ComponentName == "Probe");

            probe.Success.Should().BeTrue(string.Join("; ", probe.Errors.Select(error => error.Message)));
            ImportLineFor(probe.TypeScript, "ButtonStyles")
                .Should().Contain("from \"@equantic/runtime\"");
            probe.TypeScript.Should().NotContain("this.buttonStyles");
            probe.TypeScript.Should().NotContain("from \"./ButtonStyles\"");
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
