using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using eQuantic.Console;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web.Tests.Fixtures;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The arena, written once (#1563): every case of <c>gesture-arena.fixture.json</c> runs here against the C#
/// <see cref="GestureRouter"/>, which Photon runs, and in <c>gesture-arena.spec.ts</c> against its transpiled twin,
/// which the browser runs, with <see cref="ArenaProbe"/> and <see cref="ArenaMember"/> as the members on both sides,
/// and the dashboard's own <see cref="HoldThenDrag"/>, compiled here from the sample's file and transpiled there from it.
/// Each step says what every member heard while it ran, so the two sides agree on who wins, and at which step.
/// </summary>
public class GestureArenaFixtureTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in Fixture().GetProperty("cases").EnumerateArray())
            data.Add(testCase.GetProperty("name").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryCaseOfTheFixture_IsHeardAsTheBrowserHearsIt(string name)
    {
        var testCase = Fixture().GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == name);
        var run = new Run(testCase.GetProperty("members"));

        run.Heard.Should().Equal(Strings(testCase.GetProperty("built")), "building the detectors makes each recognizer once");
        var index = 0;
        foreach (var step in testCase.GetProperty("steps").EnumerateArray())
        {
            run.Heard.Clear();
            run.Step(step);
            run.Heard.Should().Equal(Strings(step.GetProperty("heard")), $"step {index} of \"{name}\": {step}");
            if (step.TryGetProperty("next", out var next))
                run.Router.NextDeadlineMs.Should().Be(next.ValueKind == JsonValueKind.Null ? null : next.GetDouble(),
                    $"the next deadline after step {index} of \"{name}\"");
            index++;
        }
    }

    /// <summary>
    /// Flutter's <c>canUpdate</c>: a place whose factory changes type gets a new recognizer, made and initialized by the
    /// new factory, and the old one is never handed to it. This is the C# answer, which Photon runs, and
    /// <c>gesture-arena.spec.ts</c> runs the same code transpiled, where the construction carries its type argument,
    /// as an app's does (<c>AGestureRecognizerOfTheAppsOwnTests</c>).
    /// </summary>
    [Fact]
    public void APlaceWhoseFactoryChangesType_GetsANewRecognizer() =>
        FactoryTypeChange.Run().Should().Equal("probe.create", "probe.initialize", "drag.create", "drag.initialize its own", "made new");

    [Fact]
    public void TheFixture_HoldsTheCasesTheSpecNames() =>
        Fixture().GetProperty("cases").GetArrayLength().Should().BeGreaterThanOrEqualTo(18);

    /// <summary>One case: the members, the router and the detectors it keeps, and what was heard.</summary>
    private sealed class Run
    {
        private readonly List<(string Name, JsonElement Spec)> _members = [];
        private readonly Dictionary<string, Point> _origins = [];
        private readonly Dictionary<string, ArenaProbe> _probes = [];
        private readonly Dictionary<string, IGestureArenaMember> _plain = [];
        private readonly Dictionary<string, HoldThenDrag> _samples = [];
        private readonly Dictionary<string, GestureArenaEntry> _entries = [];
        private int _build;

        public Run(JsonElement members)
        {
            foreach (var member in members.EnumerateArray())
            {
                var memberName = member.GetProperty("name").GetString()!;
                _members.Add((memberName, member));
                _origins[memberName] = member.TryGetProperty("origin", out var origin)
                    ? new Point(origin[0].GetSingle(), origin[1].GetSingle())
                    : Point.Zero;
                if (member.TryGetProperty("plain", out var plain) && plain.GetBoolean())
                    _plain[memberName] = new ArenaMember(memberName, Heard);
            }
            Build();
        }

        public List<string> Heard { get; } = [];

        public GestureRouter Router { get; } = new();

        public void Step(JsonElement step)
        {
            if (step.TryGetProperty("down", out var down)) Down(step, At(down));
            else if (step.TryGetProperty("move", out var move))
                Router.Route(Event(PointerPhase.Move, At(move), 1), InBox(PointerPhase.Move, At(move), PointerButtons.Primary));
            else if (step.TryGetProperty("up", out var up)) Router.Route(Event(PointerPhase.Up, At(up), 0), InBox(PointerPhase.Up, At(up)));
            else if (step.TryGetProperty("cancel", out var cancel)) Router.Route(Event(PointerPhase.Cancel, At(cancel), 0), InBox(PointerPhase.Cancel, At(cancel)));
            else if (step.TryGetProperty("tick", out var tick)) Router.Tick(tick.GetDouble());
            else if (step.TryGetProperty("rebuild", out _)) Build();
            else if (step.TryGetProperty("stop", out var stop)) _probes[stop.GetString()!].Stop(1);
            else if (step.TryGetProperty("resolve", out var resolve))
            {
                var disposition = Disposition(step.GetProperty("as"));
                var who = resolve.GetString()!;
                if (_probes.TryGetValue(who, out var probe)) probe.Say(disposition);
                else _entries[who].Resolve(disposition);
            }
            else throw new InvalidOperationException($"A step the runner does not know: {step}");
        }

        /// <summary>Each detector is built again: the router keeps its recognizer and hands it this build's callback.</summary>
        private void Build()
        {
            var build = ++_build;
            foreach (var (memberName, spec) in _members)
            {
                if (_plain.ContainsKey(memberName)) continue;
                if (spec.TryGetProperty("sample", out var sample) && sample.GetBoolean())
                {
                    _samples[memberName] = (HoldThenDrag)Router.Recognizers(memberName, [Sample(memberName)]).Single();
                    continue;
                }
                var factory = new GestureRecognizerFactoryWithHandlers<ArenaProbe>(
                    () =>
                    {
                        Heard.Add($"{memberName}.create");
                        return Probe(memberName, spec);
                    },
                    probe => probe.OnDeadline = who => Heard.Add($"{who}.called by build {build}"));
                _probes[memberName] = (ArenaProbe)Router.Recognizers(memberName, [factory]).Single();
            }
        }

        private void Down(JsonElement step, Point at)
        {
            var buttons = step.TryGetProperty("buttons", out var held) ? (PointerButtons)held.GetInt32() : PointerButtons.Primary;
            var members = _members
                .Select(member => _plain.TryGetValue(member.Name, out var plain) ? plain
                    : _samples.TryGetValue(member.Name, out var sample) ? sample
                    : _probes[member.Name])
                .ToList();
            var entries = Router.Down(Event(PointerPhase.Down, at, buttons), members, InBox(PointerPhase.Down, at, buttons));
            for (var i = 0; i < members.Count; i++)
                if (entries[i] is { } entry && _plain.ContainsKey(_members[i].Name)) _entries[_members[i].Name] = entry;
        }

        /// <summary>The event in a detector's box: the detector is named like its probe, and its box starts at the
        /// probe's origin.</summary>
        private Func<object, PointerEvent> InBox(PointerPhase phase, Point at, PointerButtons buttons = PointerButtons.None) =>
            position =>
            {
                var origin = _origins[(string)position];
                return Event(phase, new Point(at.X - origin.X, at.Y - origin.Y), buttons);
            };

        private static PointerEvent Event(PointerPhase phase, Point at, PointerButtons buttons) =>
            new(phase, at, PointerKind.Touch, buttons, KeyModifiers.None, 1, Point.Zero, 0);

        private static PointerEvent Event(PointerPhase phase, Point at, int buttons) => Event(phase, at, (PointerButtons)buttons);

        /// <summary>The dashboard's recognizer, built as its screen builds it: each callback says what it reported.</summary>
        private GestureRecognizerFactoryWithHandlers<HoldThenDrag> Sample(string memberName) => new(
            () =>
            {
                Heard.Add($"{memberName}.create");
                return new HoldThenDrag();
            },
            drag =>
            {
                drag.OnHeld = () => Heard.Add($"{memberName}.held");
                drag.OnMoved = offset => Heard.Add($"{memberName}.moved {Pair(offset)}");
                drag.OnReleased = offset => Heard.Add($"{memberName}.released {Pair(offset)}");
                drag.OnCancelled = () => Heard.Add($"{memberName}.cancelled");
            });

        private static string Pair(Point offset) => string.Create(CultureInfo.InvariantCulture, $"{offset.X},{offset.Y}");

        private ArenaProbe Probe(string memberName, JsonElement spec) => new(memberName, Heard,
            deadlineMs: spec.TryGetProperty("deadline", out var deadline) ? deadline.GetDouble() : 0,
            atDeadline: spec.TryGetProperty("atDeadline", out var atDeadline) ? Disposition(atDeadline) : null,
            onAdd: spec.TryGetProperty("onAdd", out var onAdd) ? Disposition(onAdd) : null,
            onMove: spec.TryGetProperty("onMove", out var onMove) ? Disposition(onMove) : null,
            primaryOnly: spec.TryGetProperty("primaryOnly", out var primaryOnly) && primaryOnly.GetBoolean(),
            thenDeadlineMs: spec.TryGetProperty("thenDeadline", out var thenDeadline) ? thenDeadline.GetDouble() : 0);
    }

    private static GestureDisposition Disposition(JsonElement value) =>
        value.GetString() == "accepted" ? GestureDisposition.Accepted : GestureDisposition.Rejected;

    private static Point At(JsonElement pair) => new(pair[0].GetSingle(), pair[1].GetSingle());

    private static IEnumerable<string> Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!);

    private static JsonElement Fixture()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath()));
        return document.RootElement.Clone();
    }

    private static string FixturePath([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "eQuantic.UI.Runtime", "src",
            "shared", "gesture-arena.fixture.json"));
}
