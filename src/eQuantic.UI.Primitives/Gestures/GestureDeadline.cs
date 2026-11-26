namespace eQuantic.UI.Primitives;

/// <summary>
/// A time a recognizer waits for (#1563): how long, for which pointer, and when it is due, set by the first tick of
/// the realizer's clock after it was asked for, since a realizer's clock and a pointer's time need not share an
/// origin.
/// </summary>
internal sealed class GestureDeadline
{
    public GestureDeadline(GestureRecognizer recognizer, int pointer, double milliseconds)
    {
        Recognizer = recognizer;
        Pointer = pointer;
        Milliseconds = milliseconds;
    }

    public GestureRecognizer Recognizer { get; }

    public int Pointer { get; }

    public double Milliseconds { get; }

    /// <summary>When it is due on the realizer's clock, or null until the clock first ticks after the ask.</summary>
    public double? DueMs { get; set; }
}
