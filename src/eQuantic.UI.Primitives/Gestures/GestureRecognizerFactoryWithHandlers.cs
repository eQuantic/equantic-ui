namespace eQuantic.UI.Primitives;

/// <summary>
/// A recognizer of type <typeparamref name="T"/> for a <see cref="RawGestureDetector"/> (#1563): Flutter's
/// <c>GestureRecognizerFactoryWithHandlers</c>. <c>create</c> runs once for the detector's place, and
/// <c>initialize</c> runs on the kept recognizer with every build, handing it the callbacks that build wrote: a
/// node is rebuilt constantly, and the recognizer, in the middle of a gesture, must live on.
/// <code>
/// new GestureRecognizerFactoryWithHandlers&lt;HoldThenDrag&gt;(() =&gt; new(), drag =&gt; drag.OnMoved = Move)
/// </code>
/// </summary>
/// <typeparam name="T">The recognizer it makes. A place handed a factory of another type makes a new one.</typeparam>
public sealed class GestureRecognizerFactoryWithHandlers<T> : GestureRecognizerFactory
    where T : GestureRecognizer
{
    private readonly Func<T> _create;
    private readonly Action<T> _initialize;

    /// <param name="create">Makes the recognizer, once for the detector's place.</param>
    /// <param name="initialize">Hands the kept recognizer what the latest build wrote. It runs at every build, and a
    /// realizer may run it again before the recognizer hears a pointer (Photon at every frame, the browser at every
    /// pointer event), so it hands callbacks and settings and does nothing that counts how often it ran.</param>
    public GestureRecognizerFactoryWithHandlers(Func<T> create, Action<T> initialize)
    {
        _create = create;
        _initialize = initialize;
    }

    internal override GestureRecognizer Create() => _create();

    internal override void Initialize(GestureRecognizer recognizer) => _initialize((T)recognizer);
}
