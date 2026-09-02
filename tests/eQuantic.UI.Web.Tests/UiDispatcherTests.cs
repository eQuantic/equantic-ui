using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// State published from a WORKER thread stops racing the thread that draws.
/// <para>
/// `SetState` was `mutate(); StateInvalidated?.Invoke();` — no barrier at all. On the web that is
/// correct and always was (one thread), but on every native target the render loop runs on the
/// platform's main thread, so a scanner or a download finishing on a pool thread mutated a
/// component's fields while that very tree was being built: a frame drawn from half-old state, a
/// collection enumerated while it grows, no exception to find it by.
/// </para>
/// </summary>
public class UiDispatcherTests
{
    private sealed class Counter : StatefulComponent
    {
        public int Value;
        public int Invalidations;

        public Counter() => StateInvalidated += () => Invalidations++;

        public void Bump() => SetState(() => Value++);

        /// <summary>Any mutation, so a test can watch the ORDER the two halves run in.</summary>
        public void SetStateForTest(Action mutate) => SetState(mutate);

        public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
    }

    /// <summary>A dispatcher whose UI thread is whichever one built it — the shape every native
    /// host has, small enough to hold the whole contract in view.</summary>
    private sealed class FakeDispatcher : IUiDispatcher
    {
        private readonly int _uiThreadId = Environment.CurrentManagedThreadId;
        private readonly List<Action> _posted = [];

        public bool IsOnUiThread => Environment.CurrentManagedThreadId == _uiThreadId;
        public int PostedCount => _posted.Count;

        public void Post(Action work) => _posted.Add(work);

        public void Drain()
        {
            foreach (var work in _posted) work();
            _posted.Clear();
        }
    }

    private static void WithDispatcher(IUiDispatcher? dispatcher, Action body)
    {
        var outer = UiDispatcher.Current;
        UiDispatcher.Current = dispatcher;
        try { body(); }
        finally { UiDispatcher.Current = outer; }
    }

    /// <summary>Runs <paramref name="work"/> on a thread that is NOT this one, and rethrows whatever
    /// it threw — a failed assertion inside a worker is otherwise a test that passes.</summary>
    private static void OnAWorkerThread(Action work)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { work(); }
            catch (Exception e) { failure = e; }
        });
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    [Fact]
    public void OnTheUiThread_SetStateRunsInline_ExactlyAsItAlwaysDid()
    {
        var dispatcher = new FakeDispatcher();
        var counter = new Counter();

        WithDispatcher(dispatcher, counter.Bump);

        counter.Value.Should().Be(1, "the calling thread IS the UI thread — nothing to marshal");
        counter.Invalidations.Should().Be(1);
        dispatcher.PostedCount.Should().Be(0);
    }

    [Fact]
    public void WithNoDispatcher_SetStateRunsInline_TheWebAndSsrCase()
    {
        var counter = new Counter();

        WithDispatcher(null, () => OnAWorkerThread(counter.Bump));

        counter.Value.Should().Be(1, "where nothing needs marshalling, nothing is armed");
        counter.Invalidations.Should().Be(1);
    }

    [Fact]
    public void OffTheUiThread_TheMutationIsPostedAndRunsOnTheUiThread()
    {
        var dispatcher = new FakeDispatcher();
        var counter = new Counter();

        WithDispatcher(dispatcher, () =>
        {
            OnAWorkerThread(counter.Bump);

            // The worker returned having touched NOTHING: this is the race that used to happen
            // while the render thread was reading the same fields.
            counter.Value.Should().Be(0);
            counter.Invalidations.Should().Be(0);
            dispatcher.PostedCount.Should().Be(1);

            dispatcher.Drain();
        });

        counter.Value.Should().Be(1, "the mutation ran where the tree is built");
        counter.Invalidations.Should().Be(1, "and the frame it needs was asked for from there too");
    }

    [Fact]
    public void TheInvalidationTravelsWithTheMutation_NeverAheadOfIt()
    {
        // Posting only the invalidation would be worse than posting nothing: the render thread would
        // be TOLD to draw while the fields it draws from were still being written.
        var dispatcher = new FakeDispatcher();
        var order = new List<string>();
        var counter = new Counter();
        counter.StateInvalidated += () => order.Add("invalidated");

        WithDispatcher(dispatcher, () =>
        {
            OnAWorkerThread(() => counter.SetStateForTest(() => order.Add("mutated")));
            order.Should().BeEmpty();
            dispatcher.Drain();
        });

        order.Should().Equal("mutated", "invalidated");
    }
}
