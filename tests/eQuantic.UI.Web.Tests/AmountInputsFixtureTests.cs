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
/// <para>
/// The C# answers under the fixture's OWN number formats, as the browser does, never the host's: each host
/// reads its cultures from its own ICU, and ja-JP's currency symbol is <c>¥</c> on one and the full-width
/// <c>￥</c> on another, so a pin over the host's data failed on every machine but the one that wrote it. The
/// host's data is read once, when the fixture is written.
/// </para>
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

    /// <summary>The fixture, its cultures built by <paramref name="cultureOf"/>: the host's own when the
    /// fixture is written, the fixture's data when it is compared.</summary>
    private static Dictionary<string, object> Fixture(Func<string, CultureInfo> cultureOf) =>
        AmountInputsTests.Cultures.ToDictionary(name => name, name => (object)Case(cultureOf(name)));

    private static Dictionary<string, object> Case(CultureInfo culture) =>
        new()
        {
            // The culture's number format, as the server sends it: the rest of what it sends is no field's.
            ["numberFormat"] = JsonDocument.Parse(CultureFormatBridge.SerializeJson(culture))
                .RootElement.GetProperty("numberFormat"),
            ["formats"] = new[] { Currency.Euro, Currency.Yen, Currency.SwissFranc }.SelectMany(currency => Amounts.Select(amount =>
                new Dictionary<string, object>
                {
                    ["currency"] = currency.Code,
                    ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                    ["text"] = AmountDriver.Under(culture, () => currency.Format(amount)),
                })).ToList(),
            ["scripts"] = Scripts.Select(script => Run(culture, script)).ToList(),
        };

    /// <summary>
    /// The culture <paramref name="name"/> with the number format the fixture recorded for it: a writable copy
    /// of the host's, every scalar and group size set from the file, by .NET's own names.
    /// </summary>
    private static CultureInfo Recorded(string name, JsonElement numberFormat)
    {
        var culture = (CultureInfo)(name.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(name)).Clone();
        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        foreach (var member in numberFormat.EnumerateObject())
        {
            var property = typeof(NumberFormatInfo).GetProperty(member.Name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"the fixture names {member.Name}, which NumberFormatInfo has not");
            object value = member.Value.ValueKind switch
            {
                JsonValueKind.String => member.Value.GetString()!,
                JsonValueKind.Number => member.Value.GetInt32(),
                JsonValueKind.Array => member.Value.EnumerateArray().Select(size => size.GetInt32()).ToArray(),
                _ => throw new InvalidOperationException($"{member.Name} is {member.Value.ValueKind}"),
            };
            property.SetValue(format, value);
        }
        culture.NumberFormat = format;
        return culture;
    }

    private static Dictionary<string, object?> Run(CultureInfo culture, Script script)
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
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, FixtureJson.Write(Fixture(name => new CultureInfo(name)), FixtureJson.Readable));
            return;
        }

        File.Exists(path).Should().BeTrue($"the twin replays this fixture: write it with {UpdateVariable}=1");
        var committed = File.ReadAllText(path);
        using var recorded = JsonDocument.Parse(committed);
        var cultures = recorded.RootElement.EnumerateObject()
            .ToDictionary(culture => culture.Name, culture => culture.Value.GetProperty("numberFormat").Clone());
        cultures.Keys.Should().BeEquivalentTo(AmountInputsTests.Cultures, "every culture the drivers probe is in the fixture");
        committed.Should().Be(FixtureJson.Write(Fixture(name => Recorded(name, cultures[name])), FixtureJson.Readable),
            $"the C# answers differently from the fixture the browser is held to: read the diff, then regenerate with {UpdateVariable}=1");
    }
}
