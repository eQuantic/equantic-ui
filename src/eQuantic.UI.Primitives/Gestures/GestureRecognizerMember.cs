namespace eQuantic.UI.Primitives;

/// <summary>
/// A recognizer as its arena sees it (#1563): the outcome reaches the recognizer through what the router must do
/// first (it stops hearing a pointer it lost, its deadline drops), whatever the app's override of
/// <see cref="GestureRecognizer.AcceptGesture"/> or <see cref="GestureRecognizer.RejectGesture"/> does.
/// </summary>
internal sealed class GestureRecognizerMember : IGestureArenaMember
{
    private readonly GestureRecognizer _recognizer;

    public GestureRecognizerMember(GestureRecognizer recognizer)
    {
        _recognizer = recognizer;
    }

    public void AcceptGesture(int pointer) => _recognizer.Won(pointer);

    public void RejectGesture(int pointer) => _recognizer.Lost(pointer);
}
