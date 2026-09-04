using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The three wrappers (design system C15's pointer tier, plus the typed row it requires).
/// <para>
/// The states these cover are the ones the Calendar's own review taught me to write first: the
/// DEFAULT (nothing chosen), the INTERACTED (opened, typed, picked), and the CONTROLLED (the app
/// moves the value). Three of the four real defects in that component came from testing only the
/// middle of a component's life.
/// </para>
/// </summary>
public class DatePickerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Render(VisualNode node, string culture = "en-US")
    {
        var previousFormat = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            return WebRealizer.Lower(node, Theme).Render();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static void Press(HtmlNode node)
    {
        var handler = node.Events.Values.OfType<Action>().FirstOrDefault();
        handler.Should().NotBeNull("this node has to carry a press for the test to mean anything");
        handler!();
    }

    private static HtmlNode Trigger(HtmlNode tree) =>
        Walk(tree).First(n => n.Attributes.ContainsKey("aria-expanded"));

    /// <summary>Types into the picker's field. The handler lives on the TextEntry NODE rather
    /// than on the lowered input: the SSR realizer declares a Pressable's click but not an
    /// entry's change (see LowerTextEntry — a gap, and a filed one), so the rendered tree has no
    /// handler to invoke. What the component promises is the node it builds.</summary>
    private static void TypeInto(UiComponent picker, string text, string culture = "en-US")
    {
        // Under the SAME culture as the render: what "7/17" MEANS is a culture question, and
        // parsing it under the machine's own is how a test passes in one country and fails in
        // another. This is the third time this pair has bitten in this track.
        IEnumerable<VisualNode> Walk(VisualNode node)
        {
            yield return node;
            var children = node switch
            {
                FlexNode flex => flex.Children,
                Box box => box.Child is { } only ? new[] { only } : Array.Empty<VisualNode>(),
                Pressable pressable => new[] { pressable.Child },
                Flexible flexible => new[] { flexible.Child },
                Anchored anchored => new[] { anchored.Anchor, anchored.Panel },
                Shortcut shortcut => new[] { shortcut.Child },
                UiComponent component => new[] { component.BuildContained(new ComponentContext(Theme)) },
                _ => Array.Empty<VisualNode>(),
            };
            foreach (var child in children)
                foreach (var descendant in Walk(child))
                    yield return descendant;
        }

        Under(culture, () =>
        {
            var entry = Walk(picker).OfType<TextEntry>().First();
            entry.OnChanged.Should().NotBeNull("the field has to be typeable");
            entry.OnChanged!(text);
        });
    }

    /// <summary>
    /// Runs under BOTH halves of .NET's culture pair, and is the only place in this file that
    /// touches them. Everything a picker does is culture-bound — the render, the parse, and
    /// AdoptConfig, which parses the typed buffer to decide whether it still says the incoming
    /// value. A test that pinned only the render read the buffer under the machine's own culture,
    /// and a day-first laptop turned 07/01 into January. Fourth time this pair has bitten here.
    /// </summary>
    private static void Under(string culture, Action body)
    {
        var previousFormat = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        // Resolved BEFORE the thread is touched: a name the platform cannot answer throws here,
        // where there is still nothing to restore.
        var asked = new CultureInfo(culture);
        try
        {
            CultureInfo.CurrentCulture = asked;
            CultureInfo.CurrentUICulture = asked;
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static string? ValueOf(HtmlNode tree) =>
        Walk(tree).FirstOrDefault(n => n.Tag == "input")?.Attributes.GetValueOrDefault("value");

    // ---- DatePicker -----------------------------------------------------------------------

    [Fact]
    public void NoControlIsNestedInsideAnother_InAnyPicker()
    {
        // HTML forbids interactive content inside a button, and a browser handed `button > input`
        // resolves it by taking the typing away — which is the path C15 calls REQUIRED for
        // keyboard and switch users. Asserted on all three, and after opening, because the panel
        // brings its own controls.
        foreach (var picker in new UiComponent[]
                 { new DatePicker(), new TimePicker(), new DateTimePicker() })
        {
            foreach (var stage in new[] { Render(picker), Reopen(picker) })
            {
                foreach (var control in Walk(stage).Where(n => n.Tag is "button" or "a"))
                {
                    var nested = Walk(control).Skip(1)
                        .Where(n => n.Tag is "input" or "select" or "textarea" or "button" or "a")
                        .Select(n => n.Tag)
                        .ToList();
                    nested.Should().BeEmpty(
                        "a control inside a control is markup no browser is required to honour");
                }
            }
        }
    }

    /// <summary>
    /// Fires a chord the way the browser would. A Shortcut lowers to a `data-eq-shortcut`
    /// attribute and its handler lives in the runtime, so there is nothing in the HTML to invoke —
    /// the binding is the NODE, and this finds it in the built tree.
    /// </summary>
    private static void Keys(UiComponent component, KeyChord chord)
    {
        var bound = Nodes(component).OfType<Shortcut>().FirstOrDefault(s => s.Chord == chord);
        bound.Should().NotBeNull($"nothing is listening for {chord}");
        bound!.OnPressed();
    }

    /// <summary>The tree after the field has been pressed once — the open state.</summary>
    private static HtmlNode Reopen(UiComponent picker)
    {
        Press(Trigger(Render(picker)));
        return Render(picker);
    }

    [Fact]
    public void TheFieldIsTypeableBeforeAnythingIsChosen()
    {
        var tree = Render(new DatePicker());

        // C15 requires the typed row, and it is the FIELD — not a fallback tucked away somewhere.
        var input = Walk(tree).Single(n => n.Tag == "input");
        // The LITERAL, not SdkStrings read back: the hint is derived from the render culture, so
        // comparing it against a read under the machine's own culture would assert nothing and
        // pass in the United States while failing in Brazil. en-US is month-first, and says so.
        input.Attributes["placeholder"].Should().Be("MM/DD/YYYY");
        input.Attributes.GetValueOrDefault("value").Should().BeEmpty();
        // …and the panel is not up until asked.
        Walk(tree).Should().NotContain(n => n.Attributes.GetValueOrDefault("role") == "grid");
    }

    [Fact]
    public void TheTriggerSaysWhatItOpens()
    {
        var trigger = Trigger(Render(new DatePicker()));
        trigger.Attributes["aria-expanded"].Should().Be("false");
        trigger.Attributes["aria-haspopup"].Should().Be("dialog");
        trigger.Attributes["aria-label"].Should().Be(SdkStrings.ChooseDate);
    }

    [Fact]
    public void OpeningShowsTheCalendar_AsADialogNotAListbox()
    {
        var picker = new DatePicker(new DateOnly(2026, 7, 17));
        Press(Trigger(Render(picker)));
        var open = Render(picker);

        Walk(open).Should().Contain(n => n.Attributes.GetValueOrDefault("role") == "dialog");
        Walk(open).Single(n => n.Attributes.GetValueOrDefault("role") == "grid")
            .Attributes["aria-label"].Should().Be("July 2026");
        Trigger(open).Attributes["aria-expanded"].Should().Be("true");
    }

    [Fact]
    public void PickingADayReportsItAndClosesThePanel()
    {
        DateOnly? reported = null;
        var picker = new DatePicker(new DateOnly(2026, 7, 17), d => reported = d);
        Press(Trigger(Render(picker)));
        var open = Render(picker);

        var firstCell = Walk(open).First(n => n.Attributes.GetValueOrDefault("role") == "gridcell");
        Press(firstCell);
        var closed = Render(picker);

        reported.Should().Be(new DateOnly(2026, 7, 1));
        Walk(closed).Should().NotContain(n => n.Attributes.GetValueOrDefault("role") == "grid");
    }

    [Fact]
    public void TypingADateReportsIt_WithoutOpeningAnything()
    {
        DateOnly? reported = null;
        var picker = new DatePicker(onChanged: d => reported = d);
        // The typed row commits the moment what is there IS a date — nothing else to press.
        TypeInto(picker, "7/17/2026");

        reported.Should().Be(new DateOnly(2026, 7, 17));
    }

    [Fact]
    public void AHalfTypedDateSaysSo_AndReportsNothing()
    {
        DateOnly? reported = null;
        var picker = new DatePicker(onChanged: d => reported = d);
        // "7/1" would NOT do: .NET parses it as July 1st of this year, and the field commits it
        // — which is right, and is why the test needs something that is genuinely not a date yet.
        TypeInto(picker, "7/");
        var after = Render(picker);

        reported.Should().BeNull();
        Walk(after).Single(n => n.Tag == "input").Attributes["aria-invalid"].Should().Be("true");
    }

    [Fact]
    public void TheFieldShowsTheSelectionInTheCulturesOwnShape()
    {
        ValueOf(Render(new DatePicker(new DateOnly(2026, 7, 17)))).Should().Be("7/17/2026");
        ValueOf(Render(new DatePicker(new DateOnly(2026, 7, 17)), "pt-BR")).Should().Be("17/07/2026");
    }

    // ---- TimePicker -----------------------------------------------------------------------

    [Fact]
    public void TheTimeListIsTheStepTheAppAskedFor()
    {
        var options = Walk(Render(new TimePicker(stepMinutes: 60)))
            .Count(n => n.Attributes.GetValueOrDefault("role") == "option");
        options.Should().Be(0, "the list is not up until the field is pressed");

        var picker = new TimePicker(stepMinutes: 60);
        Press(Trigger(Render(picker)));
        var open = Render(picker);

        // A day of whole hours is 24 slots, and its rows are OPTIONS: a time is a sequence, so
        // this is a listbox and not the grid a date needs.
        Walk(open).Count(n => n.Attributes.GetValueOrDefault("role") == "option").Should().Be(24);
        Walk(open).Should().Contain(n => n.Attributes.GetValueOrDefault("role") == "listbox");
    }

    [Fact]
    public void PickingATimeReportsIt()
    {
        TimeOnly? reported = null;
        var picker = new TimePicker(onChanged: t => reported = t, stepMinutes: 30);
        Press(Trigger(Render(picker)));
        var open = Render(picker);

        Press(Walk(open).First(n => n.Attributes.GetValueOrDefault("role") == "option"));

        reported.Should().Be(new TimeOnly(0, 0));
    }

    [Fact]
    public void ABoundedListStopsAtTheBound()
    {
        var picker = new TimePicker(min: new TimeOnly(9, 0), max: new TimeOnly(11, 0), stepMinutes: 60);
        Press(Trigger(Render(picker)));
        var open = Render(picker);

        Walk(open).Count(n => n.Attributes.GetValueOrDefault("role") == "option").Should().Be(3);
    }

    // ---- DateTimePicker -------------------------------------------------------------------

    [Fact]
    public void AMomentNeedsBothHalvesBeforeItIsReported()
    {
        DateTime? reported = null;
        var picker = new DateTimePicker(onChanged: m => reported = m, stepMinutes: 60);
        var built = picker.BuildContained(new ComponentContext(Theme));

        // Driven through the CHILDREN's own callbacks — what each half calls when the user picks.
        // A direct re-render cannot carry nested state: the instance store that preserves a child
        // component across renders belongs to the mounted path, not to a lowering in a test.
        var date = Children(built).OfType<DatePicker>().Single();
        var time = Children(built).OfType<TimePicker>().Single();

        date.OnChanged!(new DateOnly(2026, 7, 17));
        reported.Should().BeNull("half a moment is not a value an app can store");

        time.OnChanged!(new TimeOnly(9, 0));
        reported.Should().Be(new DateTime(2026, 7, 17, 9, 0, 0));
    }

    [Fact]
    public void AMomentOutsideItsBounds_IsNotReported()
    {
        DateTime? reported = null;
        var picker = new DateTimePicker(
            onChanged: m => reported = m,
            min: new DateTime(2026, 7, 17, 10, 0, 0),
            stepMinutes: 60);
        var built = picker.BuildContained(new ComponentContext(Theme));

        // The bound is on the MOMENT: the 17th passes a date-only check and 09:00 on the 17th
        // still falls before a minimum of 10:00 that day.
        Children(built).OfType<DatePicker>().Single().OnChanged!(new DateOnly(2026, 7, 17));
        Children(built).OfType<TimePicker>().Single().OnChanged!(new TimeOnly(9, 0));
        reported.Should().BeNull();

        Children(built).OfType<TimePicker>().Single().OnChanged!(new TimeOnly(11, 0));
        reported.Should().Be(new DateTime(2026, 7, 17, 11, 0, 0));
    }

    [Fact]
    public void AControlledValueBeatsAStaleBuffer_ButNotTheWordBeingTyped()
    {
        // The app moved the date somewhere the buffer does not say: the app wins, or the field
        // goes on showing text that no longer matches the value behind it.
        var picker = new DatePicker();
        TypeInto(picker, "7/17/2026");
        Under("en-US", () => picker.AdoptConfig(new DatePicker(new DateOnly(2026, 12, 25))));
        ValueOf(Render(picker)).Should().Be("12/25/2026");

        // And the case that makes the naive rule wrong: every parseable keystroke is REPORTED, so
        // a controlled parent hands most of them straight back. That echo is not the app moving
        // anything, and reformatting on it would rewrite the field under the cursor.
        // Typed WITH the leading zeros, which en-US does not use when it formats — so the two
        // strings differ where the DATE does not, and the assertion can tell "kept the buffer"
        // from "reformatted the value".
        var typing = new DatePicker();
        TypeInto(typing, "07/01/2026");
        Under("en-US", () => typing.AdoptConfig(new DatePicker(new DateOnly(2026, 7, 1))));
        ValueOf(Render(typing)).Should().Be("07/01/2026", "the reader is still mid-word");
    }

    [Fact]
    public void TheTimeListIsSteerableByKeyboard_NotOnlyByPointer()
    {
        // The listbox pattern puts the rows at tabindex=-1 on purpose: the highlight is meant to
        // travel on aria-activedescendant off the trigger. Which means that without arrows and an
        // Enter, a keyboard user can open this list and then do nothing in it.
        var picker = new TimePicker(new TimeOnly(9, 0), stepMinutes: 60);
        Press(Trigger(Render(picker)));

        var trigger = Trigger(Render(picker));
        // Opening starts on the VALUE, not on midnight: 09:00 is the tenth slot of whole hours.
        trigger.Attributes.Should().ContainKey("aria-activedescendant");
        var onNine = trigger.Attributes["aria-activedescendant"];

        Keys(picker, KeyChord.ArrowDown);
        var moved = Trigger(Render(picker)).Attributes["aria-activedescendant"];
        moved.Should().NotBe(onNine, "the arrow has to move the stated highlight, not only paint");

        TimeOnly? committed = null;
        var listening = new TimePicker(new TimeOnly(9, 0), t => committed = t, stepMinutes: 60);
        Press(Trigger(Render(listening)));
        Keys(listening, KeyChord.ArrowDown);
        Keys(listening, KeyChord.Enter);
        committed.Should().Be(new TimeOnly(10, 0), "Enter commits the row the arrows reached");
    }

    [Fact]
    public void NoAriaReferencePointsAtNothing_EvenWithAnEmptyRange()
    {
        // A range with nothing in it: Min past Max offers no slots, and a trigger that still names
        // option zero sends a screen reader to an element the page does not have. Silence beats a
        // dangling reference.
        var empty = new TimePicker(min: new TimeOnly(18, 0), max: new TimeOnly(9, 0));
        Press(Trigger(Render(empty)));
        var open = Render(empty);

        Walk(open).Should().NotContain(n => n.Attributes.GetValueOrDefault("role") == "option");
        Trigger(open).Attributes.Should().NotContainKey("aria-activedescendant");

        // And the general form of the same rule, on the whole tree: every reference resolves.
        var ids = Walk(open).Select(n => n.Attributes.GetValueOrDefault("id")).Where(id => id is not null).ToHashSet();
        foreach (var attribute in new[] { "aria-activedescendant", "aria-controls", "aria-describedby", "aria-labelledby" })
        {
            foreach (var node in Walk(open).Where(n => n.Attributes.ContainsKey(attribute)))
            {
                ids.Should().Contain(node.Attributes[attribute],
                    $"{attribute} names an element that has to exist");
            }
        }
    }

    [Fact]
    public void AClosedListCostsNothing_HoweverFineTheStep()
    {
        // The panel is not realized while closed, so building its rows is work nobody sees. At a
        // one-minute step that is a day of them, on every render, until the field is pressed.
        var closed = new TimePicker(stepMinutes: 1);
        Nodes(closed).OfType<Pressable>().Count(p => p.Role == PressableRole.Option).Should().Be(0);

        Press(Trigger(Render(closed)));
        Nodes(closed).OfType<Pressable>().Count(p => p.Role == PressableRole.Option)
            .Should().Be(24 * 60, "and every one of them once it is open");
    }

    /// <summary>Every node the component BUILDS, whether or not a target would realize it.</summary>
    private static IEnumerable<VisualNode> Nodes(UiComponent component)
    {
        IEnumerable<VisualNode> Walk(VisualNode node)
        {
            yield return node;
            var children = node switch
            {
                FlexNode flex => flex.Children,
                Box box => box.Child is { } only ? new[] { only } : Array.Empty<VisualNode>(),
                Pressable pressable => new[] { pressable.Child },
                Flexible flexible => new[] { flexible.Child },
                Anchored anchored => new[] { anchored.Anchor, anchored.Panel },
                Shortcut shortcut => new[] { shortcut.Child },
                ScrollView scroll => new[] { scroll.Child },
                _ => Array.Empty<VisualNode>(),
            };
            foreach (var child in children)
                foreach (var descendant in Walk(child))
                    yield return descendant;
        }

        return Walk(component.BuildContained(new ComponentContext(Theme)));
    }

    [Fact]
    public void AControlledParentThatClearsTheMoment_ClearsBothHalves()
    {
        var picker = new DateTimePicker(new DateTime(2026, 7, 17, 11, 0, 0));

        // Null is a value the app can hand down. A picker that treats "no value" as "nothing
        // changed" keeps showing the moment a Clear button just removed.
        picker.AdoptConfig(new DateTimePicker());

        var built = picker.BuildContained(new ComponentContext(Theme));
        Children(built).OfType<DatePicker>().Single().Selected.Should().BeNull();
        Children(built).OfType<TimePicker>().Single().Selected.Should().BeNull();
    }

    [Fact]
    public void AStepUnderAMinute_IsAMinute_AtEveryDoor()
    {
        // A step of zero cannot walk, and the guard that stops it walking forever would leave the
        // list holding one option. Both doors answer the same: the constructor, and the adopt an
        // app's re-render comes through.
        new TimePicker(stepMinutes: 0).StepMinutes.Should().Be(1);

        var picker = new TimePicker(stepMinutes: 60);
        picker.AdoptConfig(new TimePicker(stepMinutes: 0));
        picker.StepMinutes.Should().Be(1);

        Press(Trigger(Render(picker)));
        Walk(Render(picker)).Count(n => n.Attributes.GetValueOrDefault("role") == "option")
            .Should().Be(24 * 60, "a minute step is a day of minutes, not one lonely option");
    }

    /// <summary>The visual children of a built node, one level of composition deep.</summary>
    private static IEnumerable<VisualNode> Children(VisualNode node)
    {
        yield return node;
        var children = node switch
        {
            FlexNode flex => flex.Children,
            Flexible flexible => new[] { flexible.Child },
            Box box => box.Child is { } only ? new[] { only } : Array.Empty<VisualNode>(),
            _ => Array.Empty<VisualNode>(),
        };
        foreach (var child in children)
            foreach (var descendant in Children(child))
                yield return descendant;
    }
}
