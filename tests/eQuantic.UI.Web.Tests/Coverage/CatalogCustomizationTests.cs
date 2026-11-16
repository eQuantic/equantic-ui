using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests.Coverage;

/// <summary>
/// Every ready-made component takes the developer's own content, builders and style (CLAUDE.md, "Every
/// component is composed, and takes the developer's content, builders and style"). Measured on main on
/// 2026-11-15, before the rule was written: 28 of the catalog's 55 components took what they show only as
/// a <c>string</c>, and the only one that took a style of its own was <c>Markdown</c>, the precedent the
/// rule generalises. Flutter takes a widget in each of those places and a <c>ButtonStyle</c> beside it.
/// <para>
/// So this test reads every component of the catalog, and every item type a component takes (an option, a
/// column, a destination), and names two things: a member that carries text (a <c>string</c>, a list of
/// them, or a delegate that answers one), and a component without its own style (a constructor parameter
/// <c>style</c> and a property <c>Style</c>, of one of the theme's slot records, so the factory reaches it).
/// Each is an entry of a baseline that may ONLY SHRINK, written by hand with its reason: a slice that
/// removes it (<c>#N</c>), or <c>kept:</c> and why the text is text by nature (a field's value, an
/// accessible name, a source to parse).
/// </para>
/// <para>
/// The baseline is one file per FAMILY under <c>catalog-customization/</c>, each owning its components by
/// name, because the family slices (#1547 to #1551) run in parallel: one list that every slice shrinks would
/// conflict on every pair of them (#1546). A new component is owned by a family before it can pass, so the
/// catalog cannot grow a component nobody placed, and the regenerator removes entries and never writes one.
/// </para>
/// </summary>
public class CatalogCustomizationTests
{
    private const string UpdateVariable = "EQ_UPDATE_CATALOG_CUSTOMIZATION";

    private static readonly Assembly[] Catalog = Discover();

