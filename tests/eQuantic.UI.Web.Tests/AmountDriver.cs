using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Drives a <see cref="MoneyInput"/> or a <see cref="NumberInput"/> as a page does, under one culture: types
/// into its entry, moves the focus in and out, hands it a new value as a parent's rebuild would, and reads
/// what it shows. The format culture is the one asked, and the UI culture the invariant one, so what the
/// field says is the SDK's neutral words with the culture's numbers in them, which is what the browser's
/// twin reads with no catalog installed (amount-inputs.spec.ts).
/// </summary>
public sealed class AmountDriver
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private readonly CultureInfo _culture;
    private readonly Func<object?, UiComponent> _make;
    private readonly List<long?> _handed = [];

    private AmountDriver(CultureInfo culture, Func<AmountDriver, object?, UiComponent> make, object? initial)
    {
        _culture = culture;
        _make = value => make(this, value);
        Component = _make(initial);
    }

    public UiComponent Component { get; }

    /// <summary>Every value the field handed on, in order.</summary>
    public IReadOnlyList<long?> Handed => _handed;

    public static AmountDriver Money(string culture, long? amount, Currency currency, long? min = null,
        long? max = null, string? error = null, bool disabled = false) =>
        Money(new CultureInfo(culture), amount, currency, min, max, error, disabled);

    /// <summary>A money field under a culture of the caller's own, the fixture's data on the host's name.</summary>
    public static AmountDriver Money(CultureInfo culture, long? amount, Currency currency, long? min = null,
        long? max = null, string? error = null, bool disabled = false) =>
        new(culture, (driver, value) => new MoneyInput(value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture), handed => driver._handed.Add(handed), "Price", currency,
            min, max, error: error) { Disabled = disabled }, amount);

    public static AmountDriver Number(string culture, int? value, string unit, int? min = null, int? max = null) =>
        Number(new CultureInfo(culture), value, unit, min, max);

    /// <summary>A number field under a culture of the caller's own.</summary>
    public static AmountDriver Number(CultureInfo culture, int? value, string unit, int? min = null, int? max = null) =>
        new(culture, (driver, number) => new NumberInput(number is null ? null : Convert.ToInt32(number, CultureInfo.InvariantCulture), handed => driver._handed.Add(handed), "Price", unit,
            min, max), value);

    /// <summary>Types <paramref name="text"/> into the field, taking the focus first as a person does, and hands
    /// the field back what it handed on, as a page that keeps the value does on its next build.</summary>
    public void Type(string text)
    {
        if (!_focused) Focus(true);
        var before = _handed.Count;
        Under(_culture, () => Entry().OnChanged!(text));
        if (_handed.Count > before) Adopt(_handed[^1]);
    }

    /// <summary>Moves the focus into the field or out of it. A browser tells a field nothing of a focus it
    /// did not have, so neither does this.</summary>
    public void Focus(bool focused)
    {
        if (focused == _focused) return;
        _focused = focused;
        Under(_culture, () => Entry().OnFocusChanged!(focused));
    }

    private bool _focused;

    /// <summary>A parent's rebuild that hands the field <paramref name="value"/>.</summary>
    public void Adopt(object? value) => Under(_culture, () => Component.AdoptConfig(_make(value)));

    /// <summary>The entry the field builds now.</summary>
    public TextEntry Entry() => Under(_culture, () => Nodes(Component).OfType<TextEntry>().Single());

    public HtmlNode Render() => Under(_culture, () => WebRealizer.Lower(Component, Theme).Render());

    /// <summary>What the page shows: the text, the entry's name, whether it is invalid, the line under it,
    /// and the text drawn before and after the entry.</summary>
    public Observation Observe()
    {
        var tree = Render();
        var input = Walk(tree).Single(n => n.Tag == "input");
        var (row, at) = Around(tree);
        string? Beside(int index) => index >= 0 && index < row.Children.Count ? TextOf(row.Children[index]) : null;
        var caption = Walk(tree).Single(n => n.Attributes.ContainsKey("aria-live"));
        return new Observation(
            input.Attributes.GetValueOrDefault("value") ?? "",
            input.Attributes.GetValueOrDefault("aria-label"),
            input.Attributes.GetValueOrDefault("aria-invalid") == "true",
            TextOf(caption) ?? "",
            Beside(at - 1),
            Beside(at + 1));
    }

    /// <summary>The row the entry sits in, and which of its children holds the entry: the nearest ancestor
    /// of the field's shell with more than one child, since the entry is wrapped for its flex.</summary>
    public static (HtmlNode Row, int At) Around(HtmlNode tree)
    {
        var path = new List<HtmlNode>();
        bool Find(HtmlNode node)
        {
            path.Add(node);
            if (node.Attributes.GetValueOrDefault("class") == "eq-field" || node.Children.Any(Find)) return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
        if (!Find(tree)) throw new InvalidOperationException("no entry in the tree");
        for (var i = path.Count - 2; i >= 0; i--)
            if (path[i].Children.Count > 1) return (path[i], path[i].Children.IndexOf(path[i + 1]));
        throw new InvalidOperationException("the entry sits alone");
    }

    public static T Under<T>(string culture, Func<T> body) => Under(new CultureInfo(culture), body);

    public static T Under<T>(CultureInfo asked, Func<T> body)
    {
        var previousFormat = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = asked;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            return body();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static void Under(CultureInfo culture, Action body) => Under(culture, () =>
    {
        body();
        return 0;
    });

    public static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>The text an element holds, its first non-empty run, or null for none.</summary>
    private static string? TextOf(HtmlNode node) =>
        Walk(node).Where(n => n.Tag == "#text").Select(n => n.TextContent).FirstOrDefault(text => !string.IsNullOrEmpty(text));

    private static IEnumerable<VisualNode> Nodes(VisualNode node)
    {
        yield return node;
        var children = node switch
        {
            FlexNode flex => flex.Children,
            Box box => box.Child is { } only ? [only] : [],
            Flexible flexible => [flexible.Child],
            LiveRegion region => [region.Child],
            UiComponent component => [component.BuildContainedAlone(new ComponentContext(Theme))],
            _ => Array.Empty<VisualNode>(),
        };
        foreach (var child in children)
            foreach (var descendant in Nodes(child))
                yield return descendant;
    }

    /// <summary>What a page shows of the field.</summary>
    public sealed record Observation(string Value, string? Name, bool Invalid, string Caption, string? Prefix, string? Suffix);
}
