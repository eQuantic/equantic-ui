using System.Reflection;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every Primitives type an app can NAME has a runtime export of the same name.
/// <para>
/// eqc routes the whole <c>eQuantic.UI.Primitives</c> namespace to <c>@equantic/runtime</c>
/// implicitly — that is what makes the shared vocabulary work without an attribute on every type.
/// The cost is that adding a public class there silently promises an export that may not exist, and
/// the failure lands as far from the cause as it gets: the page dies at hydration on
/// <c>does not provide an export named 'X'</c> while SSR keeps answering 200 with correct markup,
/// so everything looks right until the screen is blank.
/// </para>
/// <para>
/// The existing parity spec compares the runtime's two doors to each other. Nothing compared C# to
/// TypeScript, which is the direction the promise runs in — <c>RouteValues</c> shipped without its
/// twin and took a page down.
/// </para>
/// </summary>
public class PrimitivesRuntimeExportTests
{
    /// <summary>
    /// Types eqc never emits by NAME, so no export is owed.
    /// <list type="bullet">
    /// <item>ENUMS — they cross as string literals (<c>MainAlign.Start</c> → <c>'start'</c>).</item>
    /// <item>INTERFACES — a capability is resolved by name through the service container, and a
    /// type annotation is erased.</item>
    /// <item>ATTRIBUTES — build-time only; nothing survives into the emitted module.</item>
    /// </list>
    /// </summary>
    private static bool NeedsExport(Type type) =>
        type is { IsPublic: true, IsEnum: false, IsInterface: false }
        && !typeof(Attribute).IsAssignableFrom(type)
        && !type.IsGenericTypeDefinition;

    /// <summary>
    /// The types that legitimately have no twin, each for a stated reason — kept as names so a
    /// fourth cannot slip in unnoticed.
    /// </summary>
    private static readonly HashSet<string> NoTwinRequired =
    [
        // SERVER-SIDE ONLY: reached from [ServerOnly] code, which is never transpiled.
        "ComponentBoundary",
    ];

    private static string FixturePath()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;

        here.Should().NotBeNull("the test has to find the repository root to write the fixture");
        return Path.Combine(here!.FullName, "src", "eQuantic.UI.Runtime", "src", "shared",
            "primitives-types.fixture.json");
    }

    /// <summary>
    /// Writes the list the TS side checks against real exports.
    /// <para>
    /// The comparison cannot happen here: <c>runtime-exports.ts</c> ends in
    /// <c>export * from './components'</c>, so reading the file text answers about the file rather
    /// than about the module — a wildcard is invisible to any regex and would make this pass by
    /// looking in the wrong place. What a module actually exports is a question only the module can
    /// answer, so the assertion lives in vitest and this writes it the question. Same cross-pin the
    /// atomizer fixture uses.
    /// </para>
    /// </summary>
    [Fact]
    public void ThePrimitivesTypeList_IsPinnedForTheRuntimeToCheck()
    {
        var names = typeof(VisualNode).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("eQuantic.UI.Primitives", StringComparison.Ordinal) == true)
            .Where(NeedsExport)
            .Select(t => t.Name)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(names,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        var path = FixturePath();
        var current = File.Exists(path) ? File.ReadAllText(path) : null;
        if (current?.TrimEnd() == json.TrimEnd()) return;

        File.WriteAllText(path, json + Environment.NewLine);
        current.Should().NotBeNull(
            "the fixture did not exist and has now been written — commit it and re-run");
        // Regenerated rather than failed: the list is DERIVED, and a stale copy is the only way it
        // can be wrong. What must not drift is the runtime's answer to it, and vitest asks that.
    }
}
