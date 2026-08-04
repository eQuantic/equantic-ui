namespace eQuantic.UI.Native.Components;

/// <summary>
/// What the mouse pointer looks like over a surface. Deliberately small: these are the shapes that
/// MEAN something to the person moving the mouse — this can be pressed, this can be typed into,
/// this will do nothing — rather than a catalogue of what each platform happens to ship.
/// <para>
/// The names are the CSS ones, because the web realization of the same tree emits exactly these
/// and a second vocabulary for the same four ideas would be a second thing to keep in step.
/// </para>
/// </summary>
public enum CursorShape
{
    Default,
    Pointer,
    Text,
    NotAllowed,
}
