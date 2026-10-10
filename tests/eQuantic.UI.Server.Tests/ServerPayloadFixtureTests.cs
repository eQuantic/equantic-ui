using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Json;
using FluentAssertions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Payloads exactly as the server WRITES them, pinned for the runtime's hydration spec to read.
/// <para>
/// The client's own cases rebuilt a <see cref="Rect"/> from <c>{ x, y, width, height }</c>, which
/// is what a test author types and not what <see cref="EqJson"/> writes. System.Text.Json
/// serializes every public property, the computed ones too, so a Rect arrives with <c>left</c>,
/// <c>right</c>, <c>center</c> and <c>isEmpty</c> beside its four fields. The twin declares those
/// as getters, and assigning a member that only has a getter throws in a module: the clean payload
/// passed and the real one failed hydration. A dictionary keyed by a page's own data can also carry
/// the key <c>__proto__</c>, which an assignment once turned into a prototype, and a dictionary crosses
/// as its pairs, each key a value of its type, in the order the server enumerated it (#437).
/// </para>
/// <para>
/// The file is one line of wire JSON, the bytes a page receives. It is compared, never rewritten in
/// passing: <c>EQ_UPDATE_PAYLOAD_FIXTURE=1</c> writes it, and the vitest twin
/// (<c>hydrate.spec.ts</c>) reads the same file.
/// </para>
/// </summary>
public class ServerPayloadFixtureTests
{
    private const string Fixture = "src/eQuantic.UI.Runtime/src/utils/__fixtures__/server-payload.json";

    [Fact]
    public void ThePayloadsTheRuntimeHydrates_AreTheOnesTheServerWrites()
    {
        var actual = JsonSerializer.Serialize(new
        {
            // Fractional, so the twin's single-precision getters are compared with the sums the
            // server computed in floats.
            rect = new Rect(0.1f, 0.2f, 10.1f, 5f),
            // A long past 2^53, so the dictionary spec has an entry to convert, under a key a
            // page's data is free to hold.
            balances = new Dictionary<string, long> { ["__proto__"] = 9007199254740993L, ["a"] = 2L },
            // A key of each form the dictionary spec turns back into its type, integer keys written
            // out of order, which a JSON object would have listed ascending: the pairs keep them (#437).
            scores = new Dictionary<int, string> { [3] = "c", [1] = "a" },
            flags = new Dictionary<bool, int> { [true] = 1, [false] = 0 },
            big = new Dictionary<long, string> { [9007199254740993L] = "x" },
            prices = new Dictionary<decimal, int> { [1.50m] = 1 },
            days = new Dictionary<DateOnly, int> { [new DateOnly(2026, 1, 2)] = 1 },
            names = new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 },
        }, EqJson.Options) + "\n";

        var path = Path.Combine(RepoRoot.Find(), Fixture);
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_PAYLOAD_FIXTURE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
        }

        File.Exists(path).Should().BeTrue(
            $"the runtime's hydration spec reads {Fixture}; write it once with EQ_UPDATE_PAYLOAD_FIXTURE=1");
        File.ReadAllText(path).Should().Be(actual,
            "what the server writes for these values changed; if that is intended, regenerate with EQ_UPDATE_PAYLOAD_FIXTURE=1 and run the vitest twin");
    }
}
