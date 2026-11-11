using System.Globalization;
using System.Reflection;
using System.Text;
using eQuantic.UI.Native.Testing;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// EVERY SHIPPED CONTROL IS PINNED AT EVERY VARIANT, SIZE AND DENSITY IT ACCEPTS (#1247, the variant
/// matrix of #294). A control is lowered once per cell, and each cell is pinned three ways: what the web
/// lowers (the markup and the atomic rules it names, through <see cref="ComponentTester"/>), what the
/// semantics announce, and what Photon measures (every box the layout placed).
/// <para>
/// THE CONTROLS ARE DERIVED, never listed: every component of <see cref="ComponentCatalog"/>. A control
/// ACCEPTS a <see cref="Variant"/> or a <see cref="SizeVariant"/> when its widest constructor takes one, or
/// when it has a public property of that type an author can set, and it is drawn at every value of it.
/// Every control accepts both densities: the target picks one. A new control joins the matrix with no edit
/// to this file, and its pins are written by <c>EQ_UPDATE_VARIANT_MATRIX=1</c>, like the transpiled pins.
/// </para>
/// <para>
/// A CELL THAT CANNOT EXIST is a line of <c>Coverage/variant-matrix.baseline.txt</c>, with its reason: a
/// control with no variant or no size, a value a control refuses, a target it cannot be drawn on. The
/// baseline may ONLY SHRINK. An entry is written by hand, and the update drops the entries that no longer
/// occur, keeping every reason, and never adds one.
/// </para>
/// </summary>
public class VariantMatrixTests(ITestOutputHelper output)
{
    private const string UpdateVariable = "EQ_UPDATE_VARIANT_MATRIX";
    private const float Width = 480f;
    private const float Height = 800f;
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>How one axis reaches a control: a parameter of the constructor it is authored through, or
    /// a property an author sets.</summary>
    private sealed record Axis(int Parameter, PropertyInfo? Property)
    {
        public override string ToString() => Property is null ? $"parameter {Parameter}" : Property.Name;
    }

    /// <summary>One control, how it is built, and the axes it accepts; or why it cannot be built.</summary>
    private sealed record Control(Type Type, ConstructorInfo? Constructor, object?[] Arguments,
        Func<VisualNode>? Override, Axis? Variant, Axis? Size, string? Unbuilt)
    {
        public string Name => Type.FullName!.StartsWith("eQuantic.UI.", StringComparison.Ordinal)
            ? Type.FullName["eQuantic.UI.".Length..]
            : Type.FullName;
    }

    /// <summary>One cell of a control's matrix.</summary>
    private readonly record struct Cell(Variant? Variant, SizeVariant? Size, Density Density)
    {
        public override string ToString() => string.Join(" · ",
            new[] { Variant?.ToString(), Size?.ToString(), Density.ToString() }.Where(part => part is not null));
    }

    [Fact]
    public void EveryControl_IsPinned_AtEveryVariantSizeAndDensityItAccepts()
    {
        var update = Environment.GetEnvironmentVariable(UpdateVariable) == "1";
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var drawn = new Dictionary<string, string>(StringComparer.Ordinal);
        var cells = 0;

        foreach (var control in Controls())
        {
            if (control.Unbuilt is not null)
            {
                found.Add($"{control.Name}: not built ({control.Unbuilt})");
                continue;
            }
            if (control.Variant is null) found.Add($"{control.Name}: no Variant");
            if (control.Size is null) found.Add($"{control.Name}: no SizeVariant");

            var pin = new StringBuilder();
            var failures = new List<(Cell Cell, string Failure)>();
            foreach (var cell in CellsOf(control))
            {
                var reading = Read(control, cell, out var failure);
                if (failure is not null) { failures.Add((cell, failure)); continue; }
                pin.Append(reading);
                cells++;
            }

            found.UnionWith(Failures(control, failures));

            if (pin.Length > 0) drawn[control.Name] = pin.ToString();
        }

        cells.Should().BeGreaterThan(200, "the derived set reaches every control at every cell it accepts");

        var pinDifferences = ComparePins(drawn, update);
        var baselineDifferences = CompareBaseline(found, update);

        string.Join(Environment.NewLine, pinDifferences).Should().BeEmpty(
            $"every cell draws what its pin says; a change that is right is written with {UpdateVariable}=1, "
            + "and the diff of the pins is its review");
        string.Join(Environment.NewLine, baselineDifferences).Should().BeEmpty(
            "a cell that cannot exist is written by hand into variant-matrix.baseline.txt, with its reason, "
            + $"and the baseline may only shrink ({UpdateVariable}=1 drops what no longer occurs)");
    }

