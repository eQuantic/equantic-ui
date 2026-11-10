using System.Globalization;

namespace eQuantic.UI.Native.Testing;

/// <summary>
/// The tester's culture, as BOTH cultures a component reads, for the length of one call: a
/// component formats with <see cref="CultureInfo.CurrentCulture"/>, and its own labels come from
/// resources read through <see cref="CultureInfo.CurrentUICulture"/>. The caller's pair comes back
/// when the call ends, so a test that names <c>pt-BR</c> leaves nothing behind for the next.
/// <para>
/// The web tester holds the same scope. It is written twice because no shipped assembly grants its
/// internals to another, and a public type for twenty lines of plumbing would be surface for nothing.
/// </para>
/// </summary>
internal readonly struct CultureScope : IDisposable
{
    private readonly CultureInfo _culture;
    private readonly CultureInfo _uiCulture;

    internal CultureScope(CultureInfo culture)
    {
        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