    /// <summary>
    /// The libraries beside this test that ship a component: derived from the build output, never listed,
    /// so a component library added tomorrow is read without anyone remembering to add it.
    /// </summary>
    private static Assembly[] Discover()
    {
        var vocabulary = typeof(VisualNode).Assembly.GetName().Name;
        var found = new List<Assembly>();
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "eQuantic.*.dll"))
        {
            if (Path.GetFileNameWithoutExtension(path).EndsWith(".Tests", StringComparison.Ordinal)) continue;
            Assembly assembly;
            try { assembly = Assembly.LoadFrom(path); }
            catch (BadImageFormatException) { continue; }
            catch (FileLoadException) { continue; }
            if (assembly.GetReferencedAssemblies().Any(reference => reference.Name == vocabulary)
                && Census.ComponentsOf(assembly).Any())
                found.Add(assembly);
        }

        return [.. found];
    }

    [Fact]
    public void TheCatalog_TakesNoNewTextContent_AndEveryComponentItsOwnStyle()
    {
        var census = new Census(Catalog);
        foreach (var component in Catalog.SelectMany(Census.ComponentsOf)) census.Measure(component);
        census.Collisions.Should().BeEmpty("an entry names its type by its short name, so two measured types "
            + "of one name would share entries");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
            foreach (var family in Family.ReadAll()) family.Shrink(census);
        var families = Family.ReadAll();

        var owners = families.SelectMany(family => family.Owns.Select(name => (name, family)))
            .GroupBy(owned => owned.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(owned => owned.family).ToList(),
                StringComparer.Ordinal);

        var unowned = census.Owned.Where(name => !owners.ContainsKey(name)).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.True(unowned.Count == 0,
            "A component of the catalog, or an item type a component takes text through, belongs to no family. "
            + "Add it to the `# owns:` line of its family's file under catalog-customization/ BY HAND (the "
            + "family whose theme slot styles it), or start a family's file:\n  " + string.Join("\n  ", unowned));

        var twice = owners.Where(owner => owner.Value.Count > 1).Select(owner =>
            $"{owner.Key}: {string.Join(", ", owner.Value.Select(family => family.Name))}").ToList();
        Assert.True(twice.Count == 0, "A type is owned by more than one family:\n  " + string.Join("\n  ", twice));

        var listed = families.SelectMany(family => family.Entries.Values.Select(entry => (entry, family))).ToList();
        var unlisted = census.Entries.Where(entry => !listed.Any(line => line.entry.Id == entry.Key))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}  <reason>   in {(owners.TryGetValue(entry.Value, out var owner) ? owner[0].Name : "its family's file")}")
            .ToList();
        Assert.True(unlisted.Count == 0,
            "The catalog takes text where it should take the developer's content, or a component takes no style "
            + "of its own. Take a node (a `string` converts to one), a builder for a repeated part, and "
            + "`style:` of the family's slot record (CLAUDE.md, the product principle). If the text is text by "
            + "nature, add the entry BY HAND with `kept:` and why, or with the issue that removes it:\n  "
            + string.Join("\n  ", unlisted));

        var misplaced = listed.Where(line => census.Entries.TryGetValue(line.entry.Id, out var type)
                && !line.family.Owns.Contains(type))
            .Select(line => $"{line.family.Name}:{line.entry.Line}  {line.entry.Id}").ToList();
        Assert.True(misplaced.Count == 0,
            "An entry sits in a family that does not own its type; each family's file holds only its own, so "
            + "the family slices never edit one file:\n  " + string.Join("\n  ", misplaced));

        var stale = listed.Where(line => !census.Entries.ContainsKey(line.entry.Id))
            .Select(line => $"{line.family.Name}:{line.entry.Line}  {line.entry.Id}")
            .Concat(families.SelectMany(family => family.Owns
                .Where(name => !census.Owned.Contains(name))
                .Select(name => $"{family.Name}  owns {name}")))
            .ToList();
        Assert.True(stale.Count == 0,
            $"{stale.Count} line(s) name a member that takes a node or a style now, or a type that is gone: "
            + $"regenerate ({UpdateVariable}=1) so the list shrinks on record. Regenerating is for a REMOVAL: it "
            + "keeps every reason already written and never adds an entry:\n  " + string.Join("\n  ", stale));

        var undecided = listed.Where(line => !Family.Decided.IsMatch(line.entry.Reason))
            .Select(line => $"{line.family.Name}:{line.entry.Line}  {line.entry.Id}  {line.entry.Reason}").ToList();
        Assert.True(undecided.Count == 0,
            "An entry's reason names the issue that removes it (#N), or starts with `kept:` and says why the "
            + "text is text by nature or the component has no look of its own:\n  " + string.Join("\n  ", undecided));
    }

    /// <summary>The scan reaches every library that ships a component, or the test above is asking about an
    /// empty set and passing.</summary>
    [Fact]
    public void TheScan_ReachesEveryLibraryThatShipsAComponent()
    {
        Catalog.Select(assembly => assembly.GetName().Name).Should().Contain(
            ["eQuantic.UI.Components", "eQuantic.UI.Charts", "eQuantic.UI.Spreadsheets"]);
        Catalog.SelectMany(Census.ComponentsOf).Should().HaveCountGreaterThan(55);
        Census.SlotRecords.Should().Contain([typeof(ButtonTheme), typeof(InputTheme), typeof(MarkdownTheme)],
            "a style of the component's own is one of the theme's slot records, each with its Merge");
        Census.SlotRecords.Should().NotContain(typeof(DataPalette), "the data palette has no Merge to lay an "
            + "instance's over the theme's");
    }

    /// <summary>
    /// The instrument, in the condition it exists for: text in a property, in a constructor parameter no
    /// property carries, in a list and in a delegate's answer, and in an item type the component takes, is
    /// named; a node, a builder of nodes, a get-only member and the vocabulary's own <c>Key</c> are not; a
    /// component with its slot record as <c>style</c> takes a style, one without does not, and one whose
    /// <c>Style</c> the constructor cannot reach does not either.
    /// </summary>
    [Fact]
    public void TheCensus_NamesTextStylesAndItems_AndNothingElse()
    {
        var census = new Census([typeof(Probe).Assembly]);
        census.Measure(typeof(Probe));
        census.Measure(typeof(Unstyled));
        census.Measure(typeof(InitStyled));

        census.Entries.Keys.Should().BeEquivalentTo(
            "Probe.Title  text", "Probe(options)  text", "Probe(format)  text", "ProbeItem.Name  text",
            "Unstyled  style", "InitStyled  style");
        census.Entries["ProbeItem.Name  text"].Should().Be("ProbeItem", "an item's entry belongs to the item's type");
        census.Owned.Should().BeEquivalentTo("Probe", "ProbeItem", "Unstyled", "InitStyled");
    }

    /// <summary>The regenerator removes the entries the census no longer measures, and an owned item that
    /// carries no text any more, and keeps every other line as it was written.</summary>
    [Fact]
    public void Shrinking_RemovesWhatIsGone_AndWritesNothingNew()
    {
        var census = new Census([typeof(Probe).Assembly]);
        census.Measure(typeof(Probe));
        var path = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.baseline.txt");
        try
        {
            File.WriteAllLines(path,
            [
                "# A family.",
                "# owns: Probe ProbeItem Gone",
                "Probe.Title  text  kept: the probe's title",
                "Probe.Subtitle  text  #1",
                "ProbeItem.Name  text  #2",
            ]);
            var family = Family.Read(path);
            family.Shrink(census);

            File.ReadAllLines(path).Should().Equal(
                "# A family.",
                "# owns: Probe ProbeItem",
                "Probe.Title  text  kept: the probe's title",
                "ProbeItem.Name  text  #2");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>What the catalog takes, measured: entry ids (<c>Type.Member  text</c>, <c>Type(parameter)
    /// text</c>, <c>Component  style</c>) mapped to the type that owns each.</summary>
    private sealed class Census(IReadOnlyCollection<Assembly> catalog)
    {
        private readonly Dictionary<string, Type> _names = new(StringComparer.Ordinal);

        public Dictionary<string, string> Entries { get; } = new(StringComparer.Ordinal);

        /// <summary>The components measured, and the item types that carry text: what a family owns.</summary>
        public HashSet<string> Owned { get; } = new(StringComparer.Ordinal);

        public List<string> Collisions { get; } = [];

        /// <summary>The theme's slot records: what <see cref="IAppTheme"/> answers per family, each with the
        /// <c>Merge</c> a component lays its instance's record over the theme's with.</summary>
        public static IReadOnlySet<Type> SlotRecords { get; } = typeof(IAppTheme)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.PropertyType)
            .Where(type => type.GetMethod("Merge", BindingFlags.Public | BindingFlags.Static, [type, type]) is { } merge
                && merge.ReturnType == type)
            .ToHashSet();

        public static IEnumerable<Type> ComponentsOf(Assembly assembly)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException partial) { types = partial.Types.OfType<Type>().ToArray(); }
            return types.Where(type => type.IsPublic && !type.IsAbstract && typeof(UiComponent).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);
        }

        public void Measure(Type component)
        {
            component = Definition(component);
            var name = Name(component);
            Owned.Add(name);
            foreach (var key in TextMembers(component)) Entries[$"{key}  text"] = name;
            if (!TakesStyle(component)) Entries[$"{name}  style"] = name;

            var items = new HashSet<Type>();
            foreach (var type in Inputs(component)) Reach(type, items);
            foreach (var item in items)
            {
                var members = TextMembers(item).ToList();
                if (members.Count == 0) continue;
                Owned.Add(Name(item));
                foreach (var key in members) Entries[$"{key}  text"] = Name(item);
            }
        }

        /// <summary>
        /// A settable property that carries text, declared by the catalog (not the vocabulary's own
        /// <c>Key</c>), and a constructor parameter that carries text and no settable property of its name
        /// carries.
        /// </summary>
        private IEnumerable<string> TextMembers(Type type)
        {
            var settable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.DeclaringType!.Assembly == type.Assembly
                    && property.SetMethod is { IsPublic: true })
                .ToList();
            foreach (var property in settable.Where(property => CarriesText(property.PropertyType)))
                yield return $"{Name(type)}.{property.Name}";

            var parameters = type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => CarriesText(parameter.ParameterType))
                .Select(parameter => parameter.Name!)
                .Distinct(StringComparer.Ordinal);
            foreach (var parameter in parameters)
                if (!settable.Any(property => CarriesText(property.PropertyType)
                        && string.Equals(property.Name, parameter, StringComparison.OrdinalIgnoreCase)))
                    yield return $"{Name(type)}({parameter})";
        }

        /// <summary>A style of its own: the constructor's <c>style</c> and the <c>Style</c> it fills, of one
        /// slot record, so the factory, which mirrors the constructor, reaches it.</summary>
        private static bool TakesStyle(Type component)
        {
            var property = component.GetProperty("Style", BindingFlags.Public | BindingFlags.Instance);
            return property is not null && SlotRecords.Contains(property.PropertyType)
                && component.GetConstructors().Any(constructor => constructor.GetParameters()
                    .Any(parameter => parameter.Name == "style" && parameter.ParameterType == property.PropertyType));
        }

        private static IEnumerable<Type> Inputs(Type type) =>
            type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(property => property.PropertyType));

        /// <summary>The catalog's own types a component takes: through a list, an array, a generic
        /// argument, and the members of an item it already reached. A component, an enum and a delegate are
        /// not items.</summary>
        private void Reach(Type type, HashSet<Type> items)
        {
            if (type.IsArray) { Reach(type.GetElementType()!, items); return; }
            if (type.IsGenericType)
                foreach (var argument in type.GetGenericArguments()) Reach(argument, items);
            if (type.IsGenericParameter || !catalog.Contains(type.Assembly) || !type.IsVisible || type.IsEnum
                || typeof(VisualNode).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type)
                || (type.IsAbstract && type.IsSealed))
                return;
            var definition = Definition(type);
            if (!items.Add(definition)) return;
            foreach (var input in Inputs(definition)) Reach(input, items);
        }

        /// <summary>A <c>string</c>, a sequence of text, or a delegate that answers text.</summary>
        private static bool CarriesText(Type type)
        {
            if (type == typeof(string)) return true;
            if (type.IsArray) return CarriesText(type.GetElementType()!);
            if (typeof(Delegate).IsAssignableFrom(type))
                return type.GetMethod("Invoke") is { } invoke && invoke.ReturnType != typeof(void)
                    && CarriesText(invoke.ReturnType);
            var sequence = type.GetInterfaces().Append(type).FirstOrDefault(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return sequence is not null && CarriesText(sequence.GetGenericArguments()[0]);
        }

        private static Type Definition(Type type) =>
            type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;

        private string Name(Type type)
        {
            var name = type.Name.Split('`')[0];
            if (_names.TryGetValue(name, out var known) && known != type) Collisions.Add($"{known.FullName} / {type.FullName}");
            else _names[name] = type;
            return name;
        }
    }

    /// <summary>
    /// One family's file: the types it owns (<c># owns:</c> lines) and its entries, each
    /// <c>&lt;id&gt;  &lt;kind&gt;  &lt;reason&gt;</c>, two spaces apart.
    /// </summary>
    private sealed class Family
    {
        /// <summary>A decided reason: the issue that removes the entry, or <c>kept:</c> and why.</summary>
        public static readonly Regex Decided = new(@"^kept: \S|#\d+", RegexOptions.CultureInvariant);

        private const string OwnsPrefix = "# owns:";

        private Family(string path) => Path = path;

        public string Path { get; }

        public string Name => System.IO.Path.GetFileName(Path);

        public HashSet<string> Owns { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);

        public static IReadOnlyList<Family> ReadAll() =>
            Directory.EnumerateFiles(Folder(), "*.baseline.txt").Order(StringComparer.Ordinal).Select(Read).ToList();

        public static Family Read(string path)
        {
            var family = new Family(path);
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.StartsWith(OwnsPrefix, StringComparison.Ordinal))
                {
                    foreach (var name in line[OwnsPrefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        family.Owns.Add(name);
                    continue;
                }

                if (line.Length == 0 || line.StartsWith('#')) continue;
                var fields = line.Split("  ", 3, StringSplitOptions.TrimEntries);
                var entry = new Entry($"{fields[0]}  {(fields.Length > 1 ? fields[1] : "")}",
                    fields.Length > 2 ? fields[2] : "", index + 1);
                if (!family.Entries.TryAdd(entry.Id, entry))
                    throw new InvalidOperationException($"{family.Name}:{index + 1} repeats {entry.Id}");
            }

            return family;
        }

        /// <summary>
        /// Removes the entries the census no longer measures, and the owned names that are neither a
        /// component nor an item that carries text, keeping every other line, its reason and its place. It
        /// never writes an entry: the sentence that would justify one is the author's.
        /// </summary>
        public void Shrink(Census census)
        {
            var kept = new List<string>();
            foreach (var line in File.ReadAllLines(Path))
            {
                if (line.StartsWith(OwnsPrefix, StringComparison.Ordinal))
                {
                    var names = line[OwnsPrefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(census.Owned.Contains).ToList();
                    if (names.Count > 0) kept.Add($"{OwnsPrefix} {string.Join(' ', names)}");
                    continue;
                }

                if (line.Length > 0 && !line.StartsWith('#'))
                {
                    var fields = line.Split("  ", 3, StringSplitOptions.TrimEntries);
                    if (fields.Length > 1 && !census.Entries.ContainsKey($"{fields[0]}  {fields[1]}")) continue;
                }

                kept.Add(line);
            }

            File.WriteAllText(Path, string.Join("\n", kept) + "\n");
        }

        private static string Folder()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "eQuantic.UI.sln")))
                directory = directory.Parent;
            return System.IO.Path.Combine(
                directory?.FullName ?? throw new InvalidOperationException("repository root not found"),
                "tests", "eQuantic.UI.Web.Tests", "Coverage", "catalog-customization");
        }
    }

    private sealed record Entry(string Id, string Reason, int Line);

    // The instrument's own subjects. Never part of the catalog: the scan skips test assemblies.

    public sealed record ProbeItem(string Name, int Value);

    public sealed class Probe : StatelessComponent
    {
        public Probe(string title, VisualNode child, IReadOnlyList<string> options, Func<int, string> format,
            Func<int, VisualNode> rowBuilder, ProbeItem[] items, ButtonTheme? style = null)
        {
            Title = title;
            Child = child;
            Options = options;
            Format = format;
            RowBuilder = rowBuilder;
            Items = items;
            Style = style;
        }

        public string Title { get; init; }
        public VisualNode Child { get; init; }
        public IReadOnlyList<string> Options { get; }
        public Func<int, string> Format { get; }
        public Func<int, VisualNode> RowBuilder { get; init; }
        public ProbeItem[] Items { get; init; }
        public string Summary => Title;
        public ButtonTheme? Style { get; init; }

        public override VisualNode Build(ComponentContext context) => Child;
    }

    public sealed class Unstyled(VisualNode child) : StatelessComponent
    {
        public VisualNode Child { get; } = child;

        public override VisualNode Build(ComponentContext context) => Child;
    }

    public sealed class InitStyled(VisualNode child) : StatelessComponent
    {
        public VisualNode Child { get; } = child;

        public ButtonTheme? Style { get; init; }

        public override VisualNode Build(ComponentContext context) => Child;
    }
}
