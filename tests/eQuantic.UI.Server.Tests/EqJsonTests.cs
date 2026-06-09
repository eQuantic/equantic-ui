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
}
