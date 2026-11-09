using System.Globalization;

namespace eQuantic.UI.Testing;

/// <summary>
/// The tester's culture, as BOTH cultures a component reads, for the length of one call: a
/// component formats with <see cref="CultureInfo.CurrentCulture"/>, and its own labels come from
/// resources read through <see cref="CultureInfo.CurrentUICulture"/>. The caller's pair comes back
/// when the call ends, so a test that names <c>pt-BR</c> leaves nothing behind for the next.
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
