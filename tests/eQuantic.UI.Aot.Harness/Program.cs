using System.Globalization;
using eQuantic.UI.Components;

// Track L W6: run the SDK's own localized chrome under NativeAOT and fail loudly on the first
// answer that is not the translated one. A missing satellite does not throw — ResourceManager
// falls back to neutral English — so the only honest check is comparing against the expected
// TRANSLATIONS, never against "did it crash".
var expectations = new (string Culture, string Checked, string On, string Spreadsheet)[]
{
    ("en", "Find", "Dismiss", "Spreadsheet"),
    ("pt-BR", "Localizar", "Dispensar", "Planilha"),
    ("es", "Buscar", "Descartar", "Hoja de cálculo"),
    // The parent walk, under AOT: es-AR ships no satellite of its own and must land on es.
    ("es-AR", "Buscar", "Descartar", "Hoja de cálculo"),
};

var failures = 0;
foreach (var (culture, expectedChecked, expectedOn, expectedSheet) in expectations)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    Report(culture, nameof(SdkStrings.Find), SdkStrings.Find, expectedChecked);
    Report(culture, nameof(SdkStrings.Dismiss), SdkStrings.Dismiss, expectedOn);
    Report(culture, nameof(SdkStrings.Spreadsheet), SdkStrings.Spreadsheet, expectedSheet);
}

if (failures > 0)
{
    Console.Error.WriteLine($"AOT-SATELLITES-FAILED: {failures} answer(s) were not the translation.");
    return 1;
}

Console.WriteLine("AOT-SATELLITES-OK");
return 0;

void Report(string culture, string key, string actual, string expected)
{
    var ok = actual == expected;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "ok " : "FAIL")} [{culture}] {key} = \"{actual}\"{(ok ? "" : $" (expected \"{expected}\")")}");
}
