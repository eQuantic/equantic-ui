using System.Linq;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Interaction and motion — the words a reader can do something with, and the ones that move on
/// their own. Three of them (<c>Simulated</c>, <c>InFlow</c>, <c>InView</c>) lower to whatever their
/// child lowers to while changing what that child is built AGAINST, which is why they sit beside the
/// state they set rather than beside elements they never emit.
/// </summary>
internal sealed partial class WebLoweringVisitor
{
    /// <summary>
    /// Draws the subtree in the given states. The web cannot force <c>:hover</c> or
    /// <c>:focus-visible</c> from CSS — nothing can, which is the point of the pseudo-class — so
    /// the diffs those rules carry are folded into the BASE style instead. What the reader sees is
    /// what a real hover would produce, because it is the same declarations.
    /// </summary>
    private HtmlElement? LowerSimulated(Simulated simulated,
        bool? horizontalAxis)
    {
        var previous = _simulated;
        // Nested previews COMBINE rather than replace: a hovered card containing a pressed button
        // is two nodes, and the inner one must not turn the outer's hover off.
        _simulated = previous | simulated.State;
        try
        {
            return Lower(simulated.Child, horizontalAxis);
        }
        finally
        {
            _simulated = previous;
        }
    }

    /// <summary>
    /// Arms the in-flow intent while the child BUILDS. The overlay reads it in its own Build — that
    /// is the only moment the answer can change anything, because by the time a tree exists the
    /// scrim and the layer are already in it.
    /// </summary>
    private HtmlElement? LowerInFlow(InFlow inFlow, bool? horizontalAxis)
    {
        var previous = Primitives.InFlow.Current;
        Primitives.InFlow.Current = true;
        try
        {
            return Lower(inFlow.Child, horizontalAxis);
        }
        finally
        {
            Primitives.InFlow.Current = previous;
        }
    }

    /// <summary>
    /// A layout-transparent wrapper the runtime observes. The declaration rides a stable path the
    /// client picks up after the pass mounts — the same shape ScrollView's viewport reporting uses,
    /// because the answer is not knowable until layout has happened.
    /// </summary>
    private HtmlElement? LowerInView(InView inView, bool? horizontalAxis)
    {
        var child = Lower(inView.Child, horizontalAxis);
        if (child is null) return null;

        var wrapper = new RealizedElement("div")
        {
            // display:contents — the wrapper reports, it does not lay out. A div in the flow would
            // break the grid or flex the child was written to sit in.
            //
            // The OBSERVER marker is stamped by the client, which is the only side that can watch
            // anything; the server emits the same element so the two trees have the same shape and
            // hydration is an attribute diff rather than a replaced subtree (the same split
            // ScrollView's viewport reporting uses).
            Style = new HtmlStyle { Display = Display.Contents },
        };
        wrapper.Children.Add(child);
        return wrapper;
    }
    /// <summary>
    /// Spec S8: the binding is MARKED on the child's root (<c>data-eq-shortcut</c>) and the runtime's
    /// window controller dispatches to it — SSR has no key events, so the marker is the whole
    /// server-side contract (and it keeps the SSR/hydration DOM identical to the TS lowering).
    /// </summary>
    private HtmlElement? LowerShortcut(Shortcut shortcut, bool? horizontalAxis)
    {
        if (Lower(shortcut.Child, horizontalAxis) is not { } child) return null;
        if (child is RealizedElement realized)
        {
            realized.RawAttributes ??= new Dictionary<string, string>();
            // NESTED shortcuts share one child root (a dialog binds Esc, ↑, ↓ and ↵ around the same
            // subtree) — the marker lists them all rather than the outermost overwriting the rest.
            var chord = ChordId(shortcut.Chord);
            realized.RawAttributes["data-eq-shortcut"] =
                realized.RawAttributes.TryGetValue("data-eq-shortcut", out var existing) && existing.Length > 0
                    ? $"{existing} {chord}"
                    : chord;
        }
        return child;
    }

