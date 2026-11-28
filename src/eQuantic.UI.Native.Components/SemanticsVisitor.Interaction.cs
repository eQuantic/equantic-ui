using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Interaction and motion — sixteen words. Three are controls with a keyboard stop each:
/// <see cref="Pressable"/> and <see cref="Link"/> announce as one stop, and
/// <see cref="Adjustable"/> as one stop when it is a slider and as a GROUP that keeps walking when it
/// is a tab strip or a radio group (#500); a fourth, <see cref="Progress"/>, announces WITHOUT being
/// one — it is read, never moved, which is why it carries no tab stop and no key handler; three,
/// <see cref="Navigable"/>, <see cref="LiveRegion"/> and <see cref="Group"/>, are GROUPS that announce
/// and then keep walking; the remaining nine wrap a child or are ornament.
/// <para>
/// This tally said "the fourteenth, Navigable, is the gap" until <see cref="SemanticRole.Group"/>
/// landed — the running-count shape that rots, caught here one commit after the gap closed.
/// </para>
/// </summary>
internal sealed partial class SemanticsVisitor
{
    /// <summary>
    /// A control's inner text IS its name, not a separate stop — the subtree is consumed, exactly as
    /// the web's <c>&lt;button&gt;text&lt;/button&gt;</c> reads as one element.
    /// </summary>
    public bool Visit(Pressable node, LayoutNode laidOut)
    {
        // The pressable a listbox panel hangs from is the COMBOBOX, whatever role it was given: the
        // web's own rule (`LowerAnchored` puts role="combobox" on the anchor's root), and the one
        // role here decided by where the pressable SITS rather than by what it says it is. Its state
        // is whether the list is open, never a check or a pick. Until #501 a Select's field reached
        // every bridge as a button that happened to expand.
        //
        // Its NAME is the author's label, and none without one, and what the field SHOWS is always its
        // value, the choice or the placeholder: "Country, Brazil". That is the web's reading, where a
        // combobox is never named from its content (accname) and a select-only combobox's content is
        // its value. With no label, what the field showed was the name here until #1328, so a reader heard
        // the option where the field's name belongs; a labelled one already read as it does now.
        if (ListboxAnchoredBy(laidOut) is not null)
        {
            var shown = TextWithin(laidOut);
            return Announce(new(SemanticRole.ComboBox, IdOf(laidOut), laidOut.Bounds,
                node.Label ?? "", shown.Length == 0 ? null : shown,
                node.Disabled, Expanded: node.Expanded));
        }

        // A check states its state beside the name, never inside it — the exact mirror of the web's
        // aria-checked. Mixed is checkbox-only, ARIA's own rule.
        //
        // Every role is written out and there is no default arm (#338). This switch ended in
        // `_ => Button` while it knew three of the nine roles, so a radio, a tab, a menu item, a list
        // option and a navigation destination reached VoiceOver and TalkBack as buttons — and a role
        // appended to PressableRole would have joined them without a word. Now that append fails the
        // BUILD and names the role (CS8509), the question NativeRole.Of asks of SemanticRole.
        // CS8524 is the UNNAMED value, which here only an author's cast of an integer can make — a
        // role the vocabulary does not offer — and it throws rather than pass for a button: the
        // compiler's question about every NAMED role is worth more than a guess about the rest.
#pragma warning disable CS8524
        var (role, check) = node.Role switch
        {
            PressableRole.Button => (SemanticRole.Button, (SemanticCheck?)null),
            // Checked, not picked — the web's aria-checked, and AXValue and RadioButton's own state.
            PressableRole.Radio => (SemanticRole.Radio,
                (SemanticCheck?)(node.Selected == true ? SemanticCheck.On : SemanticCheck.Off)),
            PressableRole.Checkbox => (SemanticRole.Checkbox,
                (SemanticCheck?)(node.Mixed ? SemanticCheck.Mixed
                    : node.Selected == true ? SemanticCheck.On : SemanticCheck.Off)),
            PressableRole.Switch => (SemanticRole.Switch,
                (SemanticCheck?)(node.Selected == true ? SemanticCheck.On : SemanticCheck.Off)),
            PressableRole.Tab => (SemanticRole.Tab, (SemanticCheck?)null),
            PressableRole.MenuItem => (SemanticRole.MenuItem, (SemanticCheck?)null),
            PressableRole.Option => (SemanticRole.Option, (SemanticCheck?)null),
            // Where the user IS rides Current, below, not a check and not a pick.
            PressableRole.Destination => (SemanticRole.Destination, (SemanticCheck?)null),
            PressableRole.GridCell => (SemanticRole.GridCell, (SemanticCheck?)null),
            // Picked when it says so, open when it has children, and placed (#1529).
            PressableRole.TreeItem => (SemanticRole.TreeItem, (SemanticCheck?)null),
        };
#pragma warning restore CS8524
        // PICKED-ness, for the three roles that have it. Before the Selected field existed a Tab and
        // an Option arrived here as plain Buttons whose selection was paint only.
        bool? selected = node.Role is PressableRole.Tab or PressableRole.Option or PressableRole.GridCell
            ? node.Selected == true
            // A tree item says whether it is picked only when the tree says so, as the web writes
            // aria-selected only then: a tree with no selection announces none.
            : node.Role == PressableRole.TreeItem ? node.Selected
            : null;
        // A checkbox, a radio or a switch is a nameable control: the parts that reach it name and describe it over its
        // label and its content (#1916).
        var nameable = node.Role is PressableRole.Checkbox or PressableRole.Radio or PressableRole.Switch;
        return Announce(new(role, IdOf(laidOut), laidOut.Bounds,
            (nameable ? NameOf(laidOut, node.Label) : node.Label) ?? TextWithin(laidOut), null, node.Disabled, check,
            node.Expanded,
            node.Role == PressableRole.Destination && node.Selected == true,
            selected,
            TreePlace: node.Role == PressableRole.TreeItem ? node.TreePlace : null,
            Description: nameable ? DescriptionOf(laidOut) : null));
    }

