using System.Globalization;
using System.Reflection;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using eQuantic.UI.Web.Tests;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// ONE COMPONENT ANNOUNCES THE SAME ROLE, NAME, STATE AND ORDER ON EVERY TARGET (#339). The web writes
/// ARIA inline and Photon walks a semantics tree, and both are fed by the same component; nothing
/// compared them whole, which is how a radio, a tab and three more roles were a Button on every native
/// platform with every suite green.
/// <para>
/// THE SET IS DERIVED, never listed: every public component of every assembly beside this test that
/// references the vocabulary, built with plausible arguments (<see cref="FactoryArguments"/>), and a
/// <see cref="Pressable"/> in every <see cref="PressableRole"/>. A component a screen reader hears
/// nothing from on either target has no semantic role and compares nothing. The curated cases below
/// add what plausible arguments cannot: real content, and a disclosure opened before the reading.
/// </para>
/// <para>
/// Each side is read the way its reader reads it: the web through <see cref="ComponentTester"/>, whose
/// semantics are what a screen reader announces from the markup the server writes, and Photon through
/// <see cref="PhotonHost.Semantics"/>, what the bridges hand VoiceOver and TalkBack. The two readings
/// are aligned by role and name, so one stop missing on a side is named as that stop, and the stops
/// both sides announce are compared field by field.
/// </para>
/// <para>
/// A difference fails the build unless the baseline names it, with its reason, and the baseline may
/// ONLY SHRINK: an entry is written by hand, and <c>EQ_UPDATE_SEMANTICS_PARITY_BASELINE=1</c> drops the
/// entries that no longer occur, keeping every reason, and never adds one.
/// </para>
/// </summary>
public class SemanticsParityTests(ITestOutputHelper output)
{
    private const string UpdateVariable = "EQ_UPDATE_SEMANTICS_PARITY_BASELINE";
    private const float Width = 1280f;
    private const float Height = 800f;
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>What one stop says, in the vocabulary's words, on either target.</summary>
    private readonly record struct Said(SemanticRole Role, string Name, string? Value, bool Disabled,
        SemanticCheck? Checked, bool? Selected, bool? Expanded, bool Current, int HeadingLevel)
    {
        public override string ToString() => $"{Role} \"{Name}\"";
    }

    /// <summary>
    /// The vocabulary's roles ARIA has no word for, and the word the web writes instead. A reading of the
    /// web can never say them, so Photon's reading is compared in the words both can say. A destination
    /// is a button the web marks current with <c>aria-current</c>, as <see cref="PressableRole.Destination"/>
    /// says; a code field is the multi-line text box a web reader hears. Each is the realizer's spelling,
    /// not one component's exception, which is why it is a table here and not a line of the baseline.
    /// </summary>
    private static readonly Dictionary<SemanticRole, SemanticRole> WebSpelling = new()
    {
        [SemanticRole.Destination] = SemanticRole.Button,
        [SemanticRole.CodeField] = SemanticRole.TextField,
    };

    /// <summary>One subject under comparison: what it is called in a difference, how a fresh instance of it
    /// is built (a stateful one keeps what a press did to it), and whether its one disclosure is opened
    /// before the reading.</summary>
    private sealed record Subject(string Name, Func<VisualNode> Build, bool Open = false);

    [Fact]
    public void EveryComponent_AnnouncesTheSameRolesNamesStatesAndOrder_OnTheWebAndPhoton()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var compared = 0;
        foreach (var subject in Subjects())
        {
            var differences = Compare(subject, out var announced);
            if (announced) compared++;
            found.UnionWith(differences);
        }

        compared.Should().BeGreaterThan(40, "the derived set reaches every component with a semantic role");

        var path = BaselinePath();
        var baseline = ReadBaseline(path);
        var unexplained = found.Where(entry => !baseline.ContainsKey(entry)).ToList();
        var stale = baseline.Keys.Where(entry => !found.Contains(entry)).ToList();

        // The whole list, which an assertion's message cuts short: it is what the baseline's author reads.
        foreach (var entry in unexplained) output.WriteLine($"unexplained: {entry}");
        foreach (var entry in stale) output.WriteLine($"stale: {entry}");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1" && stale.Count > 0)
            WriteBaseline(path, baseline.Where(pair => found.Contains(pair.Key)));

