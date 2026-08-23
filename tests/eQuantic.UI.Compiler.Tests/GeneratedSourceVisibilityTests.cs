using eQuantic.UI.Compiler.Services;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// SOURCE-GENERATOR output is part of the program csc compiles, so anything an app writes AGAINST
/// it has to be part of what eqc sees too. eqc reads files, not the csc compilation, so a generated
/// factory surface reaches it only through <c>EmitCompilerGeneratedFiles</c> and these lookups.
/// <para>
/// Skipping them is not a missing feature, it is a SILENT one: the call binds to nothing, the
/// fallback emits <c>this.panel(…)</c>, the build succeeds, and the page throws in the browser.
/// One unresolved name also poisons its whole expression, so calls that used to work — the SDK's
/// own factories, in the same tree — degrade with it.
/// </para>
/// </summary>
public class GeneratedSourceVisibilityTests : IDisposable
{
    private readonly string _project = Path.Combine(
        Path.GetTempPath(), "eqc-generated-" + Guid.NewGuid().ToString("N"));

    private string WriteGenerated(string fileName, string content)
    {
        var dir = Path.Combine(_project, "obj", "Debug", "net10.0", "generated", "SomeGen", "SomeGen.Emitter");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true);
    }

    [Fact]
    public void GeneratedSources_AreFound_UnderObj()
    {
        var expected = WriteGenerated("AppUI.g.cs", "public static class AppUI { }");

        var found = ProjectCompilationHelper.GetCompilerGeneratedFiles(_project).ToList();

        Assert.Contains(expected, found);
    }

    [Fact]
    public void TheImplicitUsingsFile_IsNotTakenTwice()
    {
        // It is collected by GetGeneratedGlobalUsingsFiles already; taking it here as well would
        // declare the same global usings in two trees, which Roslyn reports as a duplicate.
        WriteGenerated("Demo.GlobalUsings.g.cs", "global using System;");

        var found = ProjectCompilationHelper.GetCompilerGeneratedFiles(_project).ToList();

        Assert.DoesNotContain(found, f => f.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void AProjectWithNoObjDirectory_AnswersEmpty()
    {
        Directory.CreateDirectory(_project);

        Assert.Empty(ProjectCompilationHelper.GetCompilerGeneratedFiles(_project));
    }

    [Fact]
    public void TheConfigurationBeingBuilt_IsTheOnlyOneRead()
    {
        // A project built Debug AND Release has a generated tree under each. Sweeping obj takes
        // every generated type TWICE, which resolves to neither — and the C# build stays perfectly
        // happy throughout, so the only symptom is a page whose factories all degrade at once.
        var debug = Path.Combine(_project, "obj", "Debug", "net10.0", "generated", "Gen", "Gen.E");
        var release = Path.Combine(_project, "obj", "Release", "net10.0", "generated", "Gen", "Gen.E");
        Directory.CreateDirectory(debug);
        Directory.CreateDirectory(release);
        File.WriteAllText(Path.Combine(debug, "AppUI.g.cs"), "public static class AppUI { }");
        File.WriteAllText(Path.Combine(release, "AppUI.g.cs"), "public static class AppUI { }");

        var scoped = ProjectCompilationHelper.GetCompilerGeneratedFiles(
            _project, Path.Combine(_project, "obj", "Release", "net10.0", "generated")).ToList();

        Assert.Single(scoped);
        Assert.StartsWith(release, scoped[0]);
    }

    [Fact]
    public void WithoutAScope_TheSweepStillFindsThem()
    {
        // Direct CLI use passes no scope; the sweep is the fallback, not the contract.
        WriteGenerated("AppUI.g.cs", "public static class AppUI { }");

        Assert.NotEmpty(ProjectCompilationHelper.GetCompilerGeneratedFiles(_project, generatedDirectory: null));
    }

    [Fact]
    public void TheDependencyResolver_KnowsGeneratedTypes()
    {
        // This map is what decides whether a referenced name is imported as `./AppUI`. Without the
        // generated file in it, the emitted page names a binding nothing imports and the bundle
        // leaves it undefined — the module transpiles, and the browser still says "not defined".
        WriteGenerated("AppUI.g.cs", """
            namespace Demo;

            public static class AppUI
            {
                public static string Panel(string title) => title;
            }
            """);

        var resolver = new ComponentDependencyResolver();
        resolver.ScanSourceDirectories([_project]);

        Assert.Contains("AppUI", resolver.GetAllStaticHelpers());
    }

    private string WriteGeneratedIn(string configuration, string fileName, string content)
    {
        var dir = Path.Combine(_project, "obj", configuration, "net10.0", "generated", "SomeGen", "SomeGen.Emitter");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// A project built in both configurations has obj/Debug/…/generated AND obj/Release/…/generated,
    /// and the unscoped fallback swept both — so csc's ONE generated type reached Roslyn twice and
    /// resolved to neither, which the parameter doc had warned about while the code did it anyway.
    /// Reported by the site: CI builds Release over a tree with a Debug obj, so it hit every run.
    /// </summary>
    [Fact]
    public void OneGeneratedFile_UnderTwoConfigurations_IsCollectedOnce()
    {
        WriteGeneratedIn("Debug", "AppUI.g.cs", "public static class AppUI { }");
        WriteGeneratedIn("Release", "AppUI.g.cs", "public static class AppUI { }");

        var found = ProjectCompilationHelper.GetCompilerGeneratedFiles(_project).ToList();

        Assert.Single(found, file => file.EndsWith("AppUI.g.cs", StringComparison.Ordinal));
    }

    /// <summary>Newest wins, so a Release copy left over from yesterday cannot shadow the Debug
    /// build happening now. Same-name-different-content is the case that made it matter.</summary>
    [Fact]
    public void WhenTwoConfigurationsDisagree_TheNewerFileIsTheOneCollected()
    {
        var stale = WriteGeneratedIn("Release", "AppUI.g.cs", "public static class AppUI { /* yesterday */ }");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-1));
        var fresh = WriteGeneratedIn("Debug", "AppUI.g.cs", "public static class AppUI { /* today */ }");

        var found = ProjectCompilationHelper.GetCompilerGeneratedFiles(_project).ToList();

        Assert.Contains(fresh, found);
        Assert.DoesNotContain(stale, found);
    }

    /// <summary>Deduping is by the path INSIDE the generated root, not by filename: two generators
    /// may each write a Registry.g.cs and both are real.</summary>
    [Fact]
    public void TwoGeneratorsWritingOneFilename_AreBothKept()
    {
        var a = Path.Combine(_project, "obj", "Debug", "net10.0", "generated", "GenA", "GenA.Emitter");
        var b = Path.Combine(_project, "obj", "Debug", "net10.0", "generated", "GenB", "GenB.Emitter");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        File.WriteAllText(Path.Combine(a, "Registry.g.cs"), "class RegistryA { }");
        File.WriteAllText(Path.Combine(b, "Registry.g.cs"), "class RegistryB { }");

        var found = ProjectCompilationHelper.GetCompilerGeneratedFiles(_project).ToList();

        Assert.Equal(2, found.Count(file => file.EndsWith("Registry.g.cs", StringComparison.Ordinal)));
    }
}
