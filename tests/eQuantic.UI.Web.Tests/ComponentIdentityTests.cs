using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests
{
    /// <summary>
    /// A component is named at the seam with the browser by what it IS: the CLR full name of its
    /// definition, on the server (<see cref="ComponentIdentity"/>) and on the twin
    /// (<c>static $typeId</c>, written by eqc), and the two must say the same thing.
    /// <para>
    /// It was the SIMPLE name on both sides, so two components called <c>Row</c> from different
    /// namespaces were indistinguishable to the one check that makes a drift between the two trees
    /// safe: a client that built the other <c>Row</c> in that position took the first one's state
    /// (#278). And in the browser the name came from <c>constructor.name</c>, alive only while no
    /// bundler minified identifiers.
    /// </para>
    /// </summary>
    public class ComponentIdentityTests
    {
        [Fact]
        public void Two_components_with_one_name_are_two_identities_and_two_counts()
        {
            ComponentIdentity.Of(typeof(IdentityA.Row)).Should().Be("eQuantic.UI.Web.Tests.IdentityA.Row");
            ComponentIdentity.Of(typeof(IdentityB.Row)).Should().NotBe(ComponentIdentity.Of(typeof(IdentityA.Row)));

            var scope = new ComponentExpansionScope();
            scope.Enter(new IdentityA.Row()).Should().Be("eQuantic.UI.Web.Tests.IdentityA.Row#0");
            scope.Enter(new IdentityB.Row()).Should().Be("eQuantic.UI.Web.Tests.IdentityB.Row#0");
        }

        [Fact]
        public void A_drifted_component_keeps_its_defaults_rather_than_another_types_state()
        {
            // What the server loaded for A.Row, under A.Row's key.
            var loaded = new IdentityA.Row();
            var asBuilt = ComponentExpansionScope.Capture(loaded);
            loaded.Load("A-state");
            var restore = new Dictionary<string, LoadedComponentState>(StringComparer.Ordinal)
            {
                [ComponentIdentity.Key(typeof(IdentityA.Row), 0)] =
                    new(loaded, asBuilt, ComponentExpansionScope.Capture(loaded)),
            };

            // The next round builds B.Row in that position: the key is B's own, so nothing of A's lands.
            var drifted = new IdentityB.Row();
            new ComponentExpansionScope { Restore = restore }.Enter(drifted);
            drifted.Label.Should().Be("default");

            // And A.Row, where it is still A.Row, gets what it loaded.
            var agreeing = new IdentityA.Row();
            new ComponentExpansionScope { Restore = restore }.Enter(agreeing);
            agreeing.Label.Should().Be("A-state");
        }

        [Fact]
        public void An_identity_is_the_clr_full_name_of_the_definition()
        {
            ComponentIdentity.Of(typeof(IdentityA.Shell.Pane)).Should().Be("eQuantic.UI.Web.Tests.IdentityA.Shell+Pane");
            ComponentIdentity.Of(typeof(IdentityA.Grid<int>)).Should().Be("eQuantic.UI.Web.Tests.IdentityA.Grid`1");
        }

        [Fact]
        public void The_compiler_writes_the_identity_the_server_keys_by()
        {
            // THIS FILE's own declarations, transpiled: the syntax the compiler reads and the types the
            // runtime reflects over are the same declarations, so the two identities must agree.
            var source = File.ReadAllText(ThisFile());
            // A list, not a map by name: two of these components ARE both called Row.
            var emitted = new ComponentCompiler().CompileSource(source, ThisFile())
                .Select(r => Identity(r.TypeScript))
                .Where(id => id is not null)
                .ToList();

            foreach (var type in new[] { typeof(IdentityA.Row), typeof(IdentityB.Row), typeof(IdentityA.Shell.Pane), typeof(IdentityA.Grid<>) })
            {
                emitted.Should().Contain(ComponentIdentity.Of(type),
                    $"the twin of {type.Name} is keyed by the name the server gives it");
            }
        }

        [Fact]
        public void Every_shared_component_twin_carries_the_identity_the_server_keys_it_by()
        {
            var assemblies = new[] { typeof(eQuantic.UI.Components.UI).Assembly, typeof(eQuantic.UI.Charts.BarChart).Assembly };
            var components = assemblies.SelectMany(a => a.GetTypes())
                .Where(t => typeof(UiComponent).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic)
                .GroupBy(t => t.Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            var modules = Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", "components"), "*.ts");
            var checkedCount = 0;
            foreach (var module in modules)
            {
                var text = File.ReadAllText(module);
                var name = Regex.Match(text, @"export class (\w+)").Groups[1].Value;
                if (!components.TryGetValue(name, out var types)) continue;

                Identity(text).Should().BeOneOf(types.Select(ComponentIdentity.Of),
                    $"{Path.GetFileName(module)} is the twin of {name}, and the server keys it by its full name");
                checkedCount++;
            }

            checkedCount.Should().BeGreaterThan(50, "the shared library's twins were found and read");
        }

        private static string? Identity(string module) =>
            Regex.Match(module, @"static \$typeId = '([^']+)'") is { Success: true } m ? m.Groups[1].Value : null;

        private static string ThisFile([CallerFilePath] string path = "") => path;

        private static string RepoRoot()
        {
            var here = new DirectoryInfo(AppContext.BaseDirectory);
            while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
                here = here.Parent;
            return here!.FullName;
        }
    }
}

namespace eQuantic.UI.Web.Tests.IdentityA
{
    public sealed class Row : StatelessComponent
    {
        private string _label = "default";
        public string Label => _label;
        public void Load(string label) => _label = label;
        public override VisualNode Build(ComponentContext context) => new Text(_label);
    }

    public sealed class Shell
    {
        public sealed class Pane : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text("pane");
        }
    }

    public sealed class Grid<T> : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("grid");
    }
}

namespace eQuantic.UI.Web.Tests.IdentityB
{
    public sealed class Row : StatelessComponent
    {
        private string _label = "default";
        public string Label => _label;
        public override VisualNode Build(ComponentContext context) => new Text(_label);
    }
}
