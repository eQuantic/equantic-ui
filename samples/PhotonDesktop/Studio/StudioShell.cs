using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.Configuration;

namespace eQuantic.Studio;

/// <summary>
/// eQUANTIC STUDIO — the component gallery that is also the SDK's own acceptance test. Every control
/// the library ships appears here, live, at the size and in the state the inspector says; if a
/// component regresses, the regression is on screen rather than in a diff.
/// <para>
/// Authored in the write-once vocabulary ONLY, so the same tree runs as GPU pixels in this NSWindow
/// and as DOM in the browser. Nothing in this file knows which target it is on.
/// </para>
/// </summary>
public sealed class StudioShell : StatefulComponent
{
    private GallerySection _section;

    /// <summary>
    /// Which section opens first. The configuration comes from the container — appsettings.json,
    /// the environment and the command line, already merged — so `--section sliders` opens there
    /// with nothing parsed here.
    /// </summary>
    /// <param name="library">
    /// The device's own photo library, arriving like any other dependency — the shell registered
    /// whichever one this platform has and this page never learns which. Nullable so a head with
    /// none simply shows the capability as absent instead of failing to construct.
    /// </param>
    public StudioShell(IConfiguration configuration, IPhotoLibrary? library = null)
    {
        _section = Enum.TryParse<GallerySection>(configuration["section"], ignoreCase: true, out var section)
            ? section
            : GallerySection.Buttons;

        if (library?.IsAvailable == true) _demo.OnPickImage = () => PickAsync(library);
    }

    /// <summary>
    /// Asking IS the permission flow. Null covers cancelling and refusing alike, and neither is an
    /// error path — a picker the user closes is an ordinary Tuesday.
    /// </summary>
    private async void PickAsync(IPhotoLibrary library)
    {
        var picked = await library.PickImageAsync();
        if (picked is not null) SetState(() => _demo.PickedImage = picked);
    }
    private ThemeMode _mode = ThemeMode.Light;

    // The inspector drives the Buttons section's live specimen.
    private Variant _variant = Variant.Primary;
    private SizeVariant _size = SizeVariant.Medium;
    private SpecimenState _state = SpecimenState.Default;
    private bool _leadingIcon = true;

