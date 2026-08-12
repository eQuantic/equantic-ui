using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>One language a <see cref="CultureSwitcher"/> offers: the BCP-47 name the framework
/// switches to, and what a human sees. The label is the language's OWN endonym by convention
/// ("Português", not "Portuguese") — a reader looking for their language does not read the one
/// they cannot.</summary>
public sealed record CultureOption(string Name, string Label);

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

        if (Options.Count <= 3)
        {
            var labels = new List<string>();
            foreach (var option in Options) labels.Add(option.Label);
            return new SegmentedControl(labels, selected, index => Switch(controller, index))
            {
                Size = Size,
                Stretch = false,
            };
        }

        var items = new List<MenuItem>();
        foreach (var option in Options) items.Add(new MenuItem(option.Label));
        return new Menu(
            new Button(Options[selected].Label, Variant.Outline, Size)
            {
                Leading = CuratedIcons.Resolve(Icons.ChevronDown),
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
