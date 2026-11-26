using System.Globalization;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Testing.Tests;

/// <summary>
/// A picker drawn again without the value it reported says so in development, and a form that lays its
/// labels out itself draws a picker without its own (#1780). An order form drew
/// <c>new DatePicker(null, day =&gt; ChooseDay(day))</c>: the field emptied on every redraw while the day
/// stayed chosen underneath, and lived three slices before anyone saw it.
/// </summary>
[Collection(nameof(DevelopmentNoticesCollection))]
public class PickerNoticesTests
{
    /// <summary>The order form as it was written: the day kept, and never handed back.</summary>
    private sealed class OrderForm(bool handsTheDayBack) : StatefulComponent
    {
        private DateOnly? _day;

        public override VisualNode Build(ComponentContext context) =>
            Column(children:
            [
                DatePicker(handsTheDayBack ? _day : null, day => SetState(() => _day = day), label: "Day"),
                Text(_day is { } day ? $"Chosen {day:d}" : "Nothing chosen"),
            ]);
    }

    private static readonly TimeOnly Nine = new(9, 0);

    /// <summary>The text as the en-US tester formats it: .NET writes a narrow space before "AM".</summary>
    private static string English(FormattableString text) => text.ToString(CultureInfo.GetCultureInfo("en-US"));

    private static List<string> Listen()
    {
        var heard = new List<string>();
        DevelopmentNotices.Sink = heard.Add;
        return heard;
    }

    [Fact]
    public void APickerDrawnAgainWithoutTheDayItReported_SaysSo()
    {
        var heard = Listen();
        try
        {
            using var tester = new ComponentTester(culture: CultureInfo.GetCultureInfo("en-US"));
            tester.Pump(new OrderForm(handsTheDayBack: false));

            tester.EnterText("Day", "7/17/2026");
            tester.Pump();

            tester.Find("Chosen 7/17/2026").Should().NotBeNull("the page kept the day the picker reported");
            heard.Should().ContainSingle().Which.Should()
                .StartWith("A DatePicker reported 7/17/2026 through onChanged and was drawn again with no date");
        }
        finally
        {
            DevelopmentNotices.Sink = null;
        }
    }

    [Fact]
    public void APickerHandedBackTheDayItReported_SaysNothing()
    {
        var heard = Listen();
        try
        {
            using var tester = new ComponentTester(culture: CultureInfo.GetCultureInfo("en-US"));
            tester.Pump(new OrderForm(handsTheDayBack: true));

            tester.EnterText("Day", "7/17/2026");
            tester.Pump();

            heard.Should().BeEmpty("the controlled picker shows what it is handed, which is what it reported");
        }
        finally
        {
            DevelopmentNotices.Sink = null;
        }
    }

    /// <summary>A form drawn again for another reason, its picker still empty because nobody chose a day.</summary>
    private sealed class EmptyForm : StatefulComponent
    {
        private int _presses;

        public override VisualNode Build(ComponentContext context) =>
            Column(children:
            [
                DatePicker(null, _ => { }, label: "Day"),
                Button($"Pressed {_presses}", onPressed: () => SetState(() => _presses++)),
            ]);
    }

    [Fact]
    public void APickerNobodyChoseFrom_DrawnAgainEmpty_SaysNothing()
    {
        var heard = Listen();
        try
        {
            using var tester = new ComponentTester(culture: CultureInfo.GetCultureInfo("en-US"));
            tester.Pump(new EmptyForm());

            tester.Tap("Pressed 0");
            tester.Pump();

            tester.Find("Pressed 1").Should().NotBeNull("the form was drawn again");
            heard.Should().BeEmpty("an empty picker handed nothing is what it was told to be");
        }
        finally
        {
            DevelopmentNotices.Sink = null;
        }
    }

    /// <summary>A form whose picker has no handler, drawn again for another reason.</summary>
    private sealed class UnheardForm : StatefulComponent
    {
        private int _presses;

        public override VisualNode Build(ComponentContext context) =>
            Column(children:
            [
                DatePicker(label: "Day"),
                Button($"Pressed {_presses}", onPressed: () => SetState(() => _presses++)),
            ]);
    }