    /// <summary>Section-local demo state, so every control on screen is really interactive.</summary>
    private readonly SectionState _demo = new();

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var body = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Stretch };
        body.Add(Sidebar(theme));
        body.Add(new Flexible(Canvas(theme), 1));
        body.Add(Inspector(theme));

        var page = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        // The window has no title bar (WindowChrome.Unified), so the strip its controls sit in
        // arrives as a TOP safe-area inset — the very same node an iPhone's notch drives. The
        // toolbar keeps clear of it and the gallery's own top row becomes the window's.
        page.Add(new SafeArea(Toolbar(theme), SafeEdges.Top));
        page.Add(new Flexible(body, 1));
        page.Add(StatusBar(theme));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, page);
    }

    // ---- Chrome ----------------------------------------------------------------------------

    /// <summary>The window's own toolbar: what you are looking at, and the two global switches.</summary>
    private VisualNode Toolbar(IAppTheme theme)
    {
        var title = new Column(gap: 1);
        title.Add(new Text("Component Gallery", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        title.Add(new Text($"{Gallery.Sections.Length} sections · every control the SDK ships",
            TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var row = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(Wordmark(theme));
        row.Add(new Flexible(title, 1));
        row.Add(new SegmentedControl(["Light", "Dark"], _mode == ThemeMode.Dark ? 1 : 0,
            i => SetState(() => _mode = i == 1 ? ThemeMode.Dark : ThemeMode.Light))
        {
            Size = SizeVariant.Small,
            Stretch = false,
        });
        row.Add(new Button("Run sample", Variant.Primary, SizeVariant.Small)
        {
            Leading = CuratedIcons.Resolve(Icons.Check),
            OnPressed = () => SetState(() => _demo.Toast = "Sample run — 0 regressions"),
        });

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 56,
            Padding = EdgeInsets.Symmetric(Space.S4, 0),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, row);
    }

    private static VisualNode Wordmark(IAppTheme theme)
    {
        var dot = new Box(new BoxStyle
        {
            Width = 10,
            Height = 10,
            Background = theme.Colors(Variant.Success).Base,
            CornerRadius = new CornerRadii(Radius.Xs),
        });

        var centered = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        centered.Add(dot);

        return new Box(new BoxStyle
        {
            Width = 26,
            Height = 26,
            Background = theme.Colors(Variant.Primary).Base,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, centered);
    }

    /// <summary>The section list — the gallery's table of contents, and the app's only navigation.</summary>
    private VisualNode Sidebar(IAppTheme theme)
    {
        var list = new Column(gap: 2) { Width = SizeValue.Fill };
        foreach (var section in Gallery.Sections)
        {
            var current = section == _section;
            var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Height = SizeValue.Fill };
            row.Add(new Icon(Gallery.IconOf(section), IconSize.Sm,
                current ? theme.Colors(Variant.Primary).Base : theme.TextMuted));
            row.Add(new Text(Gallery.NameOf(section), TypeRole.Label,
                current ? theme.TextPrimary : theme.TextSecondary, maxLines: 1));

            var item = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = 34,
                Padding = EdgeInsets.Symmetric(Space.S2, 0),
                Background = current ? theme.Colors(Variant.Primary).Subtle : null,
                CornerRadius = new CornerRadii(Radius.Sm),
                Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
            }, row);

            var captured = section;
            list.Add(new Pressable(item, () => SetState(() => _section = captured))
            {
                Label = Gallery.NameOf(section),
                Selected = current,
                PressedBackground = theme.SurfaceSubtle,
            });
        }

        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        column.Add(Eyebrow("Components", theme));
        column.Add(list);
        column.Add(new Spacer(1));
        column.Add(new Divider());
        column.Add(Account(theme));

        return new Box(new BoxStyle
        {
            Width = 208,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, column);
    }

    private static VisualNode Account(IAppTheme theme)
    {
        var text = new Column(gap: 0);
        text.Add(new Text("Ana Beatriz", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        text.Add(new Text("SDK maintainer", TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        row.Add(new Avatar("AB", SizeVariant.Small, "Ana Beatriz"));
        row.Add(text);
        return row;
    }

    /// <summary>The bottom rail: what this screen is FOR, stated where a build target belongs.</summary>
    private VisualNode StatusBar(IAppTheme theme)
    {
        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Icon(Icons.Info, IconSize.Sm, theme.TextMuted));
        row.Add(new Text("Build target: every control below is the shipped component, not a mock.",
            TypeRole.Caption, theme.TextMuted, maxLines: 1));
        row.Add(new Spacer(1));
        row.Add(new Text($"{Gallery.NameOf(_section)} · {Gallery.CoverageOf(_section)} components",
            TypeRole.Caption, theme.TextSecondary, maxLines: 1) { Tabular = true });

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 32,
            Padding = EdgeInsets.Symmetric(Space.S4, 0),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, row);
    }

    // ---- Canvas ----------------------------------------------------------------------------

    private VisualNode Canvas(IAppTheme theme)
    {
        var content = new Column(gap: Space.S5) { Width = SizeValue.Fill };
        content.Add(SectionHeader(theme));
        content.Add(Gallery.Render(_section, theme, _demo, mutation => SetState(mutation), Specimen));

        var scroll = new ScrollView(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S6),
        }, content));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, scroll);
    }

    private VisualNode SectionHeader(IAppTheme theme)
    {
        var column = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        column.Add(new Text(Gallery.NameOf(_section), TypeRole.Title, theme.TextPrimary, maxLines: 1));
        column.Add(new Text(Gallery.BlurbOf(_section), TypeRole.BodyM, theme.TextSecondary, maxLines: 2));
        return column;
    }

    /// <summary>The inspector's live subject — the one Button every control on the right describes.</summary>
    private VisualNode Specimen(IAppTheme theme) =>
        new Button("Continue", _variant, _size)
        {
            Leading = _leadingIcon ? CuratedIcons.Resolve(Icons.Check) : null,
            Loading = _state == SpecimenState.Loading,
            Disabled = _state == SpecimenState.Disabled,
            OnPressed = () => SetState(() => _demo.Presses++),
        };

    // ---- Inspector -------------------------------------------------------------------------

    /// <summary>
    /// The right rail states what the specimen IS and, below it, what the control ladder resolved
    /// for that size — the numbers come from <see cref="Sizing"/> at render time, so the panel is a
    /// live readout of the token layer rather than a table someone has to keep in sync.
    /// </summary>
    private VisualNode Inspector(IAppTheme theme)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        column.Add(InspectorHead(theme));
        column.Add(VariantPicker(theme));
        column.Add(SizePicker(theme));
        column.Add(StatePicker(theme));
        column.Add(TogglesPanel(theme));
        column.Add(LadderReadout(theme));
        column.Add(AccessibilityPanel(theme));

        return new Box(new BoxStyle
        {
            Width = 252,
            Height = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, new ScrollView(column));
    }

    private VisualNode InspectorHead(IAppTheme theme)
    {
        var column = new Column(gap: 2);
        column.Add(new Text("Inspector", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        column.Add(new Text("eQuantic.UI.Components.Button", TypeRole.Caption, theme.TextMuted,
            maxLines: 1) { Mono = true });
        return Panel(theme, column);
    }

    private VisualNode VariantPicker(IAppTheme theme)
    {
        var wrap = new Grid([GridTrack.Flex(), GridTrack.Flex()], gap: Space.S1)
        {
            Width = SizeValue.Fill,
        };
        foreach (var variant in Gallery.InspectableVariants)
        {
            var captured = variant;
            wrap.Add(new Chip(variant.ToString(), ChipKind.Filter, variant == _variant,
                () => SetState(() => _variant = captured)));
        }

        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(Eyebrow("Variant", theme));
        column.Add(wrap);
        return Panel(theme, column);
    }

    private VisualNode SizePicker(IAppTheme theme)
    {
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(Eyebrow("Size", theme));
        column.Add(new SegmentedControl(["S", "M", "L", "XL"], (int)_size,
            i => SetState(() => _size = (SizeVariant)i))
        {
            Size = SizeVariant.Small,
        });
        return Panel(theme, column);
    }

    private VisualNode StatePicker(IAppTheme theme)
    {
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(Eyebrow("State", theme));
        column.Add(new RadioGroup(["Default", "Loading", "Disabled"], (int)_state,
            i => SetState(() => _state = (SpecimenState)i)));
        return Panel(theme, column);
    }

    private VisualNode TogglesPanel(IAppTheme theme)
    {
        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Flexible(new Text("Leading icon", TypeRole.BodyM, theme.TextSecondary, maxLines: 1), 1));
        row.Add(new Switch(_leadingIcon, () => SetState(() => _leadingIcon = !_leadingIcon)));

        var presses = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        presses.Add(new Flexible(new Text("Presses", TypeRole.BodyM, theme.TextSecondary, maxLines: 1), 1));
        presses.Add(new Text(_demo.Presses.ToString(), TypeRole.Label, theme.TextPrimary, maxLines: 1)
        {
            Tabular = true,
        });

        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        column.Add(row);
        column.Add(presses);
        return Panel(theme, column);
    }

    /// <summary>Seven rows, seven <see cref="Sizing"/> calls — the ladder, read live.</summary>
    private VisualNode LadderReadout(IAppTheme theme)
    {
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(Eyebrow("Resolved via Sizing", theme));
        column.Add(Metric(theme, "Height", Sizing.Height(_size)));
        column.Add(Metric(theme, "PaddingX", Sizing.PaddingX(_size)));
        column.Add(Metric(theme, "Gap", Sizing.Gap(_size)));
        column.Add(Metric(theme, "LabelSize", Sizing.LabelSize(_size)));
        column.Add(Metric(theme, "Icon", Sizing.Icon(_size)));
        column.Add(Metric(theme, "Radius", Sizing.Radius(_size)));
        column.Add(Metric(theme, "HitTarget", Sizing.HitTarget(_size)));
        return Panel(theme, column);
    }

    private static VisualNode Metric(IAppTheme theme, string name, float value)
    {
        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Flexible(new Text(name, TypeRole.Caption, theme.TextSecondary, maxLines: 1)
        {
            Mono = true,
        }, 1));
        row.Add(new Text(value.ToString("0.#"), TypeRole.Caption, theme.TextPrimary, maxLines: 1)
        {
            Mono = true,
            Tabular = true,
        });
        return row;
    }

    private VisualNode AccessibilityPanel(IAppTheme theme)
    {
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(Eyebrow("Accessibility", theme));
        column.Add(Metric(theme, "role", 0));
        column.Add(new Text(_state == SpecimenState.Loading
            ? "button · disabled while work is in flight"
            : _state == SpecimenState.Disabled ? "button · disabled" : "button",
            TypeRole.Caption, theme.TextSecondary, maxLines: 2));
        return Panel(theme, column);
    }

    // ---- Small shared pieces ---------------------------------------------------------------

    private static VisualNode Panel(IAppTheme theme, VisualNode child) =>
        new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, child);

    private static VisualNode Eyebrow(string text, IAppTheme theme) =>
        new Text(text.ToUpperInvariant(), TypeRole.Caption, theme.TextMuted, maxLines: 1);
}

/// <summary>The specimen's interaction state — what the inspector's State radio picks.</summary>
public enum SpecimenState
{
    Default = 0,
    Loading = 1,
    Disabled = 2,
}

/// <summary>
/// Live state for the demo controls, held in ONE place so the gallery's sections stay pure
/// functions of it — the same reason a real screen keeps its state above the widgets.
/// </summary>
public sealed class SectionState
{
    public int Presses;
    public string? Toast;

    /// <summary>What the device handed back, and how to ask for it. Null when this head has no
    /// photo library at all — which a desktop always does, but a stripped-down one might not.</summary>
    public ImageData? PickedImage;
    public Action? OnPickImage;

    public string Name = "Ana Beatriz Nogueira";
    public string Email = "";
    public string Query = "";
    public int Account;
    public int Quantity = 2;
    public float Budget = 0.4f;
    public int Filter;
    public bool Notifications = true;
    public bool Wifi;
    public int Choice = 1;
    public bool Agreed;
    public int Tab;
    public int Page;
    public bool DialogOpen;
}
