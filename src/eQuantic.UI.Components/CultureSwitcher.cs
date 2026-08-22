using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>One language a <see cref="CultureSwitcher"/> offers: the BCP-47 name the framework
/// switches to, and what a human sees. The label is the language's OWN endonym by convention
/// ("Português", not "Portuguese") — a reader looking for their language does not read the one
/// they cannot.</summary>
public sealed record CultureOption(string Name, string Label)
{
    /// <summary>Two or three letters for a trigger that has no room for a name — <c>PT</c>. The
    /// label is what the OPEN menu says; this is what the closed control says.</summary>
    public string? Short { get; init; }

    /// <summary>A flag emoji, when the design uses one. Text, not an image: it is the one glyph
    /// every target already draws, and it needs no asset pipeline to reach a Photon window.
    /// <para>
    /// A flag is a COUNTRY and a language is not, which is why it never appears alone here — it
    /// rides beside the endonym rather than replacing it. Spanish is not Spain to most of the
    /// people who read it.
    /// </para></summary>
    public string? Flag { get; init; }
}

/// <summary>How a <see cref="CultureSwitcher"/> presents itself.</summary>
public enum CultureSwitcherShape
{
    /// <summary>Segments up to three languages, a menu beyond — the component's own judgement.</summary>
    Auto,

    /// <summary>Always a segmented control: every language visible, one tap away.</summary>
    Segments,

    /// <summary>
    /// Always a menu. What a crowded strip needs: three segments cost the width of three language
    /// NAMES, and a header that fits them in English does not fit them in Portuguese — which is a
    /// layout that breaks on the translation rather than on the design.
    /// </summary>
    Menu,
}

/// <summary>
/// The language switch, write-once (Track L D6). It asks the host for
/// <see cref="ICultureController"/> — the same door the theme toggle uses — so the app never
/// touches JavaScript and never learns which target answered: on the web the catalog swaps and the
/// page RE-RENDERS with no reload (and the server is told for the next request); in a Photon window
/// the process statics change and the window repaints.
/// <para>
/// Presentational choice with a reason: a segmented control for two or three languages (the whole
/// set is visible, one tap away), a menu beyond that (a segmented control with eight segments is a
/// scrollbar). The component decides so every app does not.
/// </para>
/// <para>
/// v1 fence, documented rather than hidden: switching does NOT re-run <c>IHandleMetadata</c>, so
/// the SSR'd &lt;title&gt; and meta keep the landing culture until the next navigation.
/// </para>
/// </summary>
public sealed class CultureSwitcher : StatelessComponent
{
    public CultureSwitcher(IReadOnlyList<CultureOption> options)
    {
        Options = options;
    }

    public IReadOnlyList<CultureOption> Options { get; init; }

    /// <summary>Control size — the switch usually lives in a toolbar beside the theme toggle.</summary>
    public SizeVariant Size { get; init; } = SizeVariant.Small;

    /// <summary>Segments, a menu, or the component's own judgement. See
    /// <see cref="CultureSwitcherShape"/> for why an app overrides it.</summary>
    public CultureSwitcherShape Shape { get; init; } = CultureSwitcherShape.Auto;

    /// <summary>
    /// The glyph on the menu trigger — a globe, by convention.
    /// <para>
    /// The APP's, not a curated one: the §07 set has no globe, and widening a deliberately small
    /// whitelist so one component can draw one picture is the wrong trade. An app already has an
    /// icon pack; this takes whatever it hands over, and draws nothing when it hands over nothing.
    /// </para>
    /// </summary>
    public IconGlyph? Icon { get; init; }

    /// <summary>Told after a switch, for an app that wants to react (a banner, an analytics
    /// event). The switch itself is already done by then.</summary>
    public Action<string>? OnChanged { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        if (Options.Count == 0) return new Box();

        var controller = context.GetService<ICultureController>();
        var current = controller?.UICulture ?? "";

        // The ACTIVE option: exact name first, then the language part — a page served as `pt-BR`
        // must light the `pt` entry a switcher offers, or the reader sees no language selected at
        // all and every switch looks like a no-op.
        var selected = 0;
        for (var i = 0; i < Options.Count; i++)
        {
            if (Options[i].Name == current)
            {
                selected = i;
                break;
            }
            if (LanguageOf(Options[i].Name) == LanguageOf(current)) selected = i;
        }

        if (Shape != CultureSwitcherShape.Menu && (Shape == CultureSwitcherShape.Segments || Options.Count <= 3))
        {
            var labels = new List<string>();
            foreach (var option in Options) labels.Add(option.Label);
            return new SegmentedControl(labels, selected, index => Switch(controller, index))
            {
                Size = Size,
                Stretch = false,
            };
        }

        // The flag rides IN the label rather than in the icon slot: that slot takes a curated
        // Icons value, and a flag is neither curated nor an icon — it is text, which is also the
        // only form of it that reaches a native window with no asset pipeline.
        var items = new List<MenuItem>();
        foreach (var option in Options)
            items.Add(new MenuItem(option.Flag is { Length: > 0 } flag
                ? $"{flag}  {option.Label}"
                : option.Label));

        // A GLOBE, and the short code for a name. The closed control says which language you are
        // in; it does not have to say which three you could be in.
        var chosen = Options[selected];
        return new Menu(
            new Button(chosen.Short is { Length: > 0 } code ? code : chosen.Label, Variant.Ghost, Size)
            {
                Leading = Icon,
            },
            items,
            index => Switch(controller, index));
    }

    private void Switch(ICultureController? controller, int index)
    {
        if (index < 0 || index >= Options.Count) return;
        var option = Options[index];
        controller?.Apply(option.Name, option.Name);
        OnChanged?.Invoke(option.Name);
    }

    /// <summary>The language part of a BCP-47 name (`pt-BR` → `pt`), by string, because the
    /// abstract layer has no CultureInfo on the target that matters most here.</summary>
    private static string LanguageOf(string name)
    {
        var cut = name.IndexOf('-');
        return cut < 0 ? name : name[..cut];
    }
}