    [Fact]
    public void APickerNobodyListensTo_DrawnAgainAfterADay_SaysNothing()
    {
        var heard = Listen();
        try
        {
            using var tester = new ComponentTester(culture: CultureInfo.GetCultureInfo("en-US"));
            tester.Pump(new UnheardForm());

            tester.EnterText("Day", "7/17/2026");
            tester.Pump();
            tester.Tap("Pressed 0");
            tester.Pump();

            tester.Find("Pressed 1").Should().NotBeNull("the form was drawn again");
            heard.Should().BeEmpty("a picker with no onChanged reported nothing to anyone");
        }
        finally
        {
            DevelopmentNotices.Sink = null;
        }
    }

    /// <summary>A shift form keeping the time and the moment, and handing back neither.</summary>
    private sealed class ShiftForm(bool moment) : StatefulComponent
    {
        private string _kept = "Nothing kept";

        public override VisualNode Build(ComponentContext context) =>
            Column(children:
            [
                moment
                    ? DateTimePicker(null, at => SetState(() => _kept = $"Kept {at:g}"), dateLabel: "Day", timeLabel: "Time")
                    : TimePicker(null, at => SetState(() => _kept = $"Kept {at:t}"), label: "Time"),
                Text(_kept),
            ]);
    }

    [Fact]
    public void ATimePickerDrawnAgainWithoutTheTimeItReported_SaysSo()
    {
        var heard = Listen();
        try
        {
            using var tester = new ComponentTester(culture: CultureInfo.GetCultureInfo("en-US"));
            tester.Pump(new ShiftForm(moment: false));

            tester.Tap("Time");
            tester.Pump();
            tester.Tap("9:00 AM");
            tester.Pump();

            tester.Find("Kept 9:00 AM").Should().NotBeNull("the page kept the time the picker reported");
            heard.Should().ContainSingle().Which.Should()
                .StartWith(English($"A TimePicker reported {Nine:t} through onChanged and was drawn again with no time"));
        }
        finally
        {
            DevelopmentNotices.Sink = null;
        }
    }

    [Fact]
    public void ADateTimePickerDrawnAgainWithoutTheMoment_SaysSoOnce_ForTheMomentAndNotItsHalves()
    {
        var heard = Listen();
        try
        {
            using var tester = new ComponentTester(culture: CultureInfo.GetCultureInfo("en-US"));
            tester.Pump(new ShiftForm(moment: true));

            tester.EnterText("Day", "7/17/2026");
            tester.Pump();
            tester.Tap("Time");
            tester.Pump();
            tester.Tap("9:00 AM");
            tester.Pump();

            tester.Find("Kept 7/17/2026 9:00 AM").Should().NotBeNull("the page kept the moment the picker reported");
            heard.Should().ContainSingle("the halves were handed what the moment's picker told them, and the mistake is one")
                .Which.Should().StartWith(English($"A DateTimePicker reported {new DateOnly(2026, 7, 17).ToDateTime(Nine):g} through onChanged"));
        }
        finally
        {
            DevelopmentNotices.Sink = null;
        }
    }

    /// <summary>A form that starts drawing its own labels on a redraw, the picker's leaving the drawing.</summary>
    private sealed class LabelsForm : StatefulComponent
    {
        private bool _own;

        public override VisualNode Build(ComponentContext context) =>
            Column(children:
            [
                _own ? Text("Delivery day") : Spacer(),
                DatePicker(null, _ => { }, label: "Delivery day", labelHidden: _own),
                Button("Own labels", onPressed: () => SetState(() => _own = true)),
            ]);
    }

    [Fact]
    public void ALabelHiddenOnARedraw_LeavesTheDrawing()
    {
        using var tester = new ComponentTester();
        tester.Pump(new LabelsForm());

        tester.Tap("Own labels");
        tester.Pump();

        tester.Semantics.Count(stop => stop.Name == "Delivery day" && stop.Role == SemanticRole.StaticText)
            .Should().Be(1, "the picker the form drew again carries the hidden label, and only the form's is drawn");
    }

    [Fact]
    public void APickerWhoseLabelIsHidden_StillNamesItsField_AndDrawsNoLabel()
    {
        using var tester = new ComponentTester();
        tester.Pump(Column(children:
        [
            Text("Delivery day"),
            new Components.DatePicker(null, _ => { }, label: "Delivery day") { LabelHidden = true },
        ]));

        tester.Semantics.Count(stop => stop.Name == "Delivery day" && stop.Role == SemanticRole.StaticText)
            .Should().Be(1, "the form's own label is the only one drawn");
        tester.Semantics.Should().Contain(stop => stop.Name == "Delivery day" && stop.Role != SemanticRole.StaticText,
            "the field is still named for assistive technology");
    }
}
