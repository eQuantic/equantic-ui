using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A fact whose SUBJECT is the host's culture tables — day names, a time pattern, where a currency
/// puts its minus sign. These are ICU's answers, not ours, and the .NET builds on macOS, Linux and
/// Windows do not carry the same ICU: <c>ar-EG</c>'s Sunday is "الأحد" on macOS and "أحد" on the
/// Linux runner, and <c>en-US</c>'s long time pattern gains or loses its seconds.
/// <para>
/// So a fixture GENERATED from one host and compared on another fails for a reason that is no
/// defect of this repository, and cannot be fixed by regenerating it — whichever host writes it,
/// the other two disagree. Fenced here, visibly and by name, rather than filtered out of the
/// workflow where the fact would be invisible to whoever runs the tests.
/// </para>
/// <para>
/// What the fence is NOT: a reason to stop asking the question. The fixture is what the TypeScript
/// twin asserts against, so it still has to be right — and the real answer is a decision about
/// where the SDK gets culture data from, which is open as issue #147.
/// </para>
/// </summary>
public sealed class CultureDataFactAttribute : FactAttribute
{
    public CultureDataFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
            Skip = "Pins the HOST's ICU culture data, and the fixture is generated on macOS — "
                + "the ICU that ships with .NET differs per OS, so this compares two ICU versions "
                + "rather than this repository's code. See issue #147.";
    }
}
