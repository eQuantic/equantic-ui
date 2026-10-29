using System.Collections;
using System.Reflection;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests.Coverage;

/// <summary>
/// A <see cref="StatefulComponent"/> is KEPT by position across its parent's rebuilds, and it learns
/// what the parent hands it this time only through <see cref="UiComponent.AdoptConfig"/>, whose
/// default copies nothing. Every component overrides it by hand and lists what it copies, and a
/// property missing from the list works on the first build and silently ignores every later one.
/// <para>
/// It shipped that way more than once: in three components of one consumer app, before the rule was
/// written on <see cref="StatefulComponent"/>; then in <c>CodeEditor</c>'s <c>Diagnostics</c>,
/// <c>DiagnosticProviders</c> and <c>HoverProviders</c> (#1008), where every test passed, one of
/// them written to prove that the same problems handed again change nothing, because no test handed
/// a SECOND configuration, the one condition the bug lives in. The first run of this test found
/// <c>Disabled</c> dropped by five form controls and a cookie banner with no override at all (#1142).
/// </para>
/// <para>
/// So this test hands every component a second configuration, one input at a time. A kept instance
/// and a fresh one are built alike, the fresh one is made to differ in ONE input (a public
/// <c>set</c> or <c>init</c> property, or a constructor parameter), and the kept one adopts it. The
/// input is carried when the kept instance then answers what the fresh one does. It measures rather
/// than reads the override's source: a line that copies the wrong property reads as an assignment
/// and carries nothing.
/// </para>
/// <para>
/// A component that keeps the fresh instance WHOLE as its configuration (<c>_config = fresh</c>, as
/// <c>TableView</c> and the spreadsheet's parts do) carries every input by construction, the
/// property nobody has added yet included, which is the better shape for a component with many
/// settings. What this cannot see there is a <c>Build</c> that reads <c>this.X</c> where it meant
/// <c>_config.X</c>.
/// </para>
/// <para>
/// The exceptions are a LIST that may only shrink, each entry written by hand with its reason: an
/// input read once on purpose (<c>CodeEditor.InitialCode</c> opens the document, which is the
/// editor's own from then on) is a decision, and the list is where it is written down.
/// </para>
/// </summary>
public class AdoptConfigCoverageTests
{
    private const string UpdateVariable = "EQ_UPDATE_ADOPT_CONFIG_BASELINE";

    private static readonly Assembly[] Scanned = Discover();