    /// <summary>
    /// The cells a control cannot draw, each said once at the widest scope it holds: a failure every cell
    /// shares is the control's, one every density of a variant and size shares is that pair's, and any
    /// other is its cell's.
    /// </summary>
    private static IEnumerable<string> Failures(Control control, List<(Cell Cell, string Failure)> failures)
    {
        if (failures.Count == 0) yield break;
        if (failures.Count == CellsOf(control).Count() && failures.All(entry => entry.Failure == failures[0].Failure))
        {
            yield return $"{control.Name}: {failures[0].Failure}";
            yield break;
        }
        foreach (var pair in failures.GroupBy(entry => (entry.Cell.Variant, entry.Cell.Size)))
        {
            var first = pair.First().Failure;
            if (pair.Count() == Enum.GetValues<Density>().Length && pair.All(entry => entry.Failure == first))
            {
                var scope = string.Join(" · ", new[] { pair.Key.Variant?.ToString(), pair.Key.Size?.ToString() }
                    .Where(part => part is not null));
                yield return $"{control.Name} · {scope}: {first}";
                continue;
            }
            foreach (var (cell, failure) in pair) yield return $"{control.Name} · {cell}: {failure}";
        }
    }

    // ---- the controls ----------------------------------------------------------------------------

    private static IEnumerable<Control> Controls()
    {
        foreach (var type in ComponentCatalog.Components)
        {
            var built = ComponentCatalog.TryWidest(type, out var constructor, out var arguments, out var why);
            ComponentCatalog.Overrides.TryGetValue(type, out var build);
            if (!built && build is null)
            {
                yield return new(type, null, [], null, null, null, why);
                continue;
            }

            var parameters = built ? constructor!.GetParameters() : [];
            var variant = AxisOf<Variant>(type, parameters, build is not null, out var variantWhy);
            var size = AxisOf<SizeVariant>(type, parameters, build is not null, out var sizeWhy);
            yield return new(type, constructor, arguments, build, variant, size, variantWhy ?? sizeWhy);
        }
    }