    /// <summary>The chord's wire form — <c>command+shift+k</c>, modifiers in a FIXED order so both
    /// realizers and the runtime controller compare the same string.</summary>
    internal string ChordId(KeyChord chord)
    {
        var parts = new List<string>(4);
        if (chord.Modifiers.HasFlag(KeyModifiers.Command)) parts.Add("command");
        if (chord.Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("control");
        if (chord.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("alt");
        if (chord.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("shift");
        parts.Add(chord.Key.ToLowerInvariant());
        return string.Join("+", parts);
    }
    /// <summary>
    /// The TS twin's SSR half: same markup, no handler — keydown only exists client-side, exactly
    /// as Pressable's click does. One focusable wrapper is the control's whole Tab presence.
    /// </summary>
    private HtmlElement LowerAdjustable(Adjustable adjustable)
    {
        var adjustableFills = Fills(adjustable.Child);
        var adjustableCap = CapsAt(adjustable.Child);
        // ARIA pairs the role with the value, so BOTH halves are derived here rather than copied:
        // the value is the SLIDER role's and no other's, and a slider that has none is not one.
        var adjustableValue = adjustable.Role == AdjustableRole.Slider ? adjustable.Value : null;
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // Same rule as LowerProgress, and it bites harder here: this host is a TAB STOP, so
                // a block div stretched to the container draws the focus ring around empty space
                // beside the control.
                Width = adjustableFills.Width ? "100%" : "fit-content",
                MaxWidth = Size(adjustableCap),
                PointerEvents = "auto",
                // BOTH axes, like every other wrapper and like the twin already did. Taking the
                // width and leaving the height is the same half-contract that made the Link
                // diverge, mirrored: there the twin was short, here this side was.
                Height = adjustableFills.Height ? "100%" : null,
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["role"] = AriaRole(adjustable.Role, adjustableValue),
                ["tabindex"] = "0",
            },
        };
        if (adjustable.Label is { Length: > 0 } label) element.RawAttributes["aria-label"] = label;
        // The VALUE, for a role that has one (spec C7). role="slider" REQUIRES aria-valuenow, so the
        // host emitted invalid ARIA until this existed — a name and no number. The bounds go with it
        // or the number is read against ARIA's own 0-100 default, which no slider here uses.
        if (adjustableValue is { } value)
        {
            element.RawAttributes["aria-valuenow"] = TokenCss.Number(value.Now);
            element.RawAttributes["aria-valuemin"] = TokenCss.Number(value.Min);
            element.RawAttributes["aria-valuemax"] = TokenCss.Number(value.Max);
        }
        // OUTSIDE the block above, which is #243. aria-valuetext REPLACES the number for a reader,
        // so echoing the number into it would trade a value for the same value — but it does not
        // DEPEND on there being one, and reading it from inside the value meant a node with no
        // number had no words either.
        if (adjustable.ValueText is { Length: > 0 } spoken)
            element.RawAttributes["aria-valuetext"] = spoken;
        if (Lower(adjustable.Child, null) is { } child) element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// The web twin of the progress semantics (spec B14): one host carrying
    /// <c>role="progressbar"</c>, its name, and how far along it is. No tab index and no handler —
    /// nothing here is operable, which is the whole difference from <c>LowerAdjustable</c> above.
    /// <para>
    /// An INDETERMINATE bar keeps the role and omits <c>aria-valuenow</c>, which is ARIA's own rule
    /// and reads as "in progress, amount unknown". Note this is the INVERSE of the slider: there a
    /// missing value means the node is not a slider and the role is withheld; here a missing value
    /// is a state the role exists to report. Two rules that look alike and are not, so they are
    /// written out rather than shared.
    /// </para>
    /// <para>SSR half: the same markup either way — there is no handler to leave out.</para>
    /// </summary>
    private HtmlElement LowerProgress(Progress progress)
    {
        var progressFills = Fills(progress.Child);
        var progressCap = CapsAt(progress.Child);
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // FIT-CONTENT, not nothing: this host CARRIES THE ROLE, so its box is the bounds a
                // reader announces and a focus highlight draws. A bare block div stretches to the
                // container while the bar stays its own width, and the two stop describing the same
                // thing — on Photon `MeasureWrapper` gives the wrapper exactly the child's bounds.
                Width = progressFills.Width ? "100%" : "fit-content",
                // The child's cap comes THROUGH: a wrapper that took the width and dropped the
                // maximum is the half-contract that made the Link diverge once already.
                MaxWidth = Size(progressCap),
                Height = progressFills.Height ? "100%" : null,
            },
            RawAttributes = new Dictionary<string, string> { ["role"] = "progressbar" },
        };
        if (progress.Label is { Length: > 0 } label) element.RawAttributes["aria-label"] = label;
        if (progress.Value is { } value)
        {
            element.RawAttributes["aria-valuenow"] = TokenCss.Number(value.Now);
            element.RawAttributes["aria-valuemin"] = TokenCss.Number(value.Min);
            element.RawAttributes["aria-valuemax"] = TokenCss.Number(value.Max);
        }
        // An INDETERMINATE bar has no aria-valuenow and may still have words — and that is the case
        // where they matter most, because there is no number for a reader to fall back on. They used
        // to be read from inside the value, so "Estimating time remaining" was dropped exactly when
        // it was the only thing the bar could say (#243).
        if (progress.ValueText is { Length: > 0 } spoken)
            element.RawAttributes["aria-valuetext"] = spoken;
        if (Lower(progress.Child, null) is { } child) element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// LIVE REGION semantics (TS twin: lowerLiveRegion): one host the platform WATCHES, so a change
    /// inside it is announced wherever the user is, without moving focus.
    /// <para>
    /// <c>role</c> and <c>aria-live</c> are set TOGETHER and neither is redundant. The role is what
    /// a reader reports the region as; the live value is what makes it watched. `role="alert"` does
    /// imply assertive in the ARIA spec, but implementations have historically disagreed about
    /// whether an alert added to the DOM after load is announced at all, and the pairing is what
    /// every practical guide recommends — so this states both rather than relying on the implication.
    /// </para>
    /// <para>
    /// LAYOUT-TRANSPARENT, and the whole width contract passes through: this is the fifth wrapper
    /// that puts an element between parent and child and gives it a width, so it takes `fit-content`
    /// off the fill branch for the reason the other four do — the box a reader outlines is the box
    /// it announces.
    /// </para>
    /// </summary>
    private HtmlElement LowerLiveRegion(LiveRegion live)
    {
        var fills = Fills(live.Child);
        var cap = CapsAt(live.Child);
        var assertive = live.Urgency == LiveRegionUrgency.Assertive;
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : "fit-content",
                MaxWidth = Size(cap),
                Height = fills.Height ? "100%" : null,
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["role"] = assertive ? "alert" : "status",
                ["aria-live"] = assertive ? "assertive" : "polite",
                // The region is announced as a WHOLE when any part of it changes. Without this a
                // reader reads only the changed node, so a banner whose title and body both change
                // is announced as a fragment of itself.
                ["aria-atomic"] = "true",
            },
        };
        if (live.Label is { Length: > 0 } label) element.RawAttributes["aria-label"] = label;
        if (Lower(live.Child, null) is { } child) element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// What the host ANNOUNCES itself as — DERIVED from the role and the value together rather than
    /// copied from <see cref="Adjustable.Role"/>, because the pairing is ARIA's rule and this is the
    /// one place in the SDK that speaks ARIA.
    /// <para>
    /// <c>role="slider"</c> REQUIRES <c>aria-valuenow</c>. A node that asks for the slider role and
    /// carries no value cannot be given it: the combination is invalid, and a reader meeting it
    /// behaves however it likes — some announce a position of zero, some announce none. So a
    /// value-less slider announces <c>group</c>, which is what it actually is: a focusable container
    /// the arrows adjust, with no position of its own. The role the caller asked for is honoured
    /// wherever ARIA allows it to be, and never where it would be a lie.
    /// </para>
    /// <para>
    /// Deriving it HERE is what makes the rule unoutrunnable. The component library is not the only
    /// way an Adjustable is built, and a guard that enumerates components leaves
    /// <c>new Adjustable(child, onAdjust)</c> and <c>UI.Adjustable(...)</c> free to emit the invalid
    /// pair — which is exactly what they did. The TS twin derives it the same way
    /// (<c>lowerAdjustable</c> in lowering.ts).
    /// </para>
    /// </summary>
    private static string AriaRole(AdjustableRole role, RangeValue? value) => role switch
    {
        AdjustableRole.Tablist => "tablist",
        AdjustableRole.Radiogroup => "radiogroup",
        _ => value is null ? "group" : "slider",
    };