    /// <summary>
    /// Every assembly beside this test that references the vocabulary. Derived from the build
    /// output, never listed: a component library added tomorrow is measured without anyone
    /// remembering to add it, and the answer does not depend on which tests happened to load what.
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
            if (assembly.GetReferencedAssemblies().Any(reference => reference.Name == vocabulary))
                found.Add(assembly);
        }
        return [.. found];
    }

    private static IEnumerable<Type> Components() => Scanned
        .SelectMany(assembly =>
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException partial) { return partial.Types.OfType<Type>().ToArray(); }
        })
        .Where(type => typeof(StatefulComponent).IsAssignableFrom(type) && !type.IsAbstract)
        .OrderBy(type => type.FullName, StringComparer.Ordinal);

    [Fact]
    public void EveryInputAParentHands_ReachesTheInstanceItsPositionKeeps()
    {
        var census = new Census();
        foreach (var component in Components()) census.Measure(component);

        string.Join("\n", census.Unmeasured).Should().BeEmpty(
            "an input this test cannot vary is an input nobody measured: teach FactoryArguments the "
            + "type it names, or build the component another way");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1") Regenerate(census.Dropped);
        var committed = Committed();

        var unlisted = census.Dropped.Where(drop => !committed.ContainsKey(drop.Key)).ToList();
        Assert.True(unlisted.Count == 0,
            "A component's position keeps its FIRST value of an input after the parent handed another. "
            + "Copy it in the component's AdoptConfig (`X = fresh.X;`), or keep the fresh instance whole "
            + "(`_config = fresh;`) and read the settings from it. If the input is read ONCE on purpose, "
            + "add it to the baseline BY HAND with the reason:\n  "
            + string.Join("\n  ", unlisted.Select(drop => $"{drop.Key}  ({drop.Value})")));

        var stale = committed.Keys.Where(key => !census.Dropped.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0,
            $"{stale.Count} entry(ies) are carried now, or name an input that is gone: regenerate "
            + $"({UpdateVariable}=1) so the list shrinks on record. Regenerating is for a REMOVAL: it "
            + "keeps every reason already written and never adds an entry:\n  "
            + string.Join("\n  ", stale));

        var unexplained = committed.Where(entry => entry.Value.Length == 0)
            .Select(entry => entry.Key).OrderBy(key => key, StringComparer.Ordinal).ToList();
        Assert.True(unexplained.Count == 0,
            "A baseline entry with no reason is an exemption nobody decided. Say why the input is read "
            + "once:\n  " + string.Join("\n  ", unexplained));
    }

    /// <summary>
    /// The scan answers over the shipped libraries, or the test above is asking about an empty set
    /// and passing.
    /// </summary>
    [Fact]
    public void TheScan_ReachesEveryLibraryThatShipsAStatefulComponent()
    {
        Scanned.Select(assembly => assembly.GetName().Name).Should().Contain(
            ["eQuantic.UI.Components", "eQuantic.UI.Charts", "eQuantic.UI.Spreadsheets"],
            "the libraries that ship a StatefulComponent today are where the scan has to reach");
        Components().Should().HaveCountGreaterThan(15,
            "the set is derived from the build output, so a number this low means the discovery found "
            + "almost nothing");
    }

    /// <summary>
    /// The instrument, in the condition it exists for: a component that forgets one property, wires
    /// another to the wrong source and drops a constructor argument is named for exactly those three,
    /// and one that keeps the fresh instance whole is named for nothing.
    /// </summary>
    [Fact]
    public void TheMeasurement_NamesWhatAnOverrideDrops_AndNothingElse()
    {
        var census = new Census();
        census.Measure(typeof(Forgetful));
        census.Measure(typeof(KeepsTheWholeConfiguration));

        census.Unmeasured.Should().BeEmpty();
        census.Dropped.Keys.Should().BeEquivalentTo(
            $"{typeof(Forgetful).FullName}.{nameof(Forgetful.Forgotten)}",
            $"{typeof(Forgetful).FullName}.{nameof(Forgetful.Crossed)}",
            $"{typeof(Forgetful).FullName}(subtitle)");
    }

    private sealed class Forgetful : StatefulComponent
    {
        private readonly string _subtitle;
        private string _title;

        public Forgetful(string title, string subtitle)
        {
            _title = title;
            _subtitle = subtitle;
        }

        public string? Carried { get; set; }
        public string? Forgotten { get; init; }
        public string? Crossed { get; set; }

        public override void AdoptConfig(UiComponent next)
        {
            if (next is not Forgetful fresh) return;
            _title = fresh._title;
            Carried = fresh.Carried;
            // An assignment a reading of the source would count, carrying the wrong value.
            Crossed = fresh.Carried;
        }

        public override VisualNode Build(ComponentContext context) =>
            new Text($"{_title} {_subtitle} {Carried} {Forgotten} {Crossed}");
    }

    private sealed class KeepsTheWholeConfiguration : StatefulComponent
    {
        private KeepsTheWholeConfiguration _config;

        public KeepsTheWholeConfiguration(string title)
        {
            Title = title;
            _config = this;
        }

        public string Title { get; }
        public string? Subtitle { get; init; }

        public override void AdoptConfig(UiComponent next)
        {
            if (next is KeepsTheWholeConfiguration fresh) _config = fresh;
        }

        public override VisualNode Build(ComponentContext context) =>
            new Text($"{_config.Title} {_config.Subtitle}");
    }

    /// <summary>
    /// What one pass over the components found: the inputs a kept instance does not carry, each with
    /// what stayed, and the inputs that could not be measured at all.
    /// </summary>
    private sealed class Census
    {
        public SortedDictionary<string, string> Dropped { get; } = new(StringComparer.Ordinal);
        public List<string> Unmeasured { get; } = [];

        public void Measure(Type component)
        {
            var constructor = component.GetConstructors().MaxBy(candidate => candidate.GetParameters().Length);
            if (constructor is null)
            {
                Unmeasured.Add($"{component.FullName}: no public constructor to build it with");
                return;
            }

            var parameters = constructor.GetParameters();
            if (!FactoryArguments.For(parameters, out var arguments, out var why))
            {
                Unmeasured.Add($"{component.FullName}: {why}");
                return;
            }

            // One plain adoption first: a component that keeps the fresh instance itself carries
            // every input, and measuring them one by one would only measure the field.
            if (!TryBuild(constructor, arguments, component.FullName!, out var kept)
                || !TryBuild(constructor, arguments, component.FullName!, out var fresh)
                || !TryAdopt(kept, fresh, component.FullName!))
                return;
            if (Fields(component).Any(field => ReferenceEquals(field.GetValue(kept), fresh))) return;

            foreach (var property in Settable(component))
                MeasureProperty(component, constructor, arguments, property);
            for (var i = 0; i < parameters.Length; i++)
                MeasureParameter(component, constructor, arguments, i);
        }

        /// <summary>A settable property, varied alone on the fresh instance after it is built.</summary>
        private void MeasureProperty(Type component, ConstructorInfo constructor, object?[] arguments,
            PropertyInfo property)
        {
            var input = $"{component.FullName}.{property.Name}";
            if (IsSingleValued(property.PropertyType)) return;

            string? refused = null;
            foreach (var varied in Variations(property.PropertyType, Probe(constructor, arguments, property)))
            {
                if (Build(constructor, arguments) is not { } kept || Build(constructor, arguments) is not { } fresh)
                    break;
                var from = property.GetValue(fresh);
                try
                {
                    property.SetValue(fresh, varied);
                }
                catch (TargetInvocationException failure)
                {
                    refused = $"setting {Show(varied)} threw {failure.InnerException?.Message}";
                    continue;
                }
                if (Same(property.GetValue(fresh), from))
                {
                    refused = $"the setter took {Show(varied)} and still answers {Show(from)}";
                    continue;
                }

                if (!TryAdopt(kept, fresh, input)) return;
                if (!Same(property.GetValue(kept), property.GetValue(fresh)))
                    Dropped[input] = $"stays {Show(property.GetValue(kept))} after a fresh instance handed "
                        + Show(property.GetValue(fresh));
                return;
            }

            Unmeasured.Add($"{input}: " + (refused
                ?? $"nothing tells a {property.PropertyType.Name} apart from the value it is built with"));
        }

        /// <summary>
        /// A constructor argument, varied alone. What it configures is read off the instance: the
        /// public properties it changes, or, when it changes none, the fields it does. Fields come
        /// second on purpose, since a field beside a property is often state the argument only
        /// SEEDS (the month a calendar shows), and the property is the configuration's own face.
        /// An argument that only reaches settable properties is left to their own measurement.
        /// </summary>
        private void MeasureParameter(Type component, ConstructorInfo constructor, object?[] arguments, int index)
        {
            var parameter = constructor.GetParameters()[index];
            var input = $"{component.FullName}({parameter.Name})";
            if (IsSingleValued(parameter.ParameterType)) return;

            string? refused = null;
            foreach (var varied in Variations(parameter.ParameterType, arguments[index]))
            {
                var handed = (object?[])arguments.Clone();
                handed[index] = varied;
                if (Build(constructor, handed, out var failure) is not { } fresh)
                {
                    refused = $"building it with {Show(varied)} threw {failure}";
                    continue;
                }
                if (Build(constructor, arguments) is not { } reference || Build(constructor, arguments) is not { } twin
                    || Build(constructor, arguments) is not { } kept)
                    break;

                // What differs between two instances built alike is the instance's own (a closure
                // over itself, a scroll it made), and says nothing about the argument.
                var members = Members(component).Where(member => Same(member.Read(reference), member.Read(twin))).ToList();
                var differing = members.Where(member => !Same(member.Read(fresh), member.Read(reference))).ToList();
                var shown = differing.Where(member => member.IsProperty).ToList();
                if (shown.Count == 0) shown = differing;
                if (shown.Count == 0)
                {
                    refused = $"nothing on the instance tells {Show(varied)} apart from {Show(arguments[index])}";
                    continue;
                }
                if (shown.All(member => member.IsSettable)) return;

                if (!TryAdopt(kept, fresh, input)) return;
                var stayed = shown.Where(member => !member.IsSettable && !Same(member.Read(kept), member.Read(fresh)))
                    .Select(member => member.Name).ToList();
                if (stayed.Count > 0)
                    Dropped[input] = $"{string.Join(", ", stayed)} keep(s) the first value";
                return;
            }

            Unmeasured.Add($"{input}: " + (refused
                ?? $"nothing tells a {parameter.ParameterType.Name} apart from {Show(arguments[index])}"));
        }

        /// <summary>What the property answers on an instance built with these arguments: the value
        /// a variation has to differ from.</summary>
        private static object? Probe(ConstructorInfo constructor, object?[] arguments, PropertyInfo property) =>
            Build(constructor, arguments) is { } built ? property.GetValue(built) : null;

        private bool TryBuild(ConstructorInfo constructor, object?[] arguments, string input,
            out StatefulComponent built)
        {
            built = Build(constructor, arguments, out var failure)!;
            if (built is not null) return true;
            Unmeasured.Add($"{input}: building it threw {failure}");
            return false;
        }

        private static StatefulComponent? Build(ConstructorInfo constructor, object?[] arguments) =>
            Build(constructor, arguments, out _);

        private static StatefulComponent? Build(ConstructorInfo constructor, object?[] arguments, out string? failure)
        {
            failure = null;
            try
            {
                return (StatefulComponent)constructor.Invoke((object?[])arguments.Clone());
            }
            catch (TargetInvocationException thrown)
            {
                failure = thrown.InnerException?.Message ?? thrown.Message;
                return null;
            }
        }

        private bool TryAdopt(StatefulComponent kept, StatefulComponent fresh, string input)
        {
            try
            {
                kept.AdoptConfig(fresh);
                return true;
            }
            catch (Exception failure)
            {
                Unmeasured.Add($"{input}: AdoptConfig threw {failure.GetType().Name}: {failure.Message}");
                return false;
            }
        }
    }

    /// <summary>An enum with one member has no other value to arrive as, so dropping it is
    /// unobservable: nothing to measure rather than something unmeasured.</summary>
    private static bool IsSingleValued(Type type) => type.IsEnum && Enum.GetValues(type).Length == 1;

    /// <summary>
    /// The values to vary an input to, in order: every other member of an enum, because a component
    /// may refuse one (a TextInput has no Small size), and otherwise the one value
    /// <see cref="FactoryArguments"/> tells apart from where it starts.
    /// </summary>
    private static IEnumerable<object?> Variations(Type type, object? from)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if ((underlying ?? type).IsEnum)
        {
            foreach (var member in Enum.GetValues(underlying ?? type))
                if (!Equals(member, from)) yield return member;
            if (underlying is not null && from is not null) yield return null;
            yield break;
        }
        if (!FactoryArguments.TryVary(type, from, 0, out var varied)) yield break;
        yield return varied;
        // A null argument varies to a default-built value, which is often the very default the
        // component substitutes for null (a BarChart's value axis): one step further tells them apart.
        if (from is null && varied is not null && FactoryArguments.TryVary(type, varied, 0, out var further))
            yield return further;
    }

    /// <summary>Declared by the component or a base of its own, never by the vocabulary: a
    /// <see cref="VisualNode"/>'s own settings are read off the configuration the element holds.</summary>
    private static bool IsOwn(Type? declaring) =>
        declaring is not null && declaring != typeof(StatefulComponent)
            && typeof(StatefulComponent).IsAssignableFrom(declaring);

    private static IEnumerable<PropertyInfo> Settable(Type component) => component
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => IsOwn(property.DeclaringType) && property.GetIndexParameters().Length == 0
            && property.GetMethod is { IsPublic: true } && property.SetMethod is { IsPublic: true })
        .OrderBy(property => property.Name, StringComparer.Ordinal);

    private static IEnumerable<FieldInfo> Fields(Type component)
    {
        for (var type = component; IsOwn(type); type = type!.BaseType)
            foreach (var field in type!.GetFields(BindingFlags.Instance | BindingFlags.Public
                         | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return field;
    }

    private sealed record Member(string Name, bool IsProperty, bool IsSettable, Func<object, object?> Read);

    private static IEnumerable<Member> Members(Type component)
    {
        foreach (var property in component.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (IsOwn(property.DeclaringType) && property.GetIndexParameters().Length == 0
                && property.GetMethod is { IsPublic: true })
                yield return new Member(property.Name, IsProperty: true,
                    IsSettable: property.SetMethod is { IsPublic: true }, instance => Read(() => property.GetValue(instance)));

        foreach (var field in Fields(component))
            yield return new Member(field.Name, IsProperty: false, IsSettable: false, field.GetValue);
    }

    /// <summary>A getter that throws on an instance nobody has built yet answers the same thing on
    /// every instance, which is what lets the comparison step over it.</summary>
    private static readonly object Unreadable = new();

    private static object? Read(Func<object?> getter)
    {
        try { return getter(); }
        catch (TargetInvocationException) { return Unreadable; }
    }

    /// <summary>Equal as a value, the same instance, or the same elements in the same order: a
    /// component that copies a list it was handed carries it as surely as one that keeps it.</summary>
    private static bool Same(object? first, object? second)
    {
        if (ReferenceEquals(first, second) || Equals(first, second)) return true;
        if (first is string || second is string) return false;
        if (first is not IEnumerable left || second is not IEnumerable right) return false;
        var lefts = left.Cast<object?>().ToList();
        var rights = right.Cast<object?>().ToList();
        return lefts.Count == rights.Count && lefts.Zip(rights).All(pair => Same(pair.First, pair.Second));
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        Delegate => "a delegate",
        IEnumerable sequence => $"a list of {sequence.Cast<object?>().Count()}",
        _ => value.ToString() ?? value.GetType().Name,
    };

    private static Dictionary<string, string> Committed()
    {
        var committed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(BaselinePath()))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var fields = line.Split("  ", 2, StringSplitOptions.TrimEntries);
            committed[fields[0]] = fields.Length > 1 ? fields[1] : "";
        }
        return committed;
    }

    /// <summary>
    /// Rewrites the list as the entries that still stay, each with the reason already written. It
    /// never ADDS one: an input that newly stays has no reason anybody wrote, and the regenerator
    /// cannot write it, so the test goes on failing until a person adopts the input or explains it.
    /// </summary>
    private static void Regenerate(SortedDictionary<string, string> dropped)
    {
        var lines = File.ReadAllLines(BaselinePath());
        var kept = lines.Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Where(line => dropped.ContainsKey(line.Split("  ", 2)[0].Trim()));
        File.WriteAllText(BaselinePath(),
            string.Join("\n", lines.TakeWhile(line => line.StartsWith('#')).Concat(kept)) + "\n");
    }

    private static string BaselinePath() => Path.Combine(
        RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "Coverage", "adopt-config.baseline.txt");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
