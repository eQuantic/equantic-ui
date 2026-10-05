using System.Text.Json;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The harness's writer of a double, <see cref="RuntimeJson.NumberText"/>, against JavaScript's own
/// (#596). The .NET side of every case that returns a double writes it so, and a writer one digit or
/// one exponent away from JavaScript's would fail a right translation or pass a wrong one. Each value
/// reaches bun by its bits, so both sides read the same double.
/// </summary>
public class NumberTextTests
{
    [SkippableFact]
    public void NumberText_WritesEveryDoubleAsJavaScriptDoes()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var values = new List<double>
        {
            0, -0.0, 1, -1.5, 0.1, 0.1 + 0.2, 1.0 / 3, 100, 4.35, 123.456,
            1e-7, 9.999999999999999e-7, 1e-6, 0.000001234, 1e-5, 0.0001, 0.001234,
            1e15, 1e16, 1e17, 123456789012345678.0, 1e20, 99999999999999999999.0, 1e21, 1.5e21, 1e22,
            9007199254740992, 9007199254740994, double.MaxValue, double.Epsilon, 2.2250738585072014e-308,
            0.1f, 1e20f, float.MaxValue, float.Epsilon,
        };
        var random = new Random(596);
        for (var i = 0; i < 3000; i++)
        {
            // Any bit pattern: every exponent, every mantissa.
            var value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            if (double.IsFinite(value)) values.Add(value);
        }
        for (var i = 0; i < 2000; i++)
        {
            // The numbers a page computes, around both thresholds.
            values.Add(random.Next(1, 100000) * Math.Pow(10, random.Next(-12, 25)) * (random.Next(2) == 0 ? 1 : -1));
        }

        var bits = string.Join(",", values.Select(value => $"0x{BitConverter.DoubleToInt64Bits(value):x}n"));
        var program = "const f = new Float64Array(1); const u = new BigUint64Array(f.buffer); "
            + $"console.log(JSON.stringify([{bits}].map(b => {{ u[0] = b; return String(f[0]); }})));";
        var javaScript = JsonSerializer.Deserialize<List<string>>(JsExecutor.Run(program))!;
        Assert.Equal(values.Count, javaScript.Count);

        var differing = values
            .Select((value, at) => (Value: value, Ours: RuntimeJson.NumberText(value), Theirs: javaScript[at]))
            .Where(pair => pair.Ours != pair.Theirs)
            .Take(10)
            .Select(pair => $"{pair.Value:R}: the harness wrote {pair.Ours}, JavaScript {pair.Theirs}")
            .ToList();
        Assert.True(differing.Count == 0, string.Join("\n", differing));
    }
}
