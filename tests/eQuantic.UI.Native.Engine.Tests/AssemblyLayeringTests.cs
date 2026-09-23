using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The layering, held to the tree: which eQuantic assembly each core assembly may reference, exactly —
/// by project, and by package, because a <c>PackageReference</c> to a sibling is the same edge by another door.
///
/// <para>
/// The architecture is a handful of sentences — Primitives depends on nothing; the component
/// library depends on Primitives and nothing else; a realizer lowers the vocabulary and never
/// references the library it realizes; the engine sits under the framework, the framework under the
/// native realizer, the realizer under the host. Every one of those sentences is a csproj edge, and
/// csproj edges are added in passing: the web realizer carried a reference to the component library
/// for ten weeks with nothing in it using a single type from there, left behind by the merge that
/// renamed the library (e19fb9fc). The wiki said one graph, the plan document another, the tree a
/// third — and nothing compared them.
/// </para>
///
/// <para>
/// So the graph is written HERE, once, as the set of edges each assembly is allowed, and the csproj
/// files are read against it. Exact sets, both directions: an edge the file has and this table does
/// not is a layering change that owes a sentence in <c>docs/ARCHITECTURE-AUDIT.md</c>; an edge this
/// table has and the file lost is the table lying. The shells, the tools, the icon packs and the
/// satellites are deliberately not here — they are leaves, and a leaf may reference whatever is
/// below it.
/// </para>
/// </summary>
public class AssemblyLayeringTests
{
    private static readonly string Root = RepositoryRoot();

    /// <summary>Each core assembly and the ONLY project references it may declare.</summary>
    private static readonly Dictionary<string, string[]> Layering = new()
    {
        // The vocabulary. Zero project references and zero package references — the second half is
        // its own assertion below, because a NuGet dependency is the same weight arriving by another
        // door.
        ["eQuantic.UI.Primitives"] = [],

        // The code editing engine — what a CodeSurface's realizer drives. Written against the
        // vocabulary alone: it knows the surface's protocol and nothing about any host.
        ["eQuantic.UI.Code"] = ["eQuantic.UI.Primitives"],

        // Written once, against the vocabulary and nothing else — plus, for the code components,
        // the engine they are views of.
        ["eQuantic.UI.Components"] = ["eQuantic.UI.Primitives", "eQuantic.UI.Code"],
        ["eQuantic.UI.Charts"] = ["eQuantic.UI.Primitives", "eQuantic.UI.Components"],

        // The realizers. Each lowers VisualNode trees; a component reaches them as the tree its
        // Build produced, so none of them needs the library — and none may reference it, or the
        // layering inverts: the thing being realized would depend on its realizer's dependency.
        ["eQuantic.UI.Web"] = ["eQuantic.UI.Primitives"],
        ["eQuantic.UI.Email"] = ["eQuantic.UI.Primitives"],
        ["eQuantic.UI.Native.Components"] =
            ["eQuantic.UI.Primitives", "eQuantic.UI.Native.Engine", "eQuantic.UI.Native.Framework"],

        // The web host sits on the web realizer alone.
        ["eQuantic.UI.Server"] = ["eQuantic.UI.Web"],

        // Photon: engine, its backends, the layout framework, the host.
        ["eQuantic.UI.Native.Engine"] = ["eQuantic.UI.Primitives"],
        ["eQuantic.UI.Native.Engine.Metal"] = ["eQuantic.UI.Primitives", "eQuantic.UI.Native.Engine"],
        ["eQuantic.UI.Native.Engine.Vulkan"] = ["eQuantic.UI.Primitives", "eQuantic.UI.Native.Engine"],
        ["eQuantic.UI.Native.Engine.Reference"] = ["eQuantic.UI.Native.Engine"],
        ["eQuantic.UI.Native.Framework"] = ["eQuantic.UI.Primitives", "eQuantic.UI.Native.Engine"],
        ["eQuantic.UI.Native.Hosting"] = ["eQuantic.UI.Primitives", "eQuantic.UI.Native.Components"],
    };

    public static IEnumerable<object[]> EveryCoreAssembly() => Layering.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(EveryCoreAssembly))]
    public void ACoreAssembly_ReferencesExactlyWhatTheLayeringAllows(string assembly)
    {
        var declared = ProjectReferences(assembly);
        var allowed = Layering[assembly];

        declared.Should().BeEquivalentTo(allowed,
            $"{assembly} may reference exactly [{string.Join(", ", allowed)}]. An edge added here is a "
            + "layering change: declare it in this table and say why in docs/ARCHITECTURE-AUDIT.md.");
    }

    /// <summary>
    /// The three realizers, by name, because this is the sentence the exact sets exist to protect:
    /// a realizer lowers the vocabulary and never depends on the library written against it.
    /// </summary>
    [Theory]
    [InlineData("eQuantic.UI.Web")]
    [InlineData("eQuantic.UI.Email")]
    [InlineData("eQuantic.UI.Native.Components")]
    public void ARealizer_NeverReferencesTheLibraryItRealizes(string realizer)
    {
        ProjectReferences(realizer).Should().NotContain("eQuantic.UI.Components",
            "a component reaches a realizer as the tree its Build produced; the realizer has no reason "
            + "to know the library, and a reference inverts the layering");
    }

    /// <summary>Zero means zero: no project AND no package.</summary>
    [Fact]
    public void Primitives_HasNoDependencyOfAnyKind()
    {
        var csproj = File.ReadAllText(ProjectFile("eQuantic.UI.Primitives"));
        Regex.Matches(csproj, @"<PackageReference\b").Should().BeEmpty(
            "the vocabulary ships to every target, including AOT and the browser bundle; a package "
            + "dependency here is a dependency of everything");
        ProjectReferences("eQuantic.UI.Primitives").Should().BeEmpty();
    }

    private static string ProjectFile(string assembly) =>
        Path.Combine(Root, "src", assembly, assembly + ".csproj");

    /// <summary>
    /// Every eQuantic assembly the project declares an edge to — <c>ProjectReference</c> by file name,
    /// <c>PackageReference</c> by package id. Third-party packages are not layering and are left to
    /// each project, except for Primitives, whose zero is asserted separately.
    /// </summary>
    private static string[] ProjectReferences(string assembly)
    {
        var path = ProjectFile(assembly);
        File.Exists(path).Should().BeTrue($"the layering table names {assembly}, so its project has to exist");
        var csproj = File.ReadAllText(path);
        var projects = Regex.Matches(csproj, @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')));
        var packages = Regex.Matches(csproj, @"<PackageReference\s+Include=""(eQuantic\.[^""]+)""")
            .Select(m => m.Groups[1].Value);
        return projects.Concat(packages)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the pin reads the project files, so it has to find the tree");
        return dir!.FullName;
    }
}
