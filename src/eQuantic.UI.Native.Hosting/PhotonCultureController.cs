using System.Globalization;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// The culture hand for Photon — the native half of I18N-PLAN's culture story (D5/D10/W6). On the
/// web the REQUEST sets the culture; in a window the PLATFORM does, and someone must copy it onto
/// .NET's statics: a GUI process launched outside a terminal has no <c>LANG</c>/<c>LC_*</c>
/// environment, so .NET starts invariant even on a pt-BR machine. The shell resolves the platform
/// locale and Applies it here; the controller owns BOTH statics — <c>CurrentUICulture</c> picks
/// resources, <c>CurrentCulture</c> picks formats, the D13 pair — and the attached sink repaints
/// the host so a switch shows on the next frame. The <see cref="PhotonThemeController"/> shape:
/// registered TryAdd, attached by the runner once the window's host exists, Apply-before-attach
/// just records. Track L's write-once culture switch (D6) gets THIS as its native realization.
/// </summary>
public sealed class PhotonCultureController
{
    private Action? _repaint;

    public CultureInfo UICulture { get; private set; } = CultureInfo.CurrentUICulture;
    public CultureInfo FormatCulture { get; private set; } = CultureInfo.CurrentCulture;

    /// <summary>Copies the pair onto the process and repaints if a host is attached. Null format
    /// means "the UI culture's own formats" — the common case, and what a single-locale platform
    /// (Android's <c>Locale.getDefault()</c>) reports anyway.</summary>
    public void Apply(CultureInfo uiCulture, CultureInfo? formatCulture = null)
    {
        var format = formatCulture ?? uiCulture;
        ApplyToProcess(uiCulture, format);
        UICulture = uiCulture;
        FormatCulture = format;
        _repaint?.Invoke();
    }

    /// <summary>Called by the runner: the hand that repaints (set once the window's host exists —
    /// services are constructed before any window is, so the sink arrives late by design).</summary>
    public void Attach(Action repaint) => _repaint = repaint;

    /// <summary>The statics write alone — for a shell that resolves the platform locale before any
    /// controller instance is reachable (iOS today: <c>PhotonApp.Run</c> does not carry services).
    /// Default-thread AND current-thread statics, so the render loop and every thread born later
    /// agree on what the user asked for.</summary>
    public static void ApplyToProcess(CultureInfo uiCulture, CultureInfo formatCulture)
    {
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = formatCulture;
        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.CurrentCulture = formatCulture;
    }
}