    /// <summary>
    /// Whether this pressable IS a listbox panel's anchor: every ancestor up to an
    /// <see cref="Anchored"/> whose panel is a <see cref="AnchorPanelRole.Listbox"/> holds this
    /// pressable and nothing else. The ANCHOR becomes the combobox, the vocabulary's own words for
    /// that role (<see cref="Anchored.PanelRole"/>), and the panel never lays out under it here: it
    /// is an overlay root of its own.
    /// <para>
    /// What holds only the pressable is not a thing a reader meets in its place, so it is walked
    /// through: a component that BUILT the pressable, the way the web reaches the element a
    /// component lowers to, and a wrapper an author put around it, a box that sizes it, a shortcut
    /// that binds a key to it, or a tooltip, whose own <see cref="Anchored"/> hangs no panel a reader
    /// walks into. An ancestor that holds something else as well, a row of two pressables, means
    /// this one sits inside the anchor rather than being it, and nothing in there is the combobox.
    /// </para>
    /// </summary>
    /// <returns>The <see cref="Anchored"/> whose listbox hangs from this control, or null.</returns>
    private static Anchored? ListboxAnchoredBy(LayoutNode laidOut)
    {
        for (var node = laidOut; node.Parent is { } parent; node = parent)
        {
            if (parent.Source is Anchored { PanelRole: not AnchorPanelRole.None } anchored)
                return anchored.PanelRole == AnchorPanelRole.Listbox ? anchored : null;
            if (parent.Children.Count != 1) return null;
        }
        return null;
    }

    /// <summary>One stop, named by the author or derived from the words inside it — the same rule
    /// the Pressable arm follows, and the reason both consume their subtree.</summary>
    public bool Visit(Link node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.Link, IdOf(laidOut), laidOut.Bounds,
            node.Label ?? TextWithin(laidOut), null, false, Current: node.Current));

    /// <summary>
    /// ONE KEYBOARD STOP for the whole control, inner pressables stay pointer-only — the rule the
    /// focus route applies (<c>InputSink.WithoutFocusStops</c>). What a READER meets depends on the
    /// role, because a slider and a set of choices are different things to read, as the web's
    /// <c>slider</c>, <c>tablist</c> and <c>radiogroup</c> are.
    /// <para>
    /// A SLIDER is one stop that consumes its subtree, and its VALUE rides the same slot a text
    /// field's does (spec C7) — the value is the slider role's and no other's, exactly as on the web,
    /// where a <c>tablist</c> and a <c>radiogroup</c> report no range at all.
    /// </para>
    /// <para>
    /// A TAB STRIP and a RADIO GROUP are CONTAINERS, read and then walked into: the reader names the
    /// bar or the group, then lands on each tab or radio, which says itself whether it is the picked
    /// one (#338's roles) and runs the same handler a tap does. Until #500 every Adjustable was
    /// announced as a slider, so a <c>Tabs</c> and a <c>RadioGroup</c> reached every bridge as one
    /// unnamed slider with no value, and the tabs and radios inside were never read.
    /// </para>
    /// <para>
    /// The group takes no adjust gesture on any platform, as NSTabView, Android's RadioGroup and
    /// Flutter's tab bar take none: the arrows that move the pick are the keyboard's way through the
    /// set, and a reader's way is the items. A slider's adjust has a value to announce after it; a
    /// group's would move the pick and say nothing.
    /// </para>
    /// <para>
    /// Every role is written out and there is no default arm: a role appended to
    /// <see cref="AdjustableRole"/> fails the BUILD by name (CS8509) instead of being read as a
    /// slider, which is how the two lost theirs. CS8524, the unnamed value only a cast makes, is
    /// waived as narrowly as the Pressable arm waives it.
    /// </para>
    /// </summary>
