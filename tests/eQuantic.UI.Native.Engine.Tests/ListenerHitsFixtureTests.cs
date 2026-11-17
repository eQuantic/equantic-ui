using System.Runtime.CompilerServices;
using System.Text.Json;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The listeners' hit rule (#1562), as Photon writes it (<c>ListenerHits</c>), runs every case of the fixture the
/// web's <c>listenerHits</c> runs too (<c>listener-hits.spec.ts</c>): the two writings of one rule cannot drift.
/// </summary>
public class ListenerHitsFixtureTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in Fixture().GetProperty("cases").EnumerateArray())
            data.Add(testCase.GetProperty("name").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryCaseOfTheFixture_HearsAsTheWebDoes(string name)
    {
        var testCase = Fixture().GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == name);
        var flat = new List<(string? Id, ListenerHits.Under Entry)>();
        Flatten(testCase.GetProperty("under"), flat);

        var heard = ListenerHits.Hear(flat.Select(item => item.Entry).ToList()).Select(i => flat[i].Id);

        heard.Should().Equal(testCase.GetProperty("hear").EnumerateArray().Select(id => id.GetString()));
    }

    [Fact]
    public void TheFixture_HoldsTheCasesTheIssueNames() =>
        Fixture().GetProperty("cases").GetArrayLength().Should().BeGreaterThanOrEqualTo(16);

    /// <summary>The case's tree in paint order: an entry, then what it holds, then what is painted after it.</summary>
    private static void Flatten(JsonElement entries, List<(string? Id, ListenerHits.Under Entry)> into)
    {
        foreach (var entry in entries.EnumerateArray())
        {
            var at = into.Count;
            var behavior = entry.TryGetProperty("listener", out var listener)
                ? Enum.Parse<HitTestBehavior>(listener.GetString()!, ignoreCase: true)
                : (HitTestBehavior?)null;
            var solid = entry.TryGetProperty("solid", out var drawn) && drawn.GetBoolean();
            var id = entry.TryGetProperty("id", out var named) ? named.GetString() : null;
            into.Add((id, new ListenerHits.Under(at, -1, solid, behavior)));
            if (entry.TryGetProperty("children", out var children)) Flatten(children, into);
            into[at] = (id, into[at].Entry with { End = into.Count });
        }
    }

    private static JsonElement Fixture([CallerFilePath] string sourcePath = "")
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        var path = Path.Combine(root, "src", "eQuantic.UI.Runtime", "src", "shared", "listener-hits.fixture.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }
}
