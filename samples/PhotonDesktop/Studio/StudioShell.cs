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
    public StudioShell(IConfiguration configuration, IPhotoLibrary? library = null,
        IBiometrics? biometrics = null, INetworkStatus? network = null, IMotionSensor? motion = null,
        ILocation? location = null, ICamera? camera = null, IThemeController? themeSwitch = null,
        ITextClipboard? clipboard = null)
    {
        _clipboard = clipboard;
        // The light/dark hand arrives like any capability. Seed from whatever the host opened
        // with, so the toggle never starts out of step with the window.
        _themeSwitch = themeSwitch;
        _mode = themeSwitch?.Mode ?? ThemeMode.Light;

        if (camera?.IsAvailable == true)
        {
            _demo.OnToggleCamera = () => ToggleCameraAsync(camera);
            _demo.OnCapture = () => SetState(() => _demo.Captured = _demo.CameraSession?.Capture());
        }

        if (location?.IsAvailable == true)
            _demo.OnLocate = () => LocateAsync(location);

        // D5 on a desk: IsAvailable false IS the feature. The section states it instead of
        // pretending, and the same page on a phone streams readings with no new line here.
        _demo.MotionAvailable = motion?.IsAvailable == true;
        if (motion?.IsAvailable == true)
            motion.Subscribe(reading => SetState(() => _demo.Motion = reading));

        _section = Enum.TryParse<GallerySection>(configuration["section"], ignoreCase: true, out var section)
            ? section
            : GallerySection.Buttons;

        if (library?.IsAvailable == true) _demo.OnPickImage = () => PickAsync(library);

        // ABSENT is a first-class answer: a device with no reader (or nothing enrolled) leaves this
        // null, and the section says so rather than offering a button that cannot work.
        if (biometrics?.IsAvailable == true) _demo.OnAuthenticate = () => AuthenticateAsync(biometrics);

        // D4 — the first capability whose answer does not hold still. `Current` seeds the page and
        // every CHANGE flows into SetState: turn wifi off and the chip flips, with this page never
        // having learned which platform's monitor said so. The subscription is never disposed here
        // because this component IS the app's lifetime; a page with an end would keep the
        // IDisposable and dispose it there.
        if (network is not null)
        {
            _demo.Network = network.Current;
            network.Subscribe(state => SetState(() => _demo.Network = state));
        }
    }

    /// <summary>
    /// Four ways for this not to succeed, and they are not the same: a wrong finger invites another
    /// try, a device with nothing enrolled needs Settings, and someone who backed out asked to be
    /// left alone. An app that collapses them into "failed" either nags or gives up.
    /// </summary>
    private async void AuthenticateAsync(IBiometrics biometrics)
    {
        var result = await biometrics.AuthenticateAsync("Confirm it's you to reveal the key.");
        SetState(() => _demo.Authentication = result switch
        {
            BiometricResult.Succeeded => "Confirmed — sk_live_4f8a…c21b",
            BiometricResult.Cancelled => "Cancelled. Nothing was revealed.",
            BiometricResult.FallbackRequested => "Use your password instead.",
            BiometricResult.NotEnrolled => "No fingerprint enrolled — add one in Settings.",
            BiometricResult.Unavailable => "This device has no reader.",
            _ => "Not recognised. Try again.",
        });
    }

    /// <summary>
    /// D6: asking IS the permission flow — the system prompt appears on the first call, and what
    /// the user decides shows up as the PermissionState the FOLLOW-UP explains. Denied gets "how
    /// to change it", never a second prompt.
    /// </summary>
    private async void LocateAsync(ILocation location)
    {
        SetState(() => _demo.Position = "Asking…");
        var fix = await location.GetCurrentAsync();
        SetState(() => _demo.Position = fix is { } position
            ? $"{position.Latitude:0.0000}, {position.Longitude:0.0000} · ±{position.AccuracyMeters:0} m"
            : location.Permission switch
            {
                PermissionState.Denied =>
                    "Denied — allow it in System Settings › Privacy › Location Services.",
                PermissionState.NotDetermined => "No answer — the prompt was dismissed.",
                _ => "Granted, but no fix arrived. Is Location Services on?",
            });
    }

    /// <summary>
    /// D7: start prompts (asking IS the permission flow); stop disposes, which is what turns the
    /// camera light off — the user's own evidence the feed ended.
    /// </summary>
    private async void ToggleCameraAsync(ICamera camera)
    {
        if (_demo.CameraSession is { } running)
        {
            running.Dispose();
            SetState(() => _demo.CameraSession = null);
            return;
        }
        var session = await camera.StartPreviewAsync();
        SetState(() => _demo.CameraSession = session);
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
    private readonly IThemeController? _themeSwitch;
    private readonly ITextClipboard? _clipboard;

    // The inspector drives the Buttons section's live specimen. Loading and Disabled are
    // INDEPENDENT switches, exactly as the handoff draws them — a control can be both.
    private Variant _variant = Variant.Primary;
    private SizeVariant _size = SizeVariant.Medium;
    private bool _loading;
    private bool _disabled;
    private bool _leadingIcon = true;

    // The header's back/forward arrows walk REAL section history.
    private readonly List<GallerySection> _past = new();
    private readonly List<GallerySection> _ahead = new();

    // ⌘K — the section search overlay.
    private bool _searchOpen;
    private string _searchQuery = "";

    /// <summary>Section-local demo state, so every control on screen is really interactive.</summary>
    private readonly SectionState _demo = new();

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // Handoff structure: the sidebar runs FULL height on the left; the right column stacks
        // the breadcrumb bar over (canvas | inspector); the status rail closes the window.
        var content = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Stretch };
        content.Add(new Flexible(Canvas(theme), 1));
        content.Add(Inspector(theme));

        var main = new Column(gap: 0) { Height = SizeValue.Fill };
        main.Add(BreadcrumbBar(theme));
        main.Add(new Flexible(content, 1));
        // The status rail lives INSIDE the right column — the sidebar runs to the window's
        // bottom edge, exactly as the handoff draws it.
        main.Add(StatusBar(theme));

        var body = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Stretch };
        body.Add(Sidebar(theme));
        body.Add(new Flexible(main, 1));

        var page = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        // The toolbar IS the title bar: WindowChrome.Unified hides the system strip and the
        // traffic lights float over the app's own top row — the toolbar reserves their corner
        // instead of sitting below them (two stacked bars was exactly the unfaithful part).
        page.Add(Toolbar(theme));
        page.Add(new Flexible(body, 1));

        VisualNode shell = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, page);

        // ⌘K anywhere opens the section search; the overlay itself closes on Escape (Dialog).
        shell = new Shortcut(shell, KeyChord.Command("k"),
            () => SetState(() => { _searchOpen = true; _searchQuery = ""; }));
        if (!_searchOpen) return shell;
        var layers = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        layers.Add(shell);
        layers.Add(SearchOverlay(theme));
        return layers;
    }

    /// <summary>Section navigation that the header's arrows can WALK — a jump records history.</summary>
    private void Navigate(GallerySection section)
    {
        if (section == _section) return;
        SetState(() =>
        {
            _past.Add(_section);
            _ahead.Clear();
            _section = section;
        });
    }

    // ---- Chrome ----------------------------------------------------------------------------

    /// <summary>The window's own toolbar, as the handoff draws it: back/forward over REAL section
    /// history, the app's name dead centre, and the two actions on the right.</summary>
    private VisualNode Toolbar(IAppTheme theme)
    {
        var title = new Column(gap: 0) { Cross = CrossAlign.Center };
        title.Add(new Text("Component Gallery", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        title.Add(new Text("eQuantic.UI · Photon tokens", TypeRole.Caption, theme.TextMuted,
            maxLines: 1));

        // Height = Fill is what gives Cross.Center a middle to centre ON — a box does not centre
        // its child (block flow starts at the top, the CSS twin), the row centres its items.
        var row = new Row(gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
        };
        // The corner the SYSTEM owns: close/minimize/zoom float here (the window has no bar of
        // its own), and the navigation arrows sit immediately to their right, per the handoff.
        row.Add(Spacer.Fixed(74));
        row.Add(new IconButton(Icons.ChevronLeft, "Back")
        {
            Size = SizeVariant.Small,
            Disabled = _past.Count == 0,
            OnPressed = _past.Count == 0 ? null : () => SetState(() =>
            {
                _ahead.Add(_section);
                _section = _past[^1];
                _past.RemoveAt(_past.Count - 1);
            }),
        });
        row.Add(new IconButton(Icons.ChevronRight, "Forward")
        {
            Size = SizeVariant.Small,
            Disabled = _ahead.Count == 0,
            OnPressed = _ahead.Count == 0 ? null : () => SetState(() =>
            {
                _past.Add(_section);
                _section = _ahead[^1];
                _ahead.RemoveAt(_ahead.Count - 1);
            }),
        });
        row.Add(new Flexible(title, 1));
        row.Add(new IconButton(Icons.Refresh, "Re-render", IconButtonKind.Tonal)
        {
            Size = SizeVariant.Small,
            OnPressed = () => SetState(() => { }),
        });
        row.Add(new Button("Run sample", Variant.Primary, SizeVariant.Small)
        {
            Leading = CuratedIcons.Resolve(Icons.Play),
            OnPressed = () => SetState(() => _demo.Toast = "Sample run — 0 regressions"),
        });

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 44,
            Padding = EdgeInsets.Symmetric(Space.S3, 0),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, row);
    }

    /// <summary>Breadcrumb + the two per-page switches, exactly the handoff's second bar.</summary>
    private VisualNode BreadcrumbBar(IAppTheme theme)
    {
        var row = new Row(gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
        };
        row.Add(new Text("Components", TypeRole.Caption, theme.TextMuted, maxLines: 1));
        row.Add(new Icon(Icons.ChevronRight, IconSize.Sm, theme.TextMuted));
        row.Add(new Text(Gallery.NameOf(_section), TypeRole.Label, theme.TextPrimary, maxLines: 1));
        row.Add(new Spacer(1));
        row.Add(new SegmentedControl(["Light", "Dark"], _mode == ThemeMode.Dark ? 1 : 0,
            i => SetState(() =>
            {
                _mode = i == 1 ? ThemeMode.Dark : ThemeMode.Light;
                // The switch is the HOST's: the window repaints this same tree against the other
                // palette (the browser flips color-scheme). Without a controller the toggle is a
                // label of what it would do.
                _themeSwitch?.Apply(_mode);
            }))
        {
            Size = SizeVariant.Small,
            Stretch = false,
        });
        row.Add(new Button("Copy C#", Variant.Outline, SizeVariant.Small)
        {
            Leading = CuratedIcons.Resolve(Icons.Copy),
            OnPressed = _clipboard is null ? null : () => SetState(() =>
            {
                _clipboard.Write(SpecimenCSharp());
                _demo.Toast = "C# copied to the clipboard";
            }),
        });

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 42,
            Padding = EdgeInsets.Symmetric(Space.S4, 0),
            Background = theme.Background,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, row);
    }

    /// <summary>The section list — grouped exactly as the handoff groups it, with the ⌘K search
    /// pill on top and the maintainer at the bottom.</summary>
    private VisualNode Sidebar(IAppTheme theme)
    {
        var column = new Column(gap: 2) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        column.Add(SearchPill(theme));

        string? group = null;
        foreach (var section in Gallery.Sections)
        {
            if (Gallery.GroupOf(section) != group)
            {
                group = Gallery.GroupOf(section);
                column.Add(new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Padding = new EdgeInsets(Space.S2, Space.S3, Space.S2, Space.S1),
                }, Eyebrow(group, theme)));
            }

            var current = section == _section;
            var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Height = SizeValue.Fill };
            row.Add(new Icon(Gallery.IconOf(section), IconSize.Sm,
                current ? theme.Colors(Variant.Primary).OnSubtle : theme.TextMuted));
            row.Add(new Text(Gallery.NameOf(section), TypeRole.Label,
                current ? theme.Colors(Variant.Primary).OnSubtle : theme.TextSecondary, maxLines: 1));

            var item = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = 28,
                Padding = EdgeInsets.Symmetric(Space.S2, 0),
                Background = current ? theme.Colors(Variant.Primary).Subtle : null,
                CornerRadius = new CornerRadii(Radius.Sm),
                Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
            }, row);

            var captured = section;
            column.Add(new Pressable(item, () => Navigate(captured))
            {
                Label = Gallery.NameOf(section),
                Selected = current,
                PressedBackground = theme.Surface,
            });
        }

        column.Add(new Spacer(1));
        column.Add(new Divider());
        column.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(0, Space.S2, 0, 0),
        }, Account(theme)));

        return new Box(new BoxStyle
        {
            Width = 214,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.SurfaceSubtle,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, column);
    }

    /// <summary>The search affordance — a pill that opens the ⌘K overlay, stating its shortcut.</summary>
    private VisualNode SearchPill(IAppTheme theme)
    {
        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Height = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Icon(Icons.Search, IconSize.Sm, theme.TextMuted));
        row.Add(new Flexible(new Text("Search components", TypeRole.Caption, theme.TextMuted, maxLines: 1), 1));
        row.Add(new Text("⌘K", TypeRole.Caption, theme.TextMuted, maxLines: 1) { Mono = true });

        var pill = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 30,
            Padding = EdgeInsets.Symmetric(Space.S2, 0),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, row);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(0, 0, 0, Space.S2),
        }, new Pressable(pill, () => SetState(() => { _searchOpen = true; _searchQuery = ""; }))
        {
            Label = "Search components",
        });
    }

    /// <summary>⌘K: a dialog listing the sections that match, Enter-or-click to jump.</summary>
    private VisualNode SearchOverlay(IAppTheme theme)
    {
        var list = new Column(gap: 2) { Width = SizeValue.Fill };
        var matches = Gallery.Sections
            .Where(section => Gallery.NameOf(section)
                .Contains(_searchQuery.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var section in matches)
        {
            var captured = section;
            var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Height = SizeValue.Fill };
            row.Add(new Icon(Gallery.IconOf(section), IconSize.Sm, theme.TextMuted));
            row.Add(new Flexible(new Text(Gallery.NameOf(section), TypeRole.BodyM, theme.TextPrimary,
                maxLines: 1), 1));
            row.Add(new Text(Gallery.GroupOf(section), TypeRole.Caption, theme.TextMuted, maxLines: 1));
            list.Add(new Pressable(new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = 36,
                Padding = EdgeInsets.Symmetric(Space.S3, 0),
                CornerRadius = new CornerRadii(Radius.Sm),
            }, row), () => { SetState(() => _searchOpen = false); Navigate(captured); })
            {
                Label = Gallery.NameOf(section),
                PressedBackground = theme.SurfaceSubtle,
            });
        }

        var body = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        body.Add(new TextInput(_searchQuery, value => SetState(() => _searchQuery = value),
            placeholder: "Jump to a section…", size: SizeVariant.Medium)
        {
            Autofocus = true,
        });
        body.Add(matches.Length == 0
            ? new Text("Nothing matches.", TypeRole.Caption, theme.TextMuted, maxLines: 1)
            : list);

        // The Dialog component carries TEXT; this palette needs a live field, so it builds the
        // same layer by hand — scrim under, card over, Presence in, Escape out.
        void Close() => SetState(() => _searchOpen = false);
        var scrim = new Pressable(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Scrim,
        }), Close) { Label = "dismiss" };

        var card = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MaxWidth = 420,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.ExtraLarge)),
            Elevation = 5,
            Padding = EdgeInsets.All(Space.S4),
        }, body);
        var centering = new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
            Padding = EdgeInsets.Symmetric(Space.S6, 0),
        };
        centering.Add(card);

        var layersStack = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        layersStack.Add(scrim);
        layersStack.Add(centering);
        VisualNode layer = new Shortcut(new Overlay(new Presence(layersStack)), KeyChord.Escape, Close);
        // Enter takes the top match — the reason the list is ordered and filtered here.
        return matches.Length > 0
            ? new Shortcut(layer, KeyChord.Enter, () => { Close(); Navigate(matches[0]); })
            : layer;
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

    /// <summary>The bottom rail, monospaced like the handoff: the honest numbers, and a live dot.</summary>
    private VisualNode StatusBar(IAppTheme theme)
    {
        var row = new Row(gap: Space.S4)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
        };
        row.Add(new Text($"{Gallery.TotalCoverage()} components", TypeRole.Caption, theme.TextMuted,
            maxLines: 1) { Mono = true, Tabular = true });
        row.Add(new Text("Photon tokens", TypeRole.Caption, theme.TextMuted, maxLines: 1) { Mono = true });
        row.Add(new Text("light + dark verified", TypeRole.Caption, theme.TextMuted, maxLines: 1)
        {
            Mono = true,
        });
        row.Add(new Spacer(1));
        var live = new Row(gap: Space.S1) { Cross = CrossAlign.Center };
        live.Add(new Box(new BoxStyle
        {
            Width = 7,
            Height = 7,
            Background = theme.Colors(Variant.Success).Base,
            CornerRadius = new CornerRadii(Radius.Full),
        }));
        live.Add(new Text("renderer live", TypeRole.Caption, theme.Colors(Variant.Success).OnSubtle,
            maxLines: 1) { Mono = true });
        row.Add(live);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 28,
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
        content.Add(Gallery.Render(_section, theme, _demo, mutation => SetState(mutation), Specimen,
            SpecimenCSharp()));

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
            Leading = _leadingIcon ? CuratedIcons.Resolve(Icons.Play) : null,
            Loading = _loading,
            Disabled = _disabled,
            OnPressed = () => SetState(() => _demo.Presses++),
        };

    /// <summary>The C# that BUILDS the specimen — what "Copy C#" writes and the code card shows.
    /// The comment lines quote the ladder the same way the inspector reads it: from Sizing.</summary>
    private string SpecimenCSharp()
    {
        var arguments = $"\"Continue\", Variant.{_variant}, SizeVariant.{_size}";
        var inits = "";
        if (_leadingIcon) inits += "\n    Leading = CuratedIcons.Resolve(Icons.Play),";
        if (_loading) inits += "\n    Loading = true,";
        if (_disabled) inits += "\n    Disabled = true,";
        var body = inits.Length == 0
            ? $"new Button({arguments});"
            : $"new Button({arguments})\n{{{inits}\n}};";
        return body
            + "\n\n// resolved through Sizing \u2014 never inlined:"
            + $"\n// Sizing.Height({_size}) = {Sizing.Height(_size):0.#}   Sizing.PaddingX = {Sizing.PaddingX(_size):0.#}"
            + $"\n// Sizing.Icon = {Sizing.Icon(_size):0.#}   Sizing.HitTarget = {Sizing.HitTarget(_size):0.#}";
    }

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
        var wrap = new Row(gap: Space.S1) { Width = SizeValue.Fill, Wrap = true };
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

    /// <summary>Three INDEPENDENT switches, as the handoff draws them — loading and disabled
    /// are not exclusive states of one radio.</summary>
    private VisualNode StatePicker(IAppTheme theme)
    {
        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        column.Add(Eyebrow("State", theme));
        column.Add(StateSwitch(theme, "Leading icon", _leadingIcon,
            () => SetState(() => _leadingIcon = !_leadingIcon)));
        column.Add(StateSwitch(theme, "Loading", _loading,
            () => SetState(() => _loading = !_loading)));
        column.Add(StateSwitch(theme, "Disabled", _disabled,
            () => SetState(() => _disabled = !_disabled)));
        return Panel(theme, column);
    }

    private static VisualNode StateSwitch(IAppTheme theme, string label, bool value, Action toggle)
    {
        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Switch(value, toggle));
        row.Add(new Text(label, TypeRole.BodyM, theme.TextPrimary, maxLines: 1));
        return row;
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
        column.Add(new Divider());
        column.Add(new Text("press \u2192 Motion.Press", TypeRole.Caption, theme.TextMuted, maxLines: 1)
        {
            Mono = true,
        });
        column.Add(new Text("state \u2192 Motion.State", TypeRole.Caption, theme.TextMuted, maxLines: 1)
        {
            Mono = true,
        });
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
        var note = _loading ? "announces \"busy\" while loading"
            : _disabled ? "announced dimmed, not focusable"
            : "activation on Space / Enter / double-tap";
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(Eyebrow("Accessibility", theme));
        column.Add(new Text($"Role button \u00b7 name \"Continue\" \u00b7 {note}",
            TypeRole.Caption, theme.TextSecondary, maxLines: 3));
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

    /// <summary>How the last attempt at the reader ended, in words the user can act on.</summary>
    public string? Authentication;
    public Action? OnAuthenticate;

    /// <summary>The network as of the last change — null when this head offers no monitor.</summary>
    public NetworkState? Network;

    /// <summary>D5: whether this device can feel itself move, and the latest sample if it can.</summary>
    public bool MotionAvailable;
    public MotionReading? Motion;

    /// <summary>D6: what asking for the position came to, in words the user can act on.</summary>
    public string? Position;
    public Action? OnLocate;

    /// <summary>D7: the running feed (null = stopped), the last still, and the two controls.</summary>
    public ICameraSession? CameraSession;
    public ImageData? Captured;
    public Action? OnToggleCamera;
    public Action? OnCapture;

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
