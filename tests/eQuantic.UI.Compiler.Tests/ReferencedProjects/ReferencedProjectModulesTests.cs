using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A type of a project the app references that browser code reaches is a module of the bundle, and
/// the module that names it imports it (#624). Before, such a type reached eqc only as the assembly
/// MSBuild resolved, where no type has a source, so eqc wrote no module for it and imported nothing:
/// <c>new Cursor(0)</c> and <c>Palette.Names</c> were written into the page's module as names nothing
/// defined, the build said nothing, and the browser threw a <c>ReferenceError</c>.
/// <para>
/// One build of the solution serves every case here: each asks a different thing of what it wrote.
/// </para>
/// </summary>
public sealed class ReferencedProjectModulesTests : IClassFixture<ReferencedProjectModulesTests.BuiltOnce>
{
    private readonly Built _built;

    public ReferencedProjectModulesTests(BuiltOnce once) => _built = once.Built;

    /// <summary>The solution built once for the class, and removed after its last test.</summary>
    public sealed class BuiltOnce : IDisposable
    {
        private readonly Solution _solution = SharedLibrary.Solution();

        public BuiltOnce() => Built = _solution.Build();

        internal Built Built { get; }

        public void Dispose() => _solution.Dispose();
    }

    [Fact]
    public void TheBuildSucceeds_AndSaysNothing()
    {
        _built.Failed.Should().BeFalse(_built.Errors);
        _built.Errors.Should().BeEmpty("a solution whose browser code crosses raises no diagnostic");
    }

    [Fact]
    public void AStaticClassOfAReferencedProject_IsAModuleThePageImports()
    {
        _built.Module("Palette").Should().Contain("export class Palette",
            "the page reads Palette.Names, a static member of a class the referenced project declares");
        _built.SiblingImportsOf("Home").Should().Contain("Palette");
    }

    [Fact]
    public void ARecordOfAReferencedProject_IsAModuleThePageImports()
    {
        _built.Module("Cursor").Should().Contain("export class Cursor",
            "the page builds new Cursor(2), a record the referenced project declares");
        _built.Module("Cursor").Should().Contain("next()", "the record's own method crosses with it");
        _built.SiblingImportsOf("Home").Should().Contain("Cursor");
    }

    [Fact]
    public void AComponentOfAReferencedProject_IsAModuleThePageImports()
    {
        _built.Module("Wordmark").Should().Contain("export class Wordmark",
            "the page places new Wordmark(), a component the referenced project declares");
        _built.SiblingImportsOf("Home").Should().Contain("Wordmark");
    }

    [Fact]
    public void ATypeOfAReferencedProject_ImportsTheTypesOfItsOwnProject()
    {
        _built.SiblingImportsOf("Wordmark").Should().Contain("Palette",
            "the component reads Palette.Cobalt, declared beside it");
    }

    [Fact]
    public void ATypeReachedThroughASecondProject_IsAModuleToo()
    {
        _built.SiblingImportsOf("Home").Should().Contain("Receipt");
        _built.SiblingImportsOf("Receipt").Should().Contain("Money",
            "the receipt's total is a record of Domain, which Common references and the app does not");
        _built.Module("Money").Should().Contain("export class Money");
    }

    [Fact]
    public void ATypeNoModuleNames_IsNeverCompiled()
    {
        // ReceiptArchive reads a file. The page names it only inside a Server Action, whose body
        // never crosses, so no written module imports it: no module, and no diagnostic (asserted
        // above), though compiling it would raise one.
        _built.Module("ReceiptArchive").Should().BeNull();
        _built.Modules.Should().BeEquivalentTo(["Cursor", "Home", "Money", "Palette", "Receipt", "Wordmark"],
            "the build writes the app's own type and what it reaches, in whichever project, and nothing else");
    }

    [Fact]
    public void TheBuildNamesTheProjectsItCompilesFromSource()
    {
        _built.Output.Should().Contain("Compiling 2 referenced project(s) from source: Domain, Common",
            "each project is compiled after the ones its assembly references");
    }
}