    /// <summary>
    /// How a control takes <typeparamref name="T"/>: the one constructor parameter of that type, or else
    /// the one public property of that type with a public setter. Two of either is a control whose axis is
    /// not one thing, named rather than guessed. A control built by an override takes it by its property,
    /// since the override chooses its own arguments.
    /// </summary>
    private static Axis? AxisOf<T>(Type type, ParameterInfo[] parameters, bool overridden, out string? why)
    {
        why = null;
        var byParameter = parameters.Where(parameter => Unwrap(parameter.ParameterType) == typeof(T)).ToList();
        if (byParameter.Count > 1)
        {
            why = $"{byParameter.Count} {typeof(T).Name} parameters";
            return null;
        }
        var byProperty = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => Unwrap(property.PropertyType) == typeof(T) && property.SetMethod is { IsPublic: true })
            .ToList();
        if (byParameter.Count == 1 && !overridden) return new(byParameter[0].Position, null);
        if (byProperty.Count > 1)
        {
            why = $"{byProperty.Count} settable {typeof(T).Name} properties";
            return null;
        }
        if (byProperty.Count == 1) return new(-1, byProperty[0]);
        if (byParameter.Count == 1) why = $"its {typeof(T).Name} is a constructor parameter its override does not pass";
        return null;
    }

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static IEnumerable<Cell> CellsOf(Control control)
    {
        Variant?[] variants = control.Variant is null ? [null] : Enum.GetValues<Variant>().Select(v => (Variant?)v).ToArray();
        SizeVariant?[] sizes = control.Size is null ? [null] : Enum.GetValues<SizeVariant>().Select(s => (SizeVariant?)s).ToArray();
        foreach (var variant in variants)
            foreach (var size in sizes)
                foreach (var density in Enum.GetValues<Density>())
                    yield return new(variant, size, density);
    }

    /// <summary>A fresh instance of the control at the cell's variant and size.</summary>
    private static VisualNode Build(Control control, Cell cell)
    {
        VisualNode node;
        if (control.Override is { } build) node = build();
        else
        {
            var arguments = (object?[])control.Arguments.Clone();
            if (control.Variant is { Property: null } variant) arguments[variant.Parameter] = cell.Variant;
            if (control.Size is { Property: null } size) arguments[size.Parameter] = cell.Size;
            node = (VisualNode)control.Constructor!.Invoke(arguments);
        }
        control.Variant?.Property?.SetValue(node, cell.Variant);
        control.Size?.Property?.SetValue(node, cell.Size);
        return node;
    }

    // ---- one cell --------------------------------------------------------------------------------

    /// <summary>The cell's pin, or why it cannot be drawn: the control refuses the cell, or a target
    /// cannot draw it. Each target is handed an instance of its own, as each would build one, and the
    /// first build is the one that says whether the control accepts the cell at all.</summary>
    private static string Read(Control control, Cell cell, out string? failure)
    {
        failure = null;
        if (!Try(() => InTheCulture(() => Build(control, cell)), out var forTheWeb, out var refused))
        {
            failure = $"refused ({refused})";
            return "";
        }
        if (!Try(() => OnTheWeb(forTheWeb!, cell), out var web, out var webFailure))
        {
            failure = $"not drawn on the web ({webFailure})";
            return "";
        }
        if (!Try(() => OnPhoton(InTheCulture(() => Build(control, cell)), cell), out var photon, out var photonFailure))
        {
            failure = $"not drawn on Photon ({photonFailure})";
            return "";
        }
        return $"## {cell}\n{web}{photon}\n";
    }

    private static bool Try<T>(Func<T> read, out T? value, out string? failure)
    {
        try
        {
            value = read();
            failure = null;
            return true;
        }
        catch (Exception thrown)
        {
            var inner = thrown is TargetInvocationException { InnerException: { } cause } ? cause : thrown;
            value = default;
            failure = inner.GetType().Name;
            return false;
        }
    }

    /// <summary>What the web lowers and what its reader announces, through the component tester.</summary>
    private static string OnTheWeb(VisualNode node, Cell cell)
    {
        using var tester = new ComponentTester(Theme, width: Width, density: cell.Density);
        tester.Pump(node);
        var text = new StringBuilder("### web\n");
        // One element or text a line, one rule a line: a pin is read as a diff.
        text.Append(tester.Html.Replace("><", ">\n<", StringComparison.Ordinal)).Append('\n');
        text.Append("### css\n");
        var css = tester.Css.Replace("}", "}\n", StringComparison.Ordinal);
        text.Append(css.EndsWith('\n') ? css : css + "\n");
        text.Append("### semantics\n");
        foreach (var announcement in tester.Semantics) text.Append(announcement).Append('\n');
        return text.ToString();
    }

    /// <summary>What Photon measures, through the Photon tester: every box the layout placed, in the tree's
    /// order, and every overlay layer the frame showed.</summary>
    private static string OnPhoton(VisualNode node, Cell cell)
    {
        using var tester = new PhotonTester(Theme, width: Width, height: Height, density: cell.Density);
        tester.Pump(node);
        var text = new StringBuilder("### photon\n");
        Boxes(tester.Root, 0, text);
        foreach (var layer in tester.Layers)
        {
            text.Append("### photon layer\n");
            Boxes(layer, 0, text);
        }
        return text.ToString();
    }

    private static void Boxes(MeasuredNode node, int depth, StringBuilder text)
    {
        var bounds = node.Bounds;
        text.Append(' ', depth * 2).Append(node.Node.GetType().Name).Append(' ')
            .Append(Number(bounds.X)).Append(' ').Append(Number(bounds.Y)).Append(' ')
            .Append(Number(bounds.Width)).Append('x').Append(Number(bounds.Height));
        if (node.Text is { } measured) text.Append(' ').Append(measured.Lines.Count).Append(" line(s)");
        text.Append('\n');
        foreach (var child in node.Children) Boxes(child, depth + 1, text);
    }

    /// <summary>A coordinate to two places, in the invariant culture, and never "-0": a box a hair left of
    /// zero on one platform and a hair right on another is the same box.</summary>
    private static string Number(float value)
    {
        var rounded = MathF.Round(value, 2);
        return (rounded == 0 ? 0f : rounded).ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Built in the culture both targets read in: a constructor that formats would otherwise read
    /// the machine's.</summary>
    private static VisualNode InTheCulture(Func<VisualNode> build)
    {
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try { return build(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture); }
    }

    // ---- the pins --------------------------------------------------------------------------------

    private static string PinDirectory() => Path.Combine(ShrinkingBaseline.RepositoryRoot(), "tests", "eQuantic.UI.Native.Engine.Tests", "VariantMatrix");

    private List<string> ComparePins(Dictionary<string, string> drawn, bool update)
    {
        var directory = PinDirectory();
        var differences = new List<string>();
        var pinned = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.txt").ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        if (update) Directory.CreateDirectory(directory);
        foreach (var (name, pin) in drawn.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(directory, name + ".txt");
            var expected = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
            if (expected == pin) continue;
            if (update) { File.WriteAllText(path, pin); continue; }
            differences.Add(expected is null
                ? $"{name}: no pin"
                : $"{name}: {DifferingCells(expected, pin)}");
        }
        foreach (var (name, path) in pinned.Where(pair => !drawn.ContainsKey(pair.Key)))
        {
            if (update) File.Delete(path);
            else differences.Add($"{name}: pinned, and no longer drawn");
        }

        foreach (var difference in differences) output.WriteLine(difference);
        return differences;
    }

    /// <summary>The cells whose section of the pin changed, by their heading.</summary>
    private static string DifferingCells(string expected, string actual)
    {
        // A cell's section runs from its heading, the one line that opens with exactly two marks, to the next.
        static Dictionary<string, string> Sections(string pin)
        {
            var sections = new Dictionary<string, string>(StringComparer.Ordinal);
            string? heading = null;
            var body = new StringBuilder();
            foreach (var line in pin.Split('\n'))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    if (heading is not null) sections[heading] = body.ToString();
                    heading = line[3..];
                    body.Clear();
                    continue;
                }
                body.Append(line).Append('\n');
            }
            if (heading is not null) sections[heading] = body.ToString();
            return sections;
        }
        var before = Sections(expected);
        var after = Sections(actual);
        var changed = after.Keys.Union(before.Keys)
            .Where(cell => !before.TryGetValue(cell, out var was) || !after.TryGetValue(cell, out var now) || was != now)
            .ToList();
        return $"{changed.Count} cell(s) differ: {string.Join(", ", changed.Take(6))}{(changed.Count > 6 ? ", …" : "")}";
    }

    // ---- the baseline ----------------------------------------------------------------------------

    private List<string> CompareBaseline(SortedSet<string> found, bool update)
    {
        var path = ShrinkingBaseline.PathOf("variant-matrix.baseline.txt");
        var baseline = ShrinkingBaseline.Read(path);
        var unexplained = found.Where(entry => !baseline.ContainsKey(entry)).ToList();
        var stale = baseline.Keys.Where(entry => !found.Contains(entry)).ToList();

        // The whole list, which an assertion's message cuts short: it is what the baseline's author reads.
        foreach (var entry in unexplained) output.WriteLine($"unexplained: {entry}");
        foreach (var entry in stale) output.WriteLine($"stale: {entry}");

        if (update && stale.Count > 0)
        {
            ShrinkingBaseline.Write(path, baseline.Where(pair => found.Contains(pair.Key)));
            stale.Clear();
        }
        return unexplained.Select(entry => $"unexplained: {entry}").Concat(stale.Select(entry => $"stale: {entry}")).ToList();
    }
}
