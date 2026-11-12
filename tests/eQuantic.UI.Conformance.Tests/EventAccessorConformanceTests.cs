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
}
