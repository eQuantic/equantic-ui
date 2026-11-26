namespace eQuantic.UI.Primitives;

/// <summary>
/// How a <see cref="RawGestureDetector"/> makes one of its recognizers and hands it what the latest build wrote
/// (#1563): Flutter's <c>GestureRecognizerFactory</c>. The list a detector holds is of this type because a C#
/// generic is invariant, so a <see cref="GestureRecognizerFactoryWithHandlers{T}"/> of the app's recognizer is not
/// one of <see cref="GestureRecognizer"/>; every factory is a <see cref="GestureRecognizerFactoryWithHandlers{T}"/>.
/// </summary>
public abstract class GestureRecognizerFactory
{
    private protected GestureRecognizerFactory()
    {
    }

    /// <summary>A new recognizer, made once for a place of a detector's position.</summary>
    internal abstract GestureRecognizer Create();

    /// <summary>Hands the kept <paramref name="recognizer"/> what the latest build wrote.</summary>
    internal abstract void Initialize(GestureRecognizer recognizer);
}