    /// <summary>
    /// The 2-D composite (TS twin: lowerNavigable). One focusable host carrying the grid role and
    /// the name, and one ROW per declared row — <c>display:contents</c>, so the row exists for
    /// assistive tech and not for layout: the caller's Grid or Column keeps laying the cells out
    /// exactly as it did. A grid whose cells are not inside rows is an invalid tree, which is why
    /// the rows are structural here rather than left to the caller's markup.
    /// <para>SSR half: the same markup, no handler — keydown only exists client-side, exactly as
    /// Pressable's click does.</para>
    /// </summary>
    private HtmlElement LowerNavigable(Navigable navigable)
    {
        var element = new RealizedElement("div")
        {
            RawAttributes = new Dictionary<string, string>
            {
                ["role"] = navigable.Role switch { _ => "grid" },
                ["tabindex"] = "0",
            },
        };
        if (navigable.Label is { Length: > 0 } gridLabel) element.RawAttributes["aria-label"] = gridLabel;
        // The keyboard, DECLARED here as well as installed by the client twin. The server never
        // dispatches a key, so this delegate does not run — but the lowered tree is what the
        // parity net compares, and a fixture that says "no handler" cannot notice the client
        // losing one. NavigableKeys is the same table the TS half reads, cross-pinned.
        if (navigable.OnMove is { } onMove)
            element.OnKeyDown = key =>
            {
                if (NavigableKeys.Move(key.Key, key.ShiftKey) is { } move) onMove(move);
            };
        // Ids are scoped to THIS grid: two calendars on one page would otherwise both call their
        // cells eq-cell-1-1, which is a duplicate id and an activedescendant that may resolve into
        // the wrong grid. The scope is the grid's own name, hashed with the atomizer's FNV — the
        // one hash both producers compute identically, which is what SSR hydration needs.
        var scope = NavigableScope(navigable);
        if (navigable.ActiveCell is { } active)
            element.RawAttributes["aria-activedescendant"] = NavigableCellId(scope, active.Row, active.Item);

        for (var index = 0; index < navigable.Rows.Count; index++)
        {
            var row = new RealizedElement("div")
            {
                // Transparent to layout: the row is an accessibility fact, not a box.
                Style = new HtmlStyle { Display = Display.Contents },
                RawAttributes = new Dictionary<string, string> { ["role"] = "row" },
            };
            if (Lower(navigable.Rows[index], null) is { } lowered)
            {
                row.Children.Add(lowered);
                if (navigable.HasHeaderRow && index == 0) MarkColumnHeaders(lowered);
            }
            // The cells are IDENTIFIED — an aria-activedescendant pointing at an id nothing
            // carries is a dangling reference, which reads to assistive tech as no focus at all.
            var item = 0;
            if (!(navigable.HasHeaderRow && index == 0)) NumberGridCells(row, scope, index, ref item);
            element.Children.Add(row);
        }
        return element;
    }