        string.Join(Environment.NewLine, unexplained).Should().BeEmpty(
            "a component says the same thing to a screen reader on every target; a difference that is right "
            + $"is written by hand into {Path.GetFileName(path)}, with its reason");
        string.Join(Environment.NewLine, stale).Should().BeEmpty(
            $"the baseline may only shrink: these no longer differ, so their entries go ({UpdateVariable}=1)");
    }

    // ---- the subjects ----------------------------------------------------------------------------

    private static IEnumerable<Subject> Subjects()
    {
        foreach (var subject in Curated()) yield return subject;
        foreach (var role in Enum.GetValues<PressableRole>())
            yield return new Subject($"Pressable({role})",
                () => new Pressable(new Text("Choose"), () => { }) { Role = role });
        var overrides = Overrides().ToDictionary(subject => subject.Name, StringComparer.Ordinal);
        foreach (var type in Components())
            yield return overrides.GetValueOrDefault(type.FullName!) ?? Derived(type);
    }

    /// <summary>
    /// The components plausible arguments cannot read, built here instead and compared IN THEIR PLACE in
    /// the derived set: a navigation refuses an empty list of destinations, a dialog an empty row of
    /// actions, a list beside a detail a list that builds nothing; and a calendar left without a date
    /// shows the month the clock says, so a difference in it would name another month every month.
    /// </summary>
    private static IEnumerable<Subject> Overrides()
    {
        NavItem[] destinations = [new(Icons.Person, "Profile"), new(Icons.Search, "Search"), new(Icons.Heart, "Saved")];
        yield return Instead<BottomNavigation>(() => new BottomNavigation(destinations, 1, _ => { }));
        yield return Instead<NavigationRail>(() => new NavigationRail(destinations, 0, _ => { }));
        yield return Instead<ListDetail>(() => new ListDetail(() => new Text("Inbox"), () => new Text("Message")));
        yield return Instead<Dialog>(() => Page(new Dialog("Sign out?", "You can sign back in anytime.",
            [new DialogAction("Stay", null, Variant.Ghost), new DialogAction("Sign out")])));
        yield return Instead<eQuantic.UI.Components.Calendar>(() => new eQuantic.UI.Components.Calendar(new DateOnly(2026, 7, 17), _ => { }));
    }

    private static Subject Instead<T>(Func<VisualNode> build) where T : UiComponent => new(typeof(T).FullName!, build);

    /// <summary>The cases plausible arguments cannot make: real content, and an opened disclosure.</summary>
    private static IEnumerable<Subject> Curated()
    {
        yield return new("case tabs", () => new Tabs(["Overview", "Activity", "Settings"], 1, _ => { }));
        yield return new("case radio-group", () => new RadioGroup(["Standard", "Express"], 1, _ => { }, "Shipping"));
        yield return new("case segmented-control", () => new SegmentedControl(["All", "Income"], 0, _ => { }));
        yield return new("case select", () => new Select(["Lisbon", "Porto"], 0, _ => { }));
        yield return new("case select opened", () => new Select(["Lisbon", "Porto"], 0, _ => { }), Open: true);
        yield return new("case grouped select opened", () => new Select(["Lisboa"], 0, _ => { },
            groups: [new OptionGroup("A seguir", ["Braga"])]), Open: true);
        yield return new("case time-picker", () => new TimePicker(new TimeOnly(9, 30), _ => { }, label: "Arrival"));
        yield return new("case disabled select", () => new Select(["Lisbon", "Porto"], 0, _ => { }) { Disabled = true });
        yield return new("case disabled time-picker",
            () => new TimePicker(new TimeOnly(9, 30), _ => { }, label: "Arrival") { Disabled = true });
        yield return new("case menu opened",
            () => new Menu(new Button("More"), [new MenuItem("Rename"), new MenuItem("Delete")], _ => { }), Open: true);
        yield return new("case date-picker opened",
            () => new DatePicker(new DateOnly(2026, 7, 17), _ => { }, label: "Departure"), Open: true);
        yield return new("case alert-dialog", () => Page(new Dialog("Delete card?", "It cannot be undone.",
            [new DialogAction("Keep", null, Variant.Ghost), new DialogAction("Delete", null, Variant.Destructive)])));
        yield return new("case checkbox", () => new Checkbox(true, label: "Agree"));
        yield return new("case switch", () => new Switch(false));
        yield return new("case progress", () => new ProgressBar(0.42f));
        yield return new("case slider", () => new Slider(0.5f, _ => { }));
        yield return new("case text-input", () => new TextInput("ana@example.com", _ => { }, label: "Email"));
        yield return new("case image", () => new Image("https://example.com/a.png", 120, 80, ImageFit.Cover, "A harbour at dawn"));
        yield return new("case heading", () => new Text("Cards", TypeRole.Title, headingLevel: 2));
        yield return new("case list", () => new eQuantic.UI.Components.List(
            [new ListItem("Wi-Fi", "Photon-5G", onPressed: () => { }), new ListItem("Notifications")]));
        // A table and a grid, whose rows the web writes in bands and a reader reads through aria-owns.
        foreach (var role in Enum.GetValues<TableRole>())
            yield return new($"case table-view {role}", () => Table(role));
        yield return new("case table-viewport composed column by column", Viewport);
    }

    private static VisualNode Table(TableRole role) =>
        new TableView(new UniformTableSpans(6, 28), new UniformTableSpans(4, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.BodyM))
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Label = "Ledger",
            Role = role,
            CellRole = (row, column) => row == 0 ? TableCellRole.ColumnHeader
                : column == 0 ? TableCellRole.RowHeader
                : TableCellRole.Cell,
            Selected = (row, column) => column == 2 && row >= 2 && row <= 3,
            Current = new TableRange(3, 2),
        };

    /// <summary>A viewport whose cells are added down the columns: both targets read the rows, each row's
    /// cells by column, whatever order they were added in.</summary>
    private static VisualNode Viewport()
    {
        var rows = new UniformTableSpans(2, 28);
        var columns = new UniformTableSpans(2, 96);
        var scroll = new TableScroll();
        scroll.Arrange(new Size(400, 300), columns.Total, rows.Total, 96, 28, reduceMotion: true);
        var viewport = new TableViewport(rows, columns, scroll) { Label = "Ledger", Role = TableRole.Table };
        foreach (var (row, column) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            viewport.Add(new TableViewCell(row, column, new Text($"{row}:{column}", TypeRole.BodyM)));
        return viewport;
    }

    private static VisualNode Page(VisualNode layer)
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Text("Cards", TypeRole.Title));
        page.Add(layer);
        return page;
    }

    /// <summary>A component of an assembly beside this test, built through its widest constructor.</summary>
    private static Subject Derived(Type type)
    {
        var constructor = type.GetConstructors().MaxBy(candidate => candidate.GetParameters().Length);
        if (constructor is null)
            return new(type.FullName!, () => throw new MissingMethodException("no public constructor to build it with"));
        if (!FactoryArguments.For(constructor.GetParameters(), out var arguments, out var why))
            return new(type.FullName!, () => throw new MissingMethodException(why));
        return new(type.FullName!, () => (VisualNode)constructor.Invoke(arguments));
    }

    /// <summary>Every public, concrete component of every assembly beside this test that references the
    /// vocabulary. Derived from the build output, never listed.</summary>
    private static IEnumerable<Type> Components()
    {
        var vocabulary = typeof(VisualNode).Assembly.GetName().Name;
        var types = new List<Type>();
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "eQuantic.*.dll"))
        {
            if (Path.GetFileNameWithoutExtension(path).EndsWith(".Tests", StringComparison.Ordinal)) continue;
            Assembly assembly;
            try { assembly = Assembly.LoadFrom(path); }
            catch (BadImageFormatException) { continue; }
            catch (FileLoadException) { continue; }
            if (assembly.GetReferencedAssemblies().All(reference => reference.Name != vocabulary)) continue;
            Type[] exported;
            try { exported = assembly.GetExportedTypes(); }
            catch (Exception) { continue; }
            types.AddRange(exported.Where(type => typeof(UiComponent).IsAssignableFrom(type)
                && type is { IsAbstract: false, ContainsGenericParameters: false }));
        }
        return types.Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    // ---- the comparison --------------------------------------------------------------------------

    private static List<string> Compare(Subject subject, out bool announced)
    {
        var web = Read(() => OnTheWeb(subject));
        var photon = Read(() => OnPhoton(subject));
        announced = web.Said.Count > 0 || photon.Said.Count > 0;

        var differences = new List<string>();
        if (web.Failure is not null || photon.Failure is not null)
        {
            if (web.Failure is not null) differences.Add($"{subject.Name}: not read on the web: {web.Failure}");
            if (photon.Failure is not null) differences.Add($"{subject.Name}: not read on Photon: {photon.Failure}");
            announced = true;
            return differences;
        }

        foreach (var (w, p) in Align(web.Said, photon.Said))
        {
            if (p is null) { differences.Add($"{subject.Name}: {w} is announced on the web only"); continue; }
            if (w is null) { differences.Add($"{subject.Name}: {p} is announced on Photon only"); continue; }
            Field("value", w.Value.Value, p.Value.Value);
            Field("disabled", w.Value.Disabled, p.Value.Disabled);
            Field("checked", w.Value.Checked, p.Value.Checked);
            Field("selected", w.Value.Selected, p.Value.Selected);
            Field("expanded", w.Value.Expanded, p.Value.Expanded);
            Field("current", w.Value.Current, p.Value.Current);
            Field("heading level", w.Value.HeadingLevel, p.Value.HeadingLevel);

            void Field<T>(string field, T onTheWeb, T onPhoton)
            {
                if (!EqualityComparer<T>.Default.Equals(onTheWeb, onPhoton))
                    differences.Add($"{subject.Name}: {w}: {field} is {Show(onTheWeb)} on the web and {Show(onPhoton)} on Photon");
            }
        }

        if (differences.Count == 0 && !web.Said.SequenceEqual(photon.Said))
            differences.Add($"{subject.Name}: the same stops are read in another order");
        return differences;
    }

    /// <summary>An empty value and none are the same silence to a reader: a field whose text is empty.</summary>
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Show<T>(T value) => value switch
    {
        null => "none",
        string text => $"\"{text}\"",
        bool flag => flag ? "true" : "false",
        _ => value.ToString()!,
    };

    /// <summary>The two readings aligned by role and name, in order: the longest run of stops both sides
    /// announce, and every other stop on the side that announces it.</summary>
    private static List<(Said? Web, Said? Photon)> Align(IReadOnlyList<Said> web, IReadOnlyList<Said> photon)
    {
        static bool Same(Said a, Said b) => a.Role == b.Role && a.Name == b.Name;
        var lengths = new int[web.Count + 1, photon.Count + 1];
        for (var i = web.Count - 1; i >= 0; i--)
            for (var j = photon.Count - 1; j >= 0; j--)
                lengths[i, j] = Same(web[i], photon[j])
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

        var aligned = new List<(Said?, Said?)>();
        int x = 0, y = 0;
        while (x < web.Count && y < photon.Count)
        {
            if (Same(web[x], photon[y])) aligned.Add((web[x++], photon[y++]));
            else if (lengths[x + 1, y] >= lengths[x, y + 1]) aligned.Add((web[x++], null));
            else aligned.Add((null, photon[y++]));
        }
        while (x < web.Count) aligned.Add((web[x++], null));
        while (y < photon.Count) aligned.Add((null, photon[y++]));
        return aligned;
    }

    private sealed record Reading(IReadOnlyList<Said> Said, string? Failure);

    /// <summary>A side's reading, or why there is none: a component that throws on one target is a
    /// difference of its own, named by the exception's type.</summary>
    private static Reading Read(Func<IReadOnlyList<Said>> read)
    {
        try
        {
            return new(read(), null);
        }
        catch (Exception thrown)
        {
            var inner = thrown is TargetInvocationException { InnerException: { } cause } ? cause : thrown;
            return new([], inner.GetType().Name);
        }
    }

    private static IReadOnlyList<Said> OnTheWeb(Subject subject)
    {
        using var tester = new ComponentTester(Theme, width: Width);
        // Built in the culture both sides read in, as the Photon side builds it: a constructor that formats
        // would otherwise read the machine's.
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        VisualNode built;
        try { built = subject.Build(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture); }
        tester.Pump(built);
        if (subject.Open)
        {
            var trigger = tester.Semantics.Single(stop => stop.Expanded == false);
            tester.Tap(trigger.Role, trigger.Name);
            tester.Pump();
        }

        return tester.Semantics
            .Select(stop => new Said(stop.Role, stop.Name, NullIfEmpty(stop.Value), stop.Disabled, stop.Checked, stop.Selected,
                stop.Expanded, stop.Current, stop.HeadingLevel))
            .ToList();
    }

    private static IReadOnlyList<Said> OnPhoton(Subject subject)
    {
        // The culture the web side reads in (the component tester's default): a date the two format
        // differently would be the machine's culture talking, not the component.
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        var host = new PhotonHost(subject.Build(), Theme, ThemeMode.Light, Width, Height);
        try
        {
            host.RenderFrame(new DisplayListBuilder());
            if (subject.Open)
            {
                host.Activate(host.Semantics().Single(stop => stop.Expanded == false).Id).Should().BeTrue();
                host.RenderFrame(new DisplayListBuilder(), 16);
            }

            return host.Semantics()
                .Select(stop => new Said(WebSpelling.GetValueOrDefault(stop.Role, stop.Role), stop.Label, NullIfEmpty(stop.Value), stop.Disabled, stop.Checked, stop.Selected,
                    stop.Expanded, stop.Current, stop.HeadingLevel))
                .ToList();
        }
        finally
        {
            host.Unmount();
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }

    // ---- the baseline ----------------------------------------------------------------------------

    private static string BaselinePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "eQuantic.UI.Native.Engine.Tests", "Coverage", "semantics-parity.baseline.txt");
    }

    /// <summary>Each entry and its reason: the entry runs to the first two spaces, the reason after.</summary>
    private static Dictionary<string, string> ReadBaseline(string path)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return entries;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var split = line.IndexOf("  ", StringComparison.Ordinal);
            if (split < 0) entries[line] = "";
            else entries[line[..split]] = line[(split + 2)..];
        }
        return entries;
    }

    private static void WriteBaseline(string path, IEnumerable<KeyValuePair<string, string>> kept)
    {
        var header = File.ReadLines(path).TakeWhile(line => line.StartsWith('#'));
        File.WriteAllLines(path, header.Concat(kept.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}  {pair.Value}")));
    }
}
