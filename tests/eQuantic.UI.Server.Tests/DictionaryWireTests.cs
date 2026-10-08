using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;
using eQuantic.UI.Server.Json;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A dictionary crosses the wire as its pairs, in the order it enumerates (#437). A JSON object lists
/// every integer-like name first, ascending, and the browser parses one before any code sees it, so a
/// <c>Dictionary&lt;int, string&gt;</c> holding 3, then 1 arrived as 1, 3. Each key is written as a
/// value of its type, by the converter that writes that type everywhere else.
/// </summary>
public class DictionaryWireTests
{
    private enum Shelf { DataAccess, Core }

    [Flags]
    private enum Channels { None = 0, Colors = 1, Shadow = 2 }

    private sealed record Holder(IReadOnlyDictionary<int, string> Scores);

    private static string Write<T>(T value) => JsonSerializer.Serialize(value, EqJson.Options);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, EqJson.Options)!;

    [Fact]
    public void ADictionary_IsWrittenAsItsPairs_InTheOrderItEnumerates()
    {
        var scores = new Dictionary<int, string> { [3] = "c", [1] = "a", [2] = "b" };
        scores.Remove(1);
        scores[5] = "e";   // takes the slot 1 freed, as .NET's dictionary reuses it

        Write(scores).Should().Be("""[[3,"c"],[5,"e"],[2,"b"]]""");
        Write(new Dictionary<string, int> { ["b"] = 1, ["10"] = 2, ["9"] = 3 })
            .Should().Be("""[["b",1],["10",2],["9",3]]""");
    }

    [Fact]
    public void AKey_IsWrittenAsAValueOfItsType()
    {
        Write(new Dictionary<bool, int> { [true] = 1, [false] = 0 }).Should().Be("[[true,1],[false,0]]");
        Write(new Dictionary<long, string> { [9007199254740993L] = "x" }).Should().Be("""[["9007199254740993","x"]]""");
        Write(new Dictionary<decimal, int> { [1.50m] = 1 }).Should().Be("""[["1.50",1]]""");
        Write(new Dictionary<DateOnly, int> { [new DateOnly(2026, 1, 2)] = 1 }).Should().Be("""[["2026-01-02",1]]""");
        Write(new Dictionary<Shelf, int> { [Shelf.DataAccess] = 1 }).Should().Be("""[["dataAccess",1]]""");
        Write(new Dictionary<Channels, int> { [Channels.Colors | Channels.Shadow] = 3 }).Should().Be("[[3,3]]");
        Write(new Dictionary<char, int> { ['x'] = 1 }).Should().Be("""[["x",1]]""");
    }

    [Fact]
    public void EveryDictionaryTheBrowserHoldsAsItsClass_IsWrittenAsItsPairs()
    {
        Write(new SortedDictionary<string, int> { ["b"] = 3, ["9"] = 1, ["10"] = 2 })
            .Should().Be("""[["10",2],["9",1],["b",3]]""");
        Write(new SortedList<int, string> { [2] = "b", [1] = "a" }).Should().Be("""[[1,"a"],[2,"b"]]""");
        Write(new ReadOnlyDictionary<int, string>(new Dictionary<int, string> { [3] = "c", [1] = "a" }))
            .Should().Be("""[[3,"c"],[1,"a"]]""");
        Write<IDictionary<int, string>>(new Dictionary<int, string> { [3] = "c", [1] = "a" })
            .Should().Be("""[[3,"c"],[1,"a"]]""");
        // A member declared as the interface crosses as its pairs, whatever implements it.
        Write(new Holder(ImmutableSortedDictionary.CreateRange(new Dictionary<int, string> { [3] = "c", [1] = "a" })))
            .Should().Be("""{"scores":[[1,"a"],[3,"c"]]}""");
    }

    [Fact]
    public void ADictionaryLikeTypeOutsideThem_IsWrittenAsSystemTextJsonWritesIt()
    {
        // An object, as before: an immutable dictionary has no order .NET defines, and hydration
        // still builds the class from an object.
        object immutable = ImmutableDictionary<string, int>.Empty.Add("a", 1);
        Write(immutable).Should().Be("""{"a":1}""");
    }

    [Fact]
    public void ADictionary_IsReadFromItsPairs_InTheirOrder()
    {
        Read<Dictionary<int, string>>("""[[3,"c"],[1,"a"]]""").Keys.Should().Equal(3, 1);
        Read<IDictionary<long, string>>("""[["9007199254740993","x"]]""").Keys.Should().Equal(9007199254740993L);
        Read<IReadOnlyDictionary<bool, int>>("[[true,1],[false,0]]").Keys.Should().Equal(true, false);
        Read<SortedDictionary<int, string>>("""[[3,"c"],[1,"a"]]""").Keys.Should().Equal(1, 3);
        Read<SortedList<int, string>>("""[[3,"c"],[1,"a"]]""").Keys.Should().Equal(1, 3);
        Read<Dictionary<Shelf, int>>("""[["core",2]]""").Keys.Should().Equal(Shelf.Core);
    }

    [Fact]
    public void ADictionary_IsStillReadFromAnObject()
    {
        Read<Dictionary<int, string>>("""{"3":"c","1":"a"}""").Keys.Should().Equal(3, 1);
        Read<Dictionary<Channels, int>>("""{"3":3}""").Keys.Should().Equal(Channels.Colors | Channels.Shadow);
    }

    [Fact]
    public void ARepeatedKey_KeepsItsLastValue_AsARepeatedPropertyNameDoes()
    {
        Read<Dictionary<int, string>>("""[[1,"a"],[1,"b"]]""")
            .Should().Equal(new Dictionary<int, string> { [1] = "b" });
    }

    [Fact]
    public void APairThatIsNotAKeyAndAValue_IsRefused()
    {
        var one = () => Read<Dictionary<int, string>>("[[1]]");
        var three = () => Read<Dictionary<int, string>>("""[[1,"a","b"]]""");
        var nullKey = () => Read<Dictionary<string, int>>("[[null,1]]");

        one.Should().Throw<JsonException>();
        three.Should().Throw<JsonException>();
        nullKey.Should().Throw<JsonException>();
    }
}
