using System;
using System.Collections.Generic;
using System.Text.Json;
using eQuantic.UI.Server.Json;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Locks in the eQuantic.UI wire protocol for 64-bit integers: <see cref="long"/>/<see cref="ulong"/>
/// cross the wire as JSON <b>strings</b> so values beyond 2^53 survive into the client BigInt-backed
/// `long` runtime. Reads accept both strings (from the client) and bare numbers (lenient).
/// </summary>
public class EqJsonTests
{
    private sealed class Payload
    {
        public long Big { get; set; }
        public ulong UBig { get; set; }
        public int Small { get; set; }
        public decimal Price { get; set; }
    }

    private sealed class TemporalPayload
    {
        public DateTime When { get; set; }
        public TimeSpan Duration { get; set; }
    }

    [Fact]
    public void Long_IsSerializedAsString()
    {
        var json = JsonSerializer.Serialize(new Payload { Big = long.MaxValue, UBig = ulong.MaxValue, Small = 42 }, EqJson.Options);

        json.Should().Contain("\"big\":\"9223372036854775807\"");
        json.Should().Contain("\"uBig\":\"18446744073709551615\"");
        // 32-bit stays a bare number — only 64-bit needs the string protocol.
        json.Should().Contain("\"small\":42");
    }

    [Fact]
    public void Long_StringValue_RoundTripsExactly()
    {
        const long original = 9007199254740993; // 2^53 + 1 — not representable as a double
        var json = JsonSerializer.Serialize(new Payload { Big = original }, EqJson.Options);
        var back = JsonSerializer.Deserialize<Payload>(json, EqJson.Options)!;

        back.Big.Should().Be(original);
    }

    [Fact]
    public void Long_AcceptsBareNumber_OnRead()
    {
        // Lenient read: a client (or hand-written caller) sending a plain number must still parse.
        var back = JsonSerializer.Deserialize<Payload>("{\"big\":42,\"uBig\":7,\"small\":1}", EqJson.Options)!;

        back.Big.Should().Be(42);
        back.UBig.Should().Be(7);
    }

    [Fact]
    public void Options_UseCamelCase()
    {
        var json = JsonSerializer.Serialize(new Payload { Small = 1 }, EqJson.Options);
        json.Should().Contain("\"small\":");
        json.Should().NotContain("\"Small\":");
    }

    [Fact]
    public void Decimal_IsSerializedAsString_PreservingScale()
    {
        var json = JsonSerializer.Serialize(new Payload { Price = 100.50m }, EqJson.Options);

        // String form preserves the trailing zero (scale) — a JSON number would normalize to 100.5.
        json.Should().Contain("\"price\":\"100.50\"");
    }

    [Fact]
    public void Decimal_HighPrecision_RoundTripsExactly()
    {
        // 28 significant digits — far beyond what a double (the JS number type) can hold.
        const decimal original = 0.1234567890123456789012345678m;
        var json = JsonSerializer.Serialize(new Payload { Price = original }, EqJson.Options);
        var back = JsonSerializer.Deserialize<Payload>(json, EqJson.Options)!;

        back.Price.Should().Be(original);
    }

    [Fact]
    public void Decimal_AcceptsBareNumber_OnRead()
    {
        var back = JsonSerializer.Deserialize<Payload>("{\"price\":19.99}", EqJson.Options)!;
        back.Price.Should().Be(19.99m);
    }

    [Fact]
    public void DateTime_CrossesAsIso8601_AndRoundTrips()
    {
        // The client `dateTime` compat type parses/emits this ISO form (no custom converter needed —
        // System.Text.Json's default DateTime handling already matches).
        var payload = new TemporalPayload { When = new DateTime(2024, 1, 15, 9, 30, 0), Duration = TimeSpan.FromHours(25) };
        var json = JsonSerializer.Serialize(payload, EqJson.Options);

        json.Should().Contain("\"when\":\"2024-01-15T09:30:00\"");
        // TimeSpan uses the .NET "c" constant format — what the client TimeSpan compat type emits/parses.
        json.Should().Contain("\"duration\":\"1.01:00:00\"");

        var back = JsonSerializer.Deserialize<TemporalPayload>(json, EqJson.Options)!;
        back.When.Should().Be(payload.When);
        back.Duration.Should().Be(payload.Duration);
    }

    private enum Shelf { Core, DataAccess }

    [Flags]
    private enum Channels { None = 0, Colors = 1, Shadow = 2 }

    private sealed record Row(string Id, Shelf Shelf, Channels Channels);

    [Fact]
    public void Enums_CrossAsTheirCamelCaseMemberName_EvenNestedInAPayload()
    {
        // Transpiled code compares enums as camelCase member-name STRINGS; a JSON number would fail
        // every client-side comparison silently — and nested values (a record inside an array in the
        // SSR payload) are exactly where that bites.
        var json = System.Text.Json.JsonSerializer.Serialize(
            new[] { new Row("eQuantic.Core.Data", Shelf.DataAccess, Channels.Colors | Channels.Shadow) },
            EqJson.Options);

        json.Should().Contain("\"shelf\":\"dataAccess\"");
        // [Flags] stays NUMERIC on both sides — a combination has no member name.
        json.Should().Contain("\"channels\":3");
    }

    [Fact]
    public void EnumReads_AcceptTheNameInEitherCasing_OrTheOrdinal()
    {
        var fromCamel = System.Text.Json.JsonSerializer.Deserialize<Row>(
            "{\"id\":\"a\",\"shelf\":\"dataAccess\",\"channels\":1}", EqJson.Options);
        var fromPascal = System.Text.Json.JsonSerializer.Deserialize<Row>(
            "{\"id\":\"a\",\"shelf\":\"DataAccess\",\"channels\":1}", EqJson.Options);
        var fromOrdinal = System.Text.Json.JsonSerializer.Deserialize<Row>(
            "{\"id\":\"a\",\"shelf\":1,\"channels\":1}", EqJson.Options);

        fromCamel!.Shelf.Should().Be(Shelf.DataAccess);
        fromPascal!.Shelf.Should().Be(Shelf.DataAccess);
        fromOrdinal!.Shelf.Should().Be(Shelf.DataAccess);
    }
}
