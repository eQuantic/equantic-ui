using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using eQuantic.UI.Components;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the browser's twin of <see cref="MoneyInput"/>, <see cref="NumberInput"/> and <see cref="Currency"/> must
/// answer, written from the C# that runs on the server (#1764): for each culture, the format data the server
/// installs, the money it writes, and what each field shows after each step of a script a person would type.
/// amount-inputs.spec.ts installs each culture and replays each script through the transpiled components in
/// the DOM. A PIN: the file is compared with what the C# answers now, and written only with
/// <c>EQ_UPDATE_AMOUNT_FIXTURE=1</c>, so a change of behaviour on this side is reviewed in the fixture's diff
/// before the other side is held to it.
/// </summary>
public class AmountInputsFixtureTests
{
    private const string UpdateVariable = "EQ_UPDATE_AMOUNT_FIXTURE";

    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "amount-inputs.fixture.json");
    }

    private sealed record Script(string Kind, string Code, long? Value, long? Min, long? Max, string Unit, string[] Steps);

    /// <summary>The scripts, each a field and the steps typed into it: <c>look</c>, <c>focus</c>, <c>blur</c>, or
    /// a text typed whole.</summary>
    private static readonly Script[] Scripts =
    [
        new("money", "EUR", 123450, null, null, "", ["look", "focus", "blur"]),
        new("money", "JPY", 1235, null, null, "", ["look"]),
        new("money", "EUR", -123450, null, null, "", ["look"]),
        new("money", "EUR", null, null, null, "",
            ["12.5", "12,5", "1,234", "1.234", "1 234,50", "1.234,50", "12,505", "abc", "-3,5", "", "7", "blur"]),
        new("money", "EUR", null, 1000, null, "", ["5", "10", "blur"]),
        new("money", "CHF", null, 0, 10000, "", ["200", "99.95", "blur"]),
        new("number", "", 3, 1, 30, "dias", ["look", "45", "12", "2,5", "1.000", "", "blur"]),
        new("number", "", 12500, null, null, "g", ["look", "focus", "1,500", "1 500", "blur"]),
    ];

    private static readonly long[] Amounts = [0, 5, 1250, 123450, -123450, 123456789012];

    private static Dictionary<string, object> Fixture() =>
        AmountInputsTests.Cultures.ToDictionary(culture => culture, culture => (object)new Dictionary<string, object>
        {
            // The culture's number format, as the server sends it: the rest of what it sends is no field's.
            ["numberFormat"] = JsonDocument.Parse(CultureFormatBridge.SerializeJson(new CultureInfo(culture)))
                .RootElement.GetProperty("numberFormat"),
            ["formats"] = new[] { Currency.Euro, Currency.Yen, Currency.SwissFranc }.SelectMany(currency => Amounts.Select(amount =>
                new Dictionary<string, object>
                {
                    ["currency"] = currency.Code,
                    ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                    ["text"] = AmountDriver.Under(culture, () => currency.Format(amount)),
                })).ToList(),
            ["scripts"] = Scripts.Select(script => Run(culture, script)).ToList(),
        });

    private static Dictionary<string, object?> Run(string culture, Script script)
    {
        var currency = new[] { Currency.Euro, Currency.Yen, Currency.SwissFranc }.FirstOrDefault(c => c.Code == script.Code);
        var driver = script.Kind == "money"
            ? AmountDriver.Money(culture, script.Value, currency!, script.Min, script.Max)
            : AmountDriver.Number(culture, (int?)script.Value, script.Unit, (int?)script.Min, (int?)script.Max);
        var seen = new List<object>();
        foreach (var step in script.Steps)
        {
            switch (step)
            {
                case "look": break;
                case "focus": driver.Focus(true); break;
                case "blur": driver.Focus(false); break;
                default: driver.Type(step); break;
            }
            var observed = driver.Observe();
            seen.Add(new Dictionary<string, object?>
            {
                ["value"] = observed.Value,
                ["name"] = observed.Name,
                ["invalid"] = observed.Invalid,
                ["caption"] = observed.Caption,
                ["prefix"] = observed.Prefix,
                ["suffix"] = observed.Suffix,
                ["handed"] = driver.Handed.Select(value => value?.ToString(CultureInfo.InvariantCulture)).ToList(),
            });
        }
        return new Dictionary<string, object?>
        {
            ["kind"] = script.Kind,
            ["currency"] = script.Code,
            ["value"] = script.Value?.ToString(CultureInfo.InvariantCulture),
            ["min"] = script.Min?.ToString(CultureInfo.InvariantCulture),
            ["max"] = script.Max?.ToString(CultureInfo.InvariantCulture),
            ["unit"] = script.Unit,
            ["steps"] = script.Steps,
            ["seen"] = seen,
        };
    }

    [Fact]
    public void TheTwinsFixture_IsWhatTheServerAnswers()
    {
        var path = FixturePath();
        var written = FixtureJson.Write(Fixture(), FixtureJson.Readable);
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, written);
            return;
        }

        File.Exists(path).Should().BeTrue($"the twin replays this fixture: write it with {UpdateVariable}=1");
        File.ReadAllText(path).Should().Be(written,
            $"the C# answers differently from the fixture the browser is held to: read the diff, then regenerate with {UpdateVariable}=1");
    }
}