#pragma warning disable CS8524
    public bool Visit(Adjustable node, LayoutNode laidOut) => node.Role switch
    {
        AdjustableRole.Slider => Announce(new(SemanticRole.Slider, IdOf(laidOut), laidOut.Bounds,
            node.Label, node.Spoken, false,
            // The NUMBERS travel beside the words, so a bridge can offer its platform's own range — a
            // `RangeInfo` on Android, `AXMinValue`/`AXMaxValue` on macOS (#243).
            Range: node.Value)),
        AdjustableRole.Tablist => AnnounceGroup(new(SemanticRole.TabBar, IdOf(laidOut),
            laidOut.Bounds, node.Label, null, false)),
        AdjustableRole.Radiogroup => AnnounceGroup(new(SemanticRole.RadioGroup, IdOf(laidOut),
            laidOut.Bounds, node.Label, null, false)),
        // A separator a reader moves is a splitter, one stop whose value and range ride beside its
        // words as a slider's do (#1538).
        AdjustableRole.Separator => Announce(new(SemanticRole.Splitter, IdOf(laidOut), laidOut.Bounds,
            node.Label, node.Spoken, false, Range: node.Value)),
    };
#pragma warning restore CS8524

    /// <summary>
    /// Spec B14: the bar says WHAT IT IS FOR and HOW FAR ALONG. It is not a Slider — the platforms
    /// split the two (AXProgressIndicator, android.widget.ProgressBar), and calling this one a
    /// slider would offer VoiceOver's adjust gestures on something nothing can move.
    /// <para>
    /// A null value is INDETERMINATE and announces the name alone, which is the honest answer when
    /// nothing knows how far along it is — the opposite of the Adjustable rule, where a missing
    /// value means the node was never a slider.
    /// </para>
    /// </summary>
    public bool Visit(Progress node, LayoutNode laidOut) =>
        Announce(new(SemanticRole.ProgressIndicator, IdOf(laidOut), laidOut.Bounds,
            // `node.Spoken`, not `node.Value?.Spoken`: an indeterminate bar has no value and may
            // still have WORDS, which is the half #243 calls the real loss. "Estimating time
            // remaining" is most useful exactly where there is no number to fall back on.
            node.Label, node.Spoken, false, Range: node.Value));

    /// <summary>
    /// A navigable region is a TREE or a GRID, named by its label, which the reader names and then walks
    /// (web twin: <c>LowerNavigable</c>, whose host is <c>role="tree"</c> or <c>role="grid"</c>). A grid that
    /// says its first row heads the columns (<see cref="Navigable.HasHeaderRow"/>) reads that row's cells as
    /// COLUMN HEADERS, each named by the words in it: the row content's direct children, one per column, the
    /// cells the web marks <c>columnheader</c>. Its other rows are walked as they are, so its days stay the
    /// grid cells they say they are.
    /// <para>
    /// A grid was read as a group until #1330, its weekdays as plain text, so a reader on Photon lost the
    /// grid's rows and columns and the header that says which weekday a day falls on.
    /// </para>
    /// </summary>
    public bool Visit(Navigable node, LayoutNode laidOut)
    {
        if (node.Role == NavigableRole.Tree)
            return AnnounceGroup(new(SemanticRole.Tree, IdOf(laidOut), laidOut.Bounds, node.Label, null, false));
        _nodes.Add(new(SemanticRole.Grid, IdOf(laidOut), laidOut.Bounds, node.Label, null, false));
        for (var index = 0; index < laidOut.Children.Count; index++)
        {
            var row = laidOut.Children[index];
            if (node.HasHeaderRow && index == 0)
            {
                foreach (var header in row)
                    Announce(new(SemanticRole.ColumnHeader, IdOf(header), header.Bounds, TextWithin(header), null, false));
                continue;
            }
            SemanticsTree.Walk(row, this);
        }
        return Consumed;
    }

    /// <summary>
    /// A table viewport is a GROUP named by its label, over the cells it holds in view: a reader walks
    /// them as it walks a list's rows. Unless it says it is a table or a grid (#1081, web twin:
    /// <c>lowerTableViewport</c>): then it announces that role with the whole grid's extent, and each cell
    /// it holds announces its own (<see cref="TableCell"/>). The tree is flat, so the cells follow the table
    /// in reading order rather than sit inside it (#1105).
    /// </summary>
    public bool Visit(TableViewport node, LayoutNode laidOut)
    {
        if (node.Role == TableRole.Group)
            return AnnounceGroup(new(SemanticRole.Group, IdOf(laidOut), laidOut.Bounds,
                node.Label ?? "", null, false));
        var grid = node.Role == TableRole.Grid;
        _nodes.Add(new(grid ? SemanticRole.Grid : SemanticRole.Table, IdOf(laidOut), laidOut.Bounds,
            node.Label ?? "", null, false, Extent: new TableRange(0, 0, node.Rows.Count, node.Columns.Count)));
        // Row by row, each row's cells by column, whatever order they were added in: the reading order of a
        // table, which the web's rows give by owning their cells in column order.
        var cells = new List<(TableViewCell Cell, LayoutNode LaidOut)>();
        foreach (var child in laidOut)
            if (child.Source is TableViewCell { Mark: false } cell) cells.Add((cell, child));
        cells.Sort((a, b) => a.Cell.Row != b.Cell.Row
            ? a.Cell.Row.CompareTo(b.Cell.Row)
            : a.Cell.Column.CompareTo(b.Cell.Column));
        foreach (var (cell, child) in cells) TableCell(cell, child, grid);
        return Consumed;
    }

    /// <summary>
    /// One cell of a table or a grid: the header of its column or of its row, a cell or a grid cell, with
    /// its place in the whole grid and, in a grid, whether it is selected and whether it is current. It is
    /// named by the text it holds, which it consumes, as a button is named by its own; anything else inside
    /// it, a control or a heading, still follows it as a node of its own.
    /// </summary>
    private void TableCell(TableViewCell cell, LayoutNode laidOut, bool grid)
    {
        var inside = new List<SemanticNode>();
        var walker = new SemanticsVisitor(inside);
        foreach (var child in laidOut) SemanticsTree.Walk(child, walker);
        var name = string.Join(" ", inside
            .Where(each => each.Role == SemanticRole.StaticText && each.Label.Length > 0)
            .Select(each => each.Label));
        var role = cell.Role switch
        {
            TableCellRole.ColumnHeader => SemanticRole.ColumnHeader,
            TableCellRole.RowHeader => SemanticRole.RowHeader,
            _ => grid ? SemanticRole.GridCell : SemanticRole.Cell,
        };
        // In a grid a cell says whether it is selected, and a header only when it is (web twin:
        // describeTableCell), so a reader hears a selected column's header as selected.
        _nodes.Add(new(role, IdOf(laidOut), laidOut.Bounds, name, null, false,
            Current: grid && cell.Current,
            Selected: !grid ? null : role == SemanticRole.GridCell ? cell.Selected : cell.Selected ? true : null,
            Place: new TableRange(cell.Row, cell.Column, cell.Rows, cell.Columns)));
        // The plain text is the cell's name; a heading inside it stays a heading too, as the web's does, so
        // a reader still jumps to it.
        foreach (var each in inside)
            if (each.Role != SemanticRole.StaticText || each.HeadingLevel > 0) _nodes.Add(each);
    }

    /// <summary>
    /// A live region is a GROUP THE PLATFORM WATCHES. The urgency rides on the node rather than the
    /// role because that is how every platform models it — Android sets
    /// <c>accessibilityLiveRegion</c> on a container, UIKit posts an announcement — and because the
    /// region is still an ordinary group when nothing has changed.
    ///
    /// <para>
    /// PHOTON FENCE — the announcement itself does not happen here yet, and the reason is
    /// structural rather than a missing line. Announcing means noticing that a subtree CHANGED, and
    /// <c>PhotonHost.Semantics()</c> is a snapshot of the current frame with nothing to compare it
    /// against: no previous tree is kept and no pass diffs one. Posting from this walk would fire on
    /// every frame, which is worse than silence — a reader that repeats itself sixty times a second
    /// is a reader the user turns off. So what lands here is the DATA the announcement needs
    /// (<see cref="SemanticNode.Live"/> on a named group, at its bounds), and the frame-to-frame
    /// comparison plus one post per platform is its own slice.
    /// </para>
    ///
    /// <para>
    /// The web needs none of that: the DOM diff already knows what changed, so <c>aria-live</c> on
    /// the host IS the mechanism. That asymmetry is why this row closes on one target and not both,
    /// and it is stated rather than left to be discovered from a silent VoiceOver.
    /// </para>
    /// </summary>
    public bool Visit(LiveRegion node, LayoutNode laidOut) =>
        AnnounceGroup(new(SemanticRole.Group, IdOf(laidOut), laidOut.Bounds,
            NameOf(laidOut, node.Label) ?? "", null, false, Live: node.Urgency, Description: DescriptionOf(laidOut)));

    /// <summary>A labelled group: one stop named by its label, then what it holds, as the web's
    /// <c>role="group"</c> is read.</summary>
    public bool Visit(Group node, LayoutNode laidOut) =>
        AnnounceGroup(new(SemanticRole.Group, IdOf(laidOut), laidOut.Bounds, NameOf(laidOut, node.Label) ?? "", null, false,
            Description: DescriptionOf(laidOut)));

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(DragDismiss node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Draggable node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Hoverable node, LayoutNode laidOut) => Wraps;

    /// <summary>What a person asks the menu of reads as its child: the request is a gesture, not a role.</summary>
    public bool Visit(ContextTarget node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Listener node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(RawGestureDetector node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(DropTarget node, LayoutNode laidOut) => Wraps;
    /// <summary>A named list is a labelled GROUP, read and then walked into, as the web's role=group says it
    /// (#1536); an unnamed one wraps its child and says nothing of its own. Once the keyboard's carry has
    /// spoken in it (#1564), the list is the polite live region a shell marks, a group with no name when it has
    /// none, so Android has the node TalkBack reads the step from.</summary>
    public bool Visit(Sortable node, LayoutNode laidOut)
    {
        var speaks = NodeStates.Find<LiveRegionState>(laidOut.Element) is { Words: not null };
        return node.Label.Length == 0 && !speaks
            ? Wraps
            : AnnounceGroup(new(SemanticRole.Group, IdOf(laidOut), laidOut.Bounds, node.Label, null, false,
                Live: speaks ? LiveRegionUrgency.Polite : null));
    }
    /// <inheritdoc cref="Wraps"/>
    public bool Visit(SortableItem node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(InFlow node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(InView node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Decorative"/>
    public bool Visit(LoopMotion node, LayoutNode laidOut) => Decorative;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Presence node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Shortcut node, LayoutNode laidOut) => Wraps;

    /// <inheritdoc cref="Wraps"/>
    public bool Visit(Simulated node, LayoutNode laidOut) => Wraps;

    /// <summary>Every Text string under a node, joined — the derived accessible name of a control
    /// whose author gave it no explicit Label.</summary>
    internal static string TextWithin(LayoutNode node)
    {
        var parts = new List<string>();
        Gather(node, parts);
        return string.Join(" ", parts);

        static void Gather(LayoutNode node, List<string> parts)
        {
            // PlainContent, for the reason the Text arm gives: a paragraph with runs has an empty
            // Content. Here it costs more than a missing announcement — a control whose label happens
            // to emphasise one word derived NO name at all, and a nameless button is announced as
            // "button" and nothing else.
            // A text drawn and never read is no part of a name either, as the web's aria-hidden says.
            if (node.Source is Text { PlainContent.Length: > 0 } text && !HiddenFromReaders(text))
                parts.Add(text.PlainContent);
            foreach (var child in node) Gather(child, parts);
        }
    }
}