    /// <summary>Walks one lowered row and ids every gridcell in tree order — the id the host's
    /// aria-activedescendant points at, built the same way by the TS twin.</summary>
    private void NumberGridCells(HtmlElement element, string scope, int row, ref int item)
    {
        if (element.Role == "gridcell")
        {
            element.Id = NavigableCellId(scope, row, item);
            item++;
        }
        foreach (var child in element.Children)
            if (child is HtmlElement childElement)
                NumberGridCells(childElement, scope, row, ref item);
    }

    /// <summary>The header row's cells NAME their columns (design system C15: the day names).
    /// They are the row content's DIRECT children — whatever the caller laid out, one element per
    /// column — so the rule is structural rather than a guess about what a header looks like.</summary>
    private void MarkColumnHeaders(HtmlElement rowContent)
    {
        foreach (var child in rowContent.Children)
            if (child is HtmlElement cell)
                cell.Role = "columnheader";
    }

    /// <summary>The id a cell answers to for <c>aria-activedescendant</c> — the TS twin builds the
    /// same string, or the attribute points at nothing.</summary>
    internal string NavigableCellId(string scope, int row, int item) =>
        $"eq-cell-{scope}-{row}-{item}";

    /// <summary>What makes one grid's cell ids distinct from another's on the same page: the
    /// grid's NAME, hashed. Two grids that also share a name (the same month rendered twice) would
    /// still collide — a documented limit, and the point where a caller should give them names
    /// that differ, which a screen reader needs anyway.</summary>
    internal string NavigableScope(Navigable navigable) =>
        StyleAtomizer.Hash(navigable.Label);
    /// <summary>
    /// Spec §06 loop motion: a layout-transparent div carrying the GENERATED keyframe animation —
    /// the effect maps to a keyframe name, endpoints ride custom properties at the style tail
    /// (fractions of the element's own width — CSS translateX(%) has the same base as the native
    /// realizer's offset math), duration rides the animation shorthand. `prefers-reduced-motion`
    /// disables it in the generated stylesheet (the .eq-loop class is the hook).
    /// </summary>
    private HtmlElement LowerLoopMotion(LoopMotion motion)
    {
        var element = new RealizedElement("div")
        {
            // Decorative loops additionally hide under prefers-reduced-motion (generated rule).
            ClassName = motion.HideAtRest ? "eq-loop eq-loop-rest-hidden" : "eq-loop",
            Style = new HtmlStyle
            {
                Animation = $"eq-slide-x {motion.DurationMs}ms linear infinite",
                CustomProperties = new Dictionary<string, string>
                {
                    ["--eq-loop-from"] = TokenCss.Percent(motion.FromX),
                    ["--eq-loop-to"] = TokenCss.Percent(motion.ToX),
                },
            },
        };
        if (Lower(motion.Child, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }
    /// <summary>
    /// The cap a FILL child puts on itself, walked the same way <see cref="Fills"/> walks the fill
    /// — 0 when there is none.
    /// <para>
    /// A wrapper stands in for its child in the parent's layout, so it has to carry the whole
    /// width contract and not half of it. Taking the 100% and dropping the max-width made the
    /// wrapper full width with a narrower block inside: the child hugged the start edge, and a row
    /// centring its children had an item already filling the row, which is nothing to centre.
    /// Measured on a real page before it was believed — a card capped at 980 inside a 1392 wrapper.
    /// </para>
    /// </summary>
    private SizeValue CapsAt(VisualNode node) => node switch
    {
        Box box => box.Style.MaxWidth,
        Pressable pressable => CapsAt(pressable.Child),
        Hoverable hoverable => CapsAt(hoverable.Child),
        Adjustable adjustable => CapsAt(adjustable.Child),
        Progress progress => CapsAt(progress.Child),
        LiveRegion live => CapsAt(live.Child),
        Flexible flexible => CapsAt(flexible.Child),
        LoopMotion motion => CapsAt(motion.Child),
        Link link => CapsAt(link.Child),
        // The three <see cref="Fills"/> gained one round ago. Adding them THERE and not here is the
        // half-contract this file already records twice: the host takes the child's 100% and drops
        // its maximum, so a role-bearing one announces a box wider than the bar it names.
        Simulated simulated => CapsAt(simulated.Child),
        InFlow inFlow => CapsAt(inFlow.Child),
        InView inView => CapsAt(inView.Child),
        _ => SizeValue.Hug,
    };
    /// <summary>
    /// Whether a lowered subtree already carries an interactive ELEMENT, in which case a Pressable
    /// around it must not become a second one. HTML forbids a button inside a button (and an anchor
    /// inside an anchor): the parser closes the outer one and hands back an empty shell, so the
    /// wrapper renders as nothing and the hydrated tree disagrees with the served HTML about the
    /// whole subtree.
    /// <para>
    /// FORM CONTROLS count for the same reason, and it is not a style preference: the content model
    /// forbids interactive content inside a <c>button</c>, and a browser handed
    /// <c>button &gt; input</c> resolves it by taking the typing away.
    /// </para>
    /// </summary>
    private bool WrapsAnInteractive(IComponent node) =>
        node is RealizedElement element
        && (element.Tag is "button" or "a" or "input" or "select" or "textarea"
            || element.Children.Any(WrapsAnInteractive));

    private HtmlElement LowerPressable(Pressable pressable)
    {
        var fills = Fills(pressable.Child);
        var cap = CapsAt(pressable.Child);
        var child = Lower(pressable.Child, horizontalAxis: null);

        // A Pressable AROUND a control — a Menu making its trigger open the panel — is ordinary
        // composition, and the trigger is usually a Button. The OUTER one yields: it keeps the
        // press, the child keeps being the real control, and the markup stays legal.
        var wrapping = child is not null && WrapsAnInteractive(child);
        var element = new RealizedElement(wrapping ? "span" : "button")
        {
            // Neutralize UA button chrome — the child carries ALL visuals (same as native).
            Style = new HtmlStyle
            {
                Padding = "0",
                Border = "none",
                Background = "none",
                FontFamily = "inherit",
                Cursor = pressable.Disabled ? null : "pointer",
                TextAlign = TextAlign.Start,
                // A Fill child needs the 100% chain to pass through the button (scrim et al.).
                Width = fills.Width ? "100%" : null,
                MaxWidth = Size(cap),
                // A control is a target by definition, and says so because `none` inherits from
                // any transparent row above it.
                PointerEvents = "auto",
                Height = fills.Height ? "100%" : null,
            },
            Disabled = pressable.Disabled && !wrapping ? true : null,
            AriaLabel = pressable.Label,
            // Selection stated, not merely painted — a fill colour says nothing to a screen reader.
            AriaPressed = pressable.Selected is { } selected ? (selected ? "true" : "false") : null,
            // Disclosure stated the same way: a rotated chevron is paint, aria-expanded is the answer.
            AriaExpanded = pressable.Expanded,
            OnClick = pressable.Disabled ? null : pressable.OnPressed,
        };

        // Interaction states (spec §01): mechanics live in the GENERATED stylesheet — every enabled
        // pressable carries the class (the :focus-visible double ring is an accessibility DEFAULT);
        // the pressed swap additionally needs its token value as a per-element custom property.
        if (!pressable.Disabled)
        {
            // eq-pressed carries the same declaration :active does — see TokenCss. It is added, not
            // substituted, so a simulated control still behaves like the control it is picturing.
            element.ClassName = _simulated.HasFlag(SimulatedState.Pressed)
                ? "eq-pressable eq-pressed"
                : "eq-pressable";
            if (pressable.PressedBackground is { } pressedFill)
            {
                element.Style!.CustomProperties = new Dictionary<string, string>
                {
                    ["--eq-pressed-bg"] = TokenCss.Value(pressedFill),
                };
            }
        }

        // §10 initial focus: the marker the trap controller prefers over the first focusable —
        // static, identical on both producers, so hydration compares equal.
        if (pressable.InitialFocus)
        {
            element.RawAttributes ??= new Dictionary<string, string>();
            element.RawAttributes["data-eq-initial-focus"] = "";
        }

        // A span that acts as a button says so, and takes the keyboard the same way.
        if (wrapping)
        {
            element.Role = "button";
            if (pressable.Disabled) element.AriaDisabled = true;
            else element.TabIndex = 0;
        }

        // One choice of an exclusive set (role: radio): the wrapping Adjustable is the one Tab
        // stop, so the item leaves the tab order (roving) and states its selection as
        // checked-ness, not pressed-ness. TS twin emits the same three attributes.
        if (pressable.Role == PressableRole.Radio)
        {
            element.Role = "radio";
            element.AriaChecked = pressable.Selected == true ? "true" : "false";
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // One tab of a tablist — the radio's twin (roving, the wrapping Adjustable is the one Tab
        // stop), except a tab is PICKED, not checked: aria-selected is its word.
        if (pressable.Role == PressableRole.Tab)
        {
            element.Role = "tab";
            element.AriaSelected = pressable.Selected == true;
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // Panel item rows: out of the Tab order — the keyboard lives on the TRIGGER while the
        // panel is up, and the highlight travels as aria-activedescendant (see LowerAnchored).
        if (pressable.Role == PressableRole.MenuItem)
        {
            element.Role = "menuitem";
            element.AriaPressed = null;
            element.TabIndex = -1;
        }
        if (pressable.Role == PressableRole.Option)
        {
            element.Role = "option";
            element.AriaSelected = pressable.Selected == true;
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // One cell of a 2-D composite: PICKED like a tab or an option (design system C15 asks for
        // gridcell + selected), and out of the Tab order because the enclosing Navigable is the
        // composite's one stop — 42 days must not be 42 tab presses.
        if (pressable.Role == PressableRole.GridCell)
        {
            element.Role = "gridcell";
            element.AriaSelected = pressable.Selected == true;
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // A navigation destination keeps its Tab stop (a nav is a list of links, not a composite)
        // and says WHERE YOU ARE, which is the one thing aria-pressed/selected/checked cannot say.
        // Only the current one carries the attribute: aria-current="false" on the others is legal
        // and pure noise, and every destination announcing "not current" is worse than silence.
        if (pressable.Role == PressableRole.Destination)
        {
            element.AriaPressed = null;
            if (pressable.Selected == true) element.AriaCurrent = "page";
        }

        // A check states its state as an ATTRIBUTE, never in the name — a name that changes when
        // the state does reads as a different control. Unlike a radio it keeps its own Tab stop
        // (the button element already is one): a checkbox is not one choice of a composite.
        if (pressable.Role is PressableRole.Checkbox or PressableRole.Switch)
        {
            element.Role = pressable.Role == PressableRole.Switch ? "switch" : "checkbox";
            // "mixed" exists for checkboxes and nothing else — ARIA's own rule, enforced here so a
            // Switch cannot half-say something the role has no word for.
            element.AriaChecked =
                pressable.Mixed && pressable.Role == PressableRole.Checkbox ? "mixed"
                : pressable.Selected == true ? "true" : "false";
            element.AriaPressed = null;
        }

        if (child is not null) element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// Pointer-presence callback (S5 programmable): a layout-transparent div carrying
    /// mouseenter/mouseleave — the events the low-level element pipeline already hydrates. The
    /// child owns all visuals; display:contents would break the events, so the div participates
    /// as a plain wrapper passing Fill through like Pressable's button does.
    /// </summary>
    private HtmlElement LowerHoverable(Hoverable hoverable)
    {
        var fills = Fills(hoverable.Child);
        var cap = CapsAt(hoverable.Child);
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : null,
                MaxWidth = Size(cap),
                // A control is a target by definition, and says so because `none` inherits from
                // any transparent row above it.
                PointerEvents = "auto",
                Height = fills.Height ? "100%" : null,
            },
            OnMouseEnter = _ => hoverable.OnChanged(true),
            OnMouseLeave = _ => hoverable.OnChanged(false),
        };
        if (Lower(hoverable.Child, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// Navigation semantics: a REAL <c>&lt;a href&gt;</c> (SSR-crawlable, router-intercepted) whose
    /// UA chrome is neutralized — the child owns all visuals, exactly the Pressable contract. A Fill
    /// child gets the 100% pass-through chain the same way.
    /// </summary>
    private HtmlElement LowerLink(Link link)
    {
        var fills = Fills(link.Child);
        var cap = CapsAt(link.Child);
        // The render's link policy — the language prefix, when the app asked for one. Applied HERE
        // so an author writes `/pricing` once and the href is right in every language.
        var destination = RenderContext.ResolveDestination(link.Destination);
        var element = new RealizedElement("a")
        {
            ClassName = "eq-link",
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : null,
                MaxWidth = Size(cap),
                // A control is a target by definition, and says so because `none` inherits from
                // any transparent row above it.
                PointerEvents = "auto",
                Height = fills.Height ? "100%" : null,
            },
            AriaLabel = link.Label,
            RawAttributes = new Dictionary<string, string> { ["href"] = destination },
        };
        // Read by the router when it decides where the new page starts. On the anchor rather than in
        // a side table because the router meets the ANCHOR — a delegated click listener, one for the
        // whole document, which is also what makes server-rendered links work with no wiring.
        if (link.KeepsPosition) element.RawAttributes["data-eq-keep-position"] = "";
        // WARM ON HOVER. The router has always had a prefetch seam and nothing ever marked a link
        // for it, so it was dead code and every navigation paid the module import in full — which is
        // most of what a click costs. The router asks once per link and swallows failures, so the
        // worst case is the work the click was going to do anyway, done slightly earlier.
        //
        // App-internal destinations only: an absolute URL belongs to somebody else's server.
        if (destination.StartsWith('/')) element.RawAttributes["data-prefetch"] = "";
        // The page you are ON. Only the current link carries it — aria-current="false" on the other
        // nine is legal, useless, and read out loud.
        if (link.Current) element.AriaCurrent = "page";
        if (Lower(link.Child, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// Enter motion (spec §06): NO wrapper element — the mount-playing animation class rides the
    /// lowered child's own root, so flex/stack layout is untouched and the reconciler's in-place
    /// patching never restarts the animation on unrelated re-renders (it replays only on a real
    /// re-insertion — exactly the declarative-presence contract).
    /// </summary>
    private HtmlElement? LowerPresence(Presence presence, bool? horizontalAxis)
    {
        if (Lower(presence.Child, horizontalAxis) is not { } child) return null;
        var slide = presence.Enter == PresenceMotion.SlideUp;
        var cls = slide ? "eq-presence-slideup" : "eq-presence-fade";
        child.ClassName = string.IsNullOrEmpty(child.ClassName) ? cls : $"{cls} {child.ClassName}";
        // The EXIT marker: the reconciler defers this element's removal (reparented to body inside
        // its fixed overlay layer) while the reverse animation plays.
        if (child is RealizedElement realized)
        {
            realized.RawAttributes ??= new Dictionary<string, string>();
            realized.RawAttributes["data-eq-exit"] = slide ? "slideup" : "fade";
        }
        return child;
    }

    /// <summary>
    /// A continuous gesture: the child carries its RULES as data, and one document-level controller
    /// in the runtime does the tracking. The rest offset is a plain transform, so a row that is
    /// already open renders open on the server too, before any script has run.
    /// </summary>
    private HtmlElement? LowerDraggable(Draggable draggable,
        bool? horizontalAxis)
    {
        if (Lower(draggable.Child, horizontalAxis) is not RealizedElement child) return null;

        // A drag surface takes the pointer, so the node that carries the gesture declares itself:
        // `none` inherits from any transparent row above it, and a surface that is not a target
        // never receives the pointerdown the drag starts from.
        child.Style ??= new HtmlStyle();
        child.Style.PointerEvents = "auto";

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        child.RawAttributes ??= new Dictionary<string, string>();
        child.RawAttributes["data-eq-drag"] = draggable.Axis == DragAxis.Horizontal ? "x" : "y";
        child.RawAttributes["data-eq-drag-min"] = draggable.Min.ToString(culture);
        child.RawAttributes["data-eq-drag-max"] = draggable.Max.ToString(culture);
        child.RawAttributes["data-eq-drag-rest"] = draggable.RestOffset.ToString(culture);
        if (draggable.Normalized) child.RawAttributes["data-eq-drag-normalized"] = "1";
        if (!draggable.Follows) child.RawAttributes["data-eq-drag-follows"] = "0";
        if (draggable.OnMoved is not null) child.RawAttributes["data-eq-drag-moves"] = "1";

        // A gesture the caller paints itself is already where it belongs — translating the rest as
        // well would move it twice.
        if (draggable.RestOffset != 0 && draggable.Follows)
        {
            child.Style ??= new HtmlStyle();
            child.Style.Transform = draggable.Axis == DragAxis.Horizontal
                ? $"translateX({TokenCss.Px(draggable.RestOffset)})"
                : $"translateY({TokenCss.Px(draggable.RestOffset)})";
            child.Style.Transition = $"transform {Motion.BaseMs}ms";
        }

        // The release callback rides the SSR bridge the same way every other handler does; the
        // client half reads the offset off the event the controller dispatches.
        if (draggable.OnReleased is { } released)
        {
            child.CustomEvents["eq-drag-released"] = released;
        }

        if (draggable.OnMoved is { } moved)
        {
            child.CustomEvents["eq-drag-moved"] = moved;
        }

        return child;
    }

    /// <summary>
    /// Gestures v2 (SSR half): the drag marker rides the child's own root — the CLIENT runtime's
    /// pointer-capture controller drives the actual drag (the server only emits the marker; the
    /// dismiss callback attaches client-side through the lowering mirror's custom event).
    /// </summary>
    private HtmlElement? LowerDragDismiss(DragDismiss drag, bool? horizontalAxis)
    {
        if (Lower(drag.Child, horizontalAxis) is not { } child) return null;
        if (child is RealizedElement target)
        {
            // Same rule as Draggable: the surface the gesture starts on declares itself a target,
            // because `none` inherits from any transparent row above it.
            target.Style ??= new HtmlStyle();
            target.Style.PointerEvents = "auto";
        }
        if (child is RealizedElement realized)
        {
            realized.RawAttributes ??= new Dictionary<string, string>();
            realized.RawAttributes["data-eq-drag-dismiss"] =
                DragDismiss.ThresholdDp.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return child;
    }
}
