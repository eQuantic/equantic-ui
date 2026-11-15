using FluentAssertions;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An event declared with <c>add</c> and <c>remove</c> accessors lowers to them, as C# compiles <c>+=</c> and
/// <c>-=</c> on one into calls of its accessors (#746), both sides executed. The emitter knew only field-like
/// events, so such an event had no twin, and a subscription wrote a key of its name the accessors never read:
/// a handler subscribed through an interface, whose explicit event always has accessors, was never raised,
/// .NET counting 105 where the browser counted 5.
/// </summary>
public class EventAccessorConformanceTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;

        public interface IRinger
        {
            event Action Rung;
            void Ring();
        }

        // An explicit interface event: always declared with accessors.
        public sealed class Bell : IRinger
        {
            private Action? _rung;
            event Action IRinger.Rung { add => _rung += value; remove => _rung -= value; }
            public void Ring() => _rung?.Invoke();
        }

        // A field-like event behind the same interface, which a subscription through it still reaches.
        public sealed class Chime : IRinger
        {
            public event Action? Rung;
            public void Ring() => Rung?.Invoke();
        }

        // A class event with accessors of its own, which keep their handlers in a list.
        public sealed class Board
        {
            private readonly List<Action<int>> _handlers = new();
            public int Added;
            public event Action<int> Posted
            {
                add { Added++; _handlers.Add(value); }
                remove { _handlers.Remove(value); }
            }
            public void Post(int n) { foreach (var handler in _handlers) handler(n); }
            // Subscribed by its bare name, inside its own type.
            public void Listen(Action<int> handler) => Posted += handler;
        }

        public static class Hub
        {
            private static Action<string>? _said;
            public static int Subscribed;
            public static event Action<string> Said { add { Subscribed++; _said += value; } remove => _said -= value; }
            public static void Say(string text) => _said?.Invoke(text);
            public static void Listen(Action<string> handler) => Said += handler;
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a handler subscribed through an interface's explicit event",
            "var count = 5; var bell = new Bell(); IRinger ringer = bell; ringer.Rung += () => count += 100; bell.Ring(); return count;"),
        ("a handler removed through an interface's explicit event",
            "var count = 0; IRinger ringer = new Bell(); Action add = () => count++; ringer.Rung += add; ringer.Rung += add; ringer.Rung -= add; ringer.Ring(); return count;"),
        ("a field-like event subscribed through the same interface",
            "var count = 0; IRinger ringer = new Chime(); ringer.Rung += () => count += 7; ringer.Ring(); return count;"),
        ("a class event's own accessors",
            "var sum = 0; var board = new Board(); Action<int> add = n => sum += n; board.Posted += add; board.Posted += n => sum += n * 10; board.Post(2); board.Posted -= add; board.Post(3); return sum + \"|\" + board.Added;"),
        ("an event subscribed by its bare name inside its own type",
            "var sum = 0; var board = new Board(); board.Listen(n => sum += n); board.Post(4); return sum + \"|\" + board.Added;"),
        ("a static event subscribed by its bare name inside its own type",
            "var heard = \"\"; Hub.Listen(text => heard += text + \"!\"); Hub.Say(\"c\"); return heard;"),
        ("a static event's accessors",
            "var heard = \"\"; Hub.Said += text => heard += text; Hub.Say(\"a\"); Hub.Say(\"b\"); return heard + \"|\" + Hub.Subscribed;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEventWithAccessors_LowersToThem(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);

    /// <summary>A record's, a struct's and a component's event declared with accessors (#1445): only the
    /// plain class's twin wrote <c>$add$</c> and <c>$remove$</c>, so a subscription to one of these called a
    /// method its twin did not have and threw where .NET counted. Each declares one of its own, one an
    /// interface's explicitly and a static one.</summary>
    private const string EveryTwin = """
        using System;
        using eQuantic.UI.Primitives;

        public interface ITicker
        {
            event Action<int> Ticked;
            void Tick(int n);
        }

        public sealed record Signal(int Id) : ITicker
        {
            private Action? _fired;
            public event Action Fired { add => _fired += value; remove => _fired -= value; }
            public void Fire() => _fired?.Invoke();

            private Action<int>? _ticked;
            event Action<int> ITicker.Ticked { add => _ticked += value; remove => _ticked -= value; }
            public void Tick(int n) => _ticked?.Invoke(n);

            private static Action<int>? _counted;
            public static event Action<int> Counted { add => _counted += value; remove => _counted -= value; }
            public static void Count(int n) => _counted?.Invoke(n);
        }

        public struct Pulse : ITicker
        {
            private Action<int>? _beat;
            public event Action<int> Beat { add => _beat += value; remove => _beat -= value; }
            public void Fire(int n) => _beat?.Invoke(n);

            private Action<int>? _ticked;
            event Action<int> ITicker.Ticked { add => _ticked += value; remove => _ticked -= value; }
            public void Tick(int n) => _ticked?.Invoke(n);

            private static Action<int>? _counted;
            public static event Action<int> Counted { add => _counted += value; remove => _counted -= value; }
            public static void Count(int n) => _counted?.Invoke(n);
        }

        public sealed class Meter : StatefulComponent, ITicker
        {
            private Action<int>? _moved;
            public event Action<int> Moved { add => _moved += value; remove => _moved -= value; }
            public void Move(int n) => _moved?.Invoke(n);

            private Action<int>? _ticked;
            event Action<int> ITicker.Ticked { add => _ticked += value; remove => _ticked -= value; }
            public void Tick(int n) => _ticked?.Invoke(n);

            private static Action<int>? _counted;
            public static event Action<int> Counted { add => _counted += value; remove => _counted -= value; }
            public static void Count(int n) => _counted?.Invoke(n);

            public override VisualNode Build(ComponentContext context) => new Text("meter", TypeRole.BodyM);
        }
        """;

    private static readonly (string Name, string Statements)[] EveryTwinCases =
    [
        ("a record's event with accessors",
            "var count = 0; var s = new Signal(1); Action add = () => count++; s.Fired += add; s.Fired += () => count += 10; s.Fire(); s.Fired -= add; s.Fire(); return count;"),
        ("a struct's event with accessors",
            "var sum = 0; var p = new Pulse(); Action<int> add = n => sum += n; p.Beat += add; p.Beat += n => sum += n * 10; p.Fire(2); p.Beat -= add; p.Fire(3); return sum;"),
        ("a component's event with accessors",
            "var sum = 0; var m = new Meter(); Action<int> add = n => sum += n; m.Moved += add; m.Moved += n => sum += n * 10; m.Move(2); m.Moved -= add; m.Move(3); return sum;"),
        ("a record's explicit interface event",
            "var sum = 0; ITicker t = new Signal(1); Action<int> add = n => sum += n; t.Ticked += add; t.Ticked += n => sum += n * 10; t.Tick(2); t.Ticked -= add; t.Tick(3); return sum;"),
        ("a struct's explicit interface event",
            "var sum = 0; ITicker t = new Pulse(); Action<int> add = n => sum += n; t.Ticked += add; t.Ticked += n => sum += n * 10; t.Tick(2); t.Ticked -= add; t.Tick(3); return sum;"),
        ("a component's explicit interface event",
            "var sum = 0; ITicker t = new Meter(); Action<int> add = n => sum += n; t.Ticked += add; t.Ticked += n => sum += n * 10; t.Tick(2); t.Ticked -= add; t.Tick(3); return sum;"),
        ("a record's static event with accessors",
            "var sum = 0; Action<int> add = n => sum += n; Signal.Counted += add; Signal.Count(2); Signal.Counted -= add; Signal.Count(3); return sum;"),
        ("a struct's static event with accessors",
            "var sum = 0; Action<int> add = n => sum += n; Pulse.Counted += add; Pulse.Count(2); Pulse.Counted -= add; Pulse.Count(3); return sum;"),
        ("a component's static event with accessors",
            "var sum = 0; Action<int> add = n => sum += n; Meter.Counted += add; Meter.Count(2); Meter.Counted -= add; Meter.Count(3); return sum;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryTwinsEventWithAccessors_LowersToThem(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(EveryTwin, typeAnnotations, EveryTwinCases);

    /// <summary>A component's twin on each of its paths (#1445): an abstract base that holds only a field and
    /// an event took the path that writes nothing for a member it does not count, and a primitive, an
    /// <c>HtmlElement</c>, a path of its own.</summary>
    private const string EveryComponentPath = """
        using System;
        using eQuantic.UI.Primitives;
        using eQuantic.UI.Web;

        public abstract class Gauge : StatefulComponent
        {
            protected Action<int>? _read;
            public event Action<int> Read { add => _read += value; remove => _read -= value; }
        }

        public sealed class Dial : Gauge
        {
            public void Turn(int n) => _read?.Invoke(n);
            public override VisualNode Build(ComponentContext context) => new Text("dial", TypeRole.BodyM);
        }

        public sealed class Lamp : HtmlElement
        {
            private Action<int>? _lit;
            public event Action<int> Lit { add => _lit += value; remove => _lit -= value; }
            public void Light(int n) => _lit?.Invoke(n);
            public override HtmlNode Render() => new HtmlNode { Tag = "span" };
        }
        """;

    private static readonly (string Name, string Statements)[] EveryComponentPathCases =
    [
        ("an abstract component base's event with accessors",
            "var sum = 0; var d = new Dial(); Action<int> add = n => sum += n; d.Read += add; d.Read += n => sum += n * 10; d.Turn(2); d.Read -= add; d.Turn(3); return sum;"),
        ("a primitive component's event with accessors",
            "var sum = 0; var l = new Lamp(); Action<int> add = n => sum += n; l.Lit += add; l.Lit += n => sum += n * 10; l.Light(2); l.Lit -= add; l.Light(3); return sum;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryComponentPathsEventWithAccessors_LowersToThem(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(EveryComponentPath, typeAnnotations, EveryComponentPathCases);

    /// <summary>An event's accessor marked <c>[ServerOnly]</c>, or <c>[ServerAction]</c> on a component's, stays
    /// on the server in every twin (#1445): its body reads the file system, which a twin that held it would be
    /// refused for. The accessor beside it crosses, and a subscription through it runs.</summary>
    private const string KeptAccessors = """
        using System;
        using eQuantic.UI.Primitives;

        public sealed class Vault
        {
            private Action? _opened;
            public event Action Opened { add => _opened += value; [ServerOnly] remove { _ = System.IO.File.Exists("vault"); _opened -= value; } }
            public void Open() => _opened?.Invoke();
        }

        public sealed record Signal(int Id)
        {
            private Action? _fired;
            public event Action Fired { add => _fired += value; [ServerOnly] remove { _ = System.IO.File.Exists("signal"); _fired -= value; } }
            public void Fire() => _fired?.Invoke();
        }

        public struct Pulse
        {
            private Action? _beat;
            public event Action Beat { add => _beat += value; [ServerOnly] remove { _ = System.IO.File.Exists("pulse"); _beat -= value; } }
            public void Fire() => _beat?.Invoke();
        }

        public sealed class Meter : StatefulComponent
        {
            private Action? _moved;
            public event Action Moved { add => _moved += value; [ServerOnly] remove { _ = System.IO.File.Exists("meter"); _moved -= value; } }
            private Action? _served;
            public event Action Served { add => _served += value; [ServerAction] remove { _ = System.IO.File.Exists("served"); _served -= value; } }
            public void Move() { _moved?.Invoke(); _served?.Invoke(); }
            public override VisualNode Build(ComponentContext context) => new Text("meter", TypeRole.BodyM);
        }
        """;

    private static readonly (string Name, string Statements)[] KeptAccessorCases =
    [
        ("a class's event beside its server-only accessor",
            "var count = 0; var v = new Vault(); v.Opened += () => count += 3; v.Open(); return count;"),
        ("a record's event beside its server-only accessor",
            "var count = 0; var s = new Signal(1); s.Fired += () => count += 3; s.Fire(); return count;"),
        ("a struct's event beside its server-only accessor",
            "var count = 0; var p = new Pulse(); p.Beat += () => count += 3; p.Fire(); return count;"),
        ("a component's events beside their server-only accessors",
            "var count = 0; var m = new Meter(); m.Moved += () => count += 3; m.Served += () => count += 30; m.Move(); return count;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEventsServerOnlyAccessor_StaysOnTheServer(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(KeptAccessors, typeAnnotations, KeptAccessorCases);

    /// <summary>A null-conditional subscription, C# 14's <c>s?.Fired += h</c> (#1445): it went through the guarded
    /// assignment, which wrote <c>$t.fired += h</c>, never the event's accessors, so a handler subscribed so to an
    /// event with accessors was never raised. A null receiver subscribes nothing and evaluates the handler never.</summary>
    private const string NullConditional = """
        using System;
        using eQuantic.UI.Primitives;

        public sealed class Board
        {
            private Action<int>? _posted;
            public event Action<int> Posted { add => _posted += value; remove => _posted -= value; }
            public void Post(int n) => _posted?.Invoke(n);
        }

        public sealed record Signal(int Id)
        {
            private Action<int>? _fired;
            public event Action<int> Fired { add => _fired += value; remove => _fired -= value; }
            public void Fire(int n) => _fired?.Invoke(n);
        }

        public sealed class Meter : StatefulComponent
        {
            private Action<int>? _moved;
            public event Action<int> Moved { add => _moved += value; remove => _moved -= value; }
            public void Move(int n) => _moved?.Invoke(n);
            public override VisualNode Build(ComponentContext context) => new Text("meter", TypeRole.BodyM);
        }

        public static class Made
        {
            public static int Count;
            public static Action<int> Handler(Action<int> handler) { Count++; return handler; }
        }
        """;

    private static string NullConditionalCase(string construct, string type, string @event, string raise) =>
        $"var sum = 0; {type}? x = {construct}; Action<int> add = n => sum += n; x?.{@event} += add; "
        + $"x?.{@event} += Made.Handler(n => sum += n * 10); x?.{raise}(2); x?.{@event} -= add; x?.{raise}(3); return sum + \"|\" + Made.Count;";

    private static readonly (string Name, string Statements)[] NullConditionalCases =
    [
        ("a class's event subscribed through ?.", NullConditionalCase("new Board()", "Board", "Posted", "Post")),
        ("a class's event subscribed through ?. on null", NullConditionalCase("null", "Board", "Posted", "Post")),
        ("a record's event subscribed through ?.", NullConditionalCase("new Signal(1)", "Signal", "Fired", "Fire")),
        ("a record's event subscribed through ?. on null", NullConditionalCase("null", "Signal", "Fired", "Fire")),
        ("a component's event subscribed through ?.", NullConditionalCase("new Meter()", "Meter", "Moved", "Move")),
        ("a component's event subscribed through ?. on null", NullConditionalCase("null", "Meter", "Moved", "Move")),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullConditionalSubscription_LowersToTheEventsAccessors(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(NullConditional, typeAnnotations, NullConditionalCases);

    /// <summary>A subscription from client code that calls an accessor no twin holds is refused (EQ2010), as a
    /// call of a <c>[ServerOnly]</c> method is: it would call a method the browser does not have (#1445).</summary>
    [Theory]
    [InlineData("public sealed record Signal(int Id) { private Action? _f; public event Action Fired { add => _f += value; [eQuantic.UI.Primitives.ServerOnly] remove => _f -= value; } }",
        "public void Drop(Signal s, Action a) => s.Fired -= a;")]
    [InlineData("public struct Pulse { private Action? _f; public event Action Beat { [eQuantic.UI.Primitives.ServerOnly] add => _f += value; remove => _f -= value; } }",
        "public void Take(Pulse p, Action a) => p.Beat += a;")]
    [InlineData("public sealed class Meter : eQuantic.UI.Primitives.StatefulComponent { private Action? _f; public event Action Moved { [eQuantic.UI.Primitives.ServerAction] add => _f += value; remove => _f -= value; } public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) => new eQuantic.UI.Primitives.Text(\"m\", eQuantic.UI.Primitives.TypeRole.BodyM); }",
        "public void Take(Meter m, Action a) => m.Moved += a;")]
    public void ASubscriptionToAServerOnlyAccessor_IsRefused(string declaring, string member) =>
        Transpiler.AskTheBuild(declaring + "\npublic class Client { " + member + " }")
            .Should().Contain(refusal => refusal.Id == "EQ2010" && refusal.Module == "Client");
}
