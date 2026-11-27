using System.Runtime.CompilerServices;
using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web.Tests.Fixtures;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The focus traversal, written once (#1897): every case of <c>focus-traversal.fixture.json</c> runs here against the C#
/// <see cref="FocusManager"/>, which Photon runs, and in <c>focus-traversal.spec.ts</c> against its transpiled twin,
/// which the browser runs, with <see cref="FocusProbe"/> as the code an app writes around a node (a handler added to
/// <c>Changed</c> that removes another, an <c>onFocusChange</c>). Each step says where the walk or the request
/// landed, what every node heard while it ran, and where the focus stands afterwards, so the two sides agree on
/// every answer of the manager, and on how a C# event dispatches while a handler changes it.
/// </summary>
public class FocusTraversalFixtureTests
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
    public void EveryCaseOfTheFixture_IsAnsweredAsTheBrowserAnswersIt(string name)
    {
        var testCase = Fixture().GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == name);
        var run = new Run(testCase);
        var index = 0;
        foreach (var step in testCase.GetProperty("steps").EnumerateArray())
        {
            run.Step(step);
            run.Check(step, $"step {index} of \"{name}\": {step}");
            index++;
        }
    }

    /// <summary>
    /// The two answers design.md "Measurements" measured on .NET with <c>dotnet fsi</c>: the handlers subscribed when
    /// a dispatch began are the ones it calls, and one <c>-=</c> removes one of a handler added twice. The cases above run
    /// them on the language itself, through <see cref="FocusProbe"/> and a real C# event; this pins that the fixture says
    /// what was measured, so the twin is held to it.
    /// </summary>
    [Fact]
    public void TheTwoMeasuredAnswersOfAnEvent_AreCasesOfTheFixture()
    {
        var cases = Fixture().GetProperty("cases").EnumerateArray().ToList();
        Heard(cases.Single(c => c.GetProperty("name").GetString()!.StartsWith("a handler that removes the next one")))
            .Should().Equal("H1,H2,n.onFocusChange true", "H1,H3,n.onFocusChange false");
        Heard(cases.Single(c => c.GetProperty("name").GetString()!.StartsWith("a handler added twice")))
            .Should().Equal("H,H,n.onFocusChange true", "H,n.onFocusChange false");

        static IEnumerable<string> Heard(JsonElement testCase) => testCase.GetProperty("steps").EnumerateArray()
            .Where(step => step.TryGetProperty("heard", out _))
            .Select(step => string.Join(",", step.GetProperty("heard").EnumerateArray().Select(h => h.GetString())));
    }

    [Fact]
    public void TheFixture_HoldsTheCasesTheSpecNames() =>
        Fixture().GetProperty("cases").GetArrayLength().Should().BeGreaterThanOrEqualTo(40);

    /// <summary>A place the focus can stand, named as the fixture names it.</summary>
    private sealed class Spot(string name)
    {
        public string Name { get; } = name;
    }

    /// <summary>One case: the nodes, the manager, the frame it was last given and what was heard.</summary>
    private sealed class Run
    {
        private readonly Dictionary<string, FocusNode> _nodes = [];
        private readonly Dictionary<string, FocusProbe> _probes = [];
        private readonly Dictionary<string, Spot> _spots = [];
        private readonly List<string> _heard = [];
        private string? _move;
        private bool? _returns;
        private string? _focus;
        private string[] _hasFocus = [];
        private string? _primary;

        public Run(JsonElement testCase)
        {
            Manager = new FocusManager(testCase.TryGetProperty("view", out var view) && view.GetString() == "leaves");
            Manager.RequestAsked += () => _heard.Add("asked");
            foreach (var node in testCase.GetProperty("nodes").EnumerateObject())
            {
                var spec = node.Value;
                FocusNode made = spec.TryGetProperty("scope", out var scope) && scope.GetBoolean()
                    ? new FocusScopeNode { TraversalEdgeBehavior = Edge(spec) }
                    : new FocusNode();
                if (spec.TryGetProperty("canRequestFocus", out var can)) made.CanRequestFocus = can.GetBoolean();
                if (spec.TryGetProperty("skipTraversal", out var skip)) made.SkipTraversal = skip.GetBoolean();
                var probe = new FocusProbe(node.Name, _heard, made);
                if (spec.TryGetProperty("handlers", out var handlers))
                    foreach (var handler in handlers.EnumerateArray()) Add(probe, handler);
                else probe.Watch();
                _nodes[node.Name] = made;
                _probes[node.Name] = probe;
            }
            if (testCase.TryGetProperty("frame", out var frame)) Attach(frame);
        }

        public FocusManager Manager { get; }

        public void Step(JsonElement step)
        {
            _heard.Clear();
            _move = null;
            _returns = null;
            if (step.TryGetProperty("attach", out var attach)) Attach(attach);
            else if (step.TryGetProperty("tab", out _)) Land(Manager.Traverse(true));
            else if (step.TryGetProperty("shiftTab", out _)) Land(Manager.Traverse(false));
            else if (step.TryGetProperty("focused", out var focused)) Manager.Focused(PositionOf(focused));
            else if (step.TryGetProperty("request", out var request)) _nodes[request.GetString()!].RequestFocus();
            else if (step.TryGetProperty("unfocus", out var unfocus)) _nodes[unfocus.GetString()!].Unfocus();
            else if (step.TryGetProperty("next", out var next)) _returns = _nodes[next.GetString()!].NextFocus();
            else if (step.TryGetProperty("previous", out var previous)) _returns = _nodes[previous.GetString()!].PreviousFocus();
            else if (step.TryGetProperty("apply", out _)) Applied(Manager.TakeRequest());
            else if (step.TryGetProperty("add", out var add)) Add(_probes[add.GetString()!], step);
            else if (step.TryGetProperty("remove", out var remove)) _probes[remove.GetString()!].Remove(step.GetProperty("handler").GetString()!);
            else if (step.TryGetProperty("knob", out var knob)) Knob(_nodes[knob.GetString()!], step);
            else throw new InvalidOperationException($"A step the runner does not know: {step}");
        }

        /// <summary>What the step answered and what stands after it: whatever the step does not say is what the step
        /// before it said, so every step asserts all of it.</summary>
        public void Check(JsonElement step, string because)
        {
            if (step.TryGetProperty("focus", out var focus)) _focus = focus.ValueKind == JsonValueKind.Null ? null : focus.GetString();
            if (step.TryGetProperty("hasFocus", out var has)) _hasFocus = has.EnumerateArray().Select(n => n.GetString()!).Order(StringComparer.Ordinal).ToArray();
            if (step.TryGetProperty("primary", out var primary)) _primary = primary.ValueKind == JsonValueKind.Null ? null : primary.GetString();

            _heard.Should().Equal(step.TryGetProperty("heard", out var heard) ? heard.EnumerateArray().Select(h => h.GetString()!) : [], because);
            if (step.TryGetProperty("move", out var move)) _move.Should().Be(move.GetString(), because);
            else _move.Should().BeNull(because + ": the step names no move");
            if (step.TryGetProperty("returns", out var returns)) _returns.Should().Be(returns.GetBoolean(), because);
            Name(Manager.PrimaryPosition).Should().Be(_focus, because + ": the position that holds the focus");
            _nodes.Where(node => node.Value.HasFocus).Select(node => node.Key).Order(StringComparer.Ordinal)
                .Should().Equal(_hasFocus, because + ": the nodes that have the focus");
            _nodes.FirstOrDefault(node => ReferenceEquals(node.Value, Manager.PrimaryFocus)).Key
                .Should().Be(_primary, because + ": the node that owns it");
        }

        /// <summary>A frame: the stops with the nodes each stands under, as the realizer's build would write them.</summary>
        private void Attach(JsonElement frame)
        {
            var autofocus = frame.TryGetProperty("autofocus", out var asking)
                ? asking.EnumerateArray().Select(n => n.GetString()!).ToHashSet()
                : [];
            var stops = new List<TraversalStop>();
            foreach (var stop in frame.GetProperty("stops").EnumerateArray())
            {
                var parts = stop.EnumerateArray().Select(part => part.GetString()!).ToList();
                var chain = parts.Skip(1)
                    .Select(node => new FocusAttachment(_nodes[node], _probes[node].OnFocusChange, autofocus.Contains(node)))
                    .ToList();
                stops.Add(new TraversalStop(SpotNamed(parts[0]), chain));
            }
            var modal = frame.TryGetProperty("modal", out var modalName) ? (FocusScopeNode)_nodes[modalName.GetString()!] : null;
            var modalAt = frame.TryGetProperty("modalAt", out var at) ? SpotNamed(at.GetString()!) : null;
            Manager.Attach(stops, modal, modalAt);
        }

        /// <summary>The realizer lands where the walk answered, and reports it: leaving the page leaves nothing focused.</summary>
        private void Land(FocusMove move)
        {
            _move = move.LeavesView ? "leaves" : move.To is null ? "clear" : $"to {Name(move.To)}";
            Manager.Focused(move.LeavesView ? null : move.To);
        }

        private void Applied(FocusMove? move)
        {
            if (move is { } made) Land(made);
            else _move = "none";
        }

        private static void Add(FocusProbe probe, JsonElement handler) =>
            probe.Add(handler.GetProperty(handler.TryGetProperty("name", out _) ? "name" : "handler").GetString()!,
                handler.TryGetProperty("removes", out var removes) ? removes.GetString() : null,
                handler.TryGetProperty("adds", out var adds) ? adds.GetString() : null);

        private static void Knob(FocusNode node, JsonElement step)
        {
            if (step.TryGetProperty("canRequestFocus", out var can)) node.CanRequestFocus = can.GetBoolean();
            if (step.TryGetProperty("skipTraversal", out var skip)) node.SkipTraversal = skip.GetBoolean();
        }

        private static TraversalEdgeBehavior Edge(JsonElement spec) =>
            !spec.TryGetProperty("edge", out var edge) ? TraversalEdgeBehavior.ClosedLoop : edge.GetString() switch
            {
                "parentScope" => TraversalEdgeBehavior.ParentScope,
                "stop" => TraversalEdgeBehavior.Stop,
                _ => TraversalEdgeBehavior.ClosedLoop,
            };

        private object? PositionOf(JsonElement name) => name.ValueKind == JsonValueKind.Null ? null : SpotNamed(name.GetString()!);

        private Spot SpotNamed(string name)
        {
            if (!_spots.TryGetValue(name, out var spot)) _spots[name] = spot = new Spot(name);
            return spot;
        }

        private static string? Name(object? position) => (position as Spot)?.Name;
    }

    private static JsonElement Fixture()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath()));
        return document.RootElement.Clone();
    }

    private static string FixturePath([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "eQuantic.UI.Runtime", "src",
            "shared", "focus-traversal.fixture.json"));
}
