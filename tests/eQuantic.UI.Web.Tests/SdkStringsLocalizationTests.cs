using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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

    /// <summary>
    /// Every Portuguese-speaking country but Brazil writes European Portuguese, and .NET walks each of
    /// their cultures through the neutral <c>pt</c>, never through <c>pt-BR</c>: before the <c>pt</c>
    /// catalog, an app in <c>pt-PT</c> read every component of the SDK in English (#1221).
    /// </summary>
    [Theory]
    [InlineData("pt")]
    [InlineData("pt-PT")]
    [InlineData("pt-AO")]
    [InlineData("pt-MZ")]
    public void APortugueseUiCultureOutsideBrazil_AnnouncesInEuropeanPortuguese(string culture)
    {
        InCulture(culture, () => SdkStrings.UploadChooseFiles).Should().Be("Escolher ficheiros");
        InCulture(culture, () => SdkStrings.Spreadsheet).Should().Be("Folha de cálculo");
        InCulture(culture, () => SdkStrings.Dismiss).Should().Be("Dispensar");
    }

    [Fact]
    public void Brazil_KeepsItsOwnPortuguese()
    {
        InCulture("pt-BR", () => SdkStrings.UploadChooseFiles).Should().Be("Escolher arquivos");
        InCulture("pt-BR", () => SdkStrings.Spreadsheet).Should().Be("Planilha");
    }

    /// <summary>
    /// DERIVED from the files: every catalog beside the neutral one holds each of its keys and no
    /// other, with the same placeholders. A key missing from a translation does not fail anywhere else,
    /// since ResourceManager answers it from the neutral catalog, in English, in the middle of a
    /// Portuguese page; and a translation that drops a placeholder throws in <c>string.Format</c>, or
    /// silently drops the number.
    /// </summary>
    [Fact]
    public void EveryCatalog_HoldsEveryKeyOfTheNeutralOne_WithItsPlaceholders()
    {
        var directory = ComponentsDirectory();
        var neutral = Catalog(Path.Combine(directory, "SdkResources.resx"));
        var translations = Directory.GetFiles(directory, "SdkResources.*.resx").Order(StringComparer.Ordinal).ToList();
        translations.Select(Path.GetFileName).Should().Contain(["SdkResources.es.resx", "SdkResources.pt.resx", "SdkResources.pt-BR.resx"]);

        var offences = new List<string>();
        foreach (var path in translations)
        {
            var culture = Path.GetFileNameWithoutExtension(path)["SdkResources.".Length..];
            var catalog = Catalog(path);
            foreach (var key in neutral.Keys.Except(catalog.Keys)) offences.Add($"{culture}: {key} is missing");
            foreach (var key in catalog.Keys.Except(neutral.Keys)) offences.Add($"{culture}: {key} is not a key of the neutral catalog");
            foreach (var key in neutral.Keys.Intersect(catalog.Keys).Where(key => !Placeholders(neutral[key]).SetEquals(Placeholders(catalog[key]))))
                offences.Add($"{culture}: {key} has the placeholders {string.Join(" ", Placeholders(catalog[key]).Order())}, "
                    + $"and the neutral one {string.Join(" ", Placeholders(neutral[key]).Order())}");
        }

        string.Join("\n", offences).Should().BeEmpty("a catalog of the SDK translates every string the neutral one has");

        static Dictionary<string, string> Catalog(string path) =>
            XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "");

        static HashSet<string> Placeholders(string value) =>
            Regex.Matches(value, @"\{\d+[^}]*\}").Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
    }

    private static string ComponentsDirectory([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..", "src", "eQuantic.UI.Components"));

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

        foreach (var culture in new[] { "en", "pt-BR", "pt", "pt-PT", "es" })
        foreach (var property in properties)
        {
            var value = InCulture(culture, () => (string?)property.GetValue(null));
            value.Should().NotBeNullOrWhiteSpace(
                $"SdkStrings.{property.Name} must resolve in '{culture}'");
        }
    }
}
