using System.Globalization;
using eQuantic.UI.Components;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The SDK localizes its OWN chrome (I18N-PLAN D14) — the promise that an app with zero resx of
/// its own still announces "Marcado" to a pt-BR screen reader. On server and native that is the
/// real <c>ResourceManager</c> over satellite assemblies, which is what these assert; the web half
/// rides eqc's accessor rewrite and is pinned by the transpilation fixture (SdkStrings.ts carries
/// <c>$eq.str("SdkResources", …)</c>, never a literal).
/// </summary>
public class SdkStringsLocalizationTests
{
    private static T InCulture<T>(string culture, Func<T> read)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void TheNeutralCulture_SpeaksEnglish()
    {
        InCulture("en", () => SdkStrings.Find).Should().Be("Find");
        InCulture("en", () => SdkStrings.SearchPlaceholder).Should().Be("Search…");
    }

    [Fact]
    public void APortugueseUiCulture_AnnouncesInPortuguese()
    {
        InCulture("pt-BR", () => SdkStrings.Find).Should().Be("Localizar");
        InCulture("pt-BR", () => SdkStrings.SearchPlaceholder).Should().Be("Pesquisar…");
        InCulture("pt-BR", () => SdkStrings.Dismiss).Should().Be("Dispensar");
    }

    [Fact]
    public void ASpanishUiCulture_AnnouncesInSpanish()
    {
        InCulture("es", () => SdkStrings.Dismiss).Should().Be("Descartar");
        InCulture("es", () => SdkStrings.Spreadsheet).Should().Be("Hoja de cálculo");
    }

    [Fact]
    public void ASpecificCulture_FallsBackToItsParentsTranslation()
    {
        // es-AR ships no satellite of its own; .NET walks es-AR → es → neutral, and the SDK gets
        // that walk for free precisely because it uses ResourceManager rather than a map.
        InCulture("es-AR", () => SdkStrings.Dismiss).Should().Be("Descartar");
    }

    [Fact]
    public void AnUntranslatedCulture_FallsBackToNeutral_RatherThanBlank()
    {
        // A missing translation degrades to English, never to empty — the missing-key policy the
        // plan states for the client, holding on the server for the same reason.
        InCulture("ja-JP", () => SdkStrings.Find).Should().Be("Find");
    }

    [Fact]
    public void EveryPropertyResolves_InEveryShippedCulture()
    {
        // The whole seam, swept: a key added to the class but not to a resx would come back null
        // and surface as a blank announcement somewhere far from here.
        var properties = typeof(SdkStrings).GetProperties();
        // 8, down from 13: Checked/Unchecked/PartlySelected/On/Off left when a check's state moved
        // into aria-checked / SemanticNode.Checked — the platform announces state in the user's
        // language, which no resx of ours could match. The floor still guards the sweep being real.
        properties.Should().HaveCountGreaterThan(5, "the audited SDK string set");

        foreach (var culture in new[] { "en", "pt-BR", "es" })
        foreach (var property in properties)
        {
            var value = InCulture(culture, () => (string?)property.GetValue(null));
            value.Should().NotBeNullOrWhiteSpace(
                $"SdkStrings.{property.Name} must resolve in '{culture}'");
        }
    }
}
