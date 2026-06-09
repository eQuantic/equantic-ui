using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eQuantic.UI.Server.Json;

/// <summary>
/// JSON serialization for the eQuantic.UI wire protocol. Types that JavaScript cannot represent
/// without precision loss cross the wire as <b>strings</b> so the client compat runtime can restore
/// them exactly:
/// <list type="bullet">
///   <item>64-bit integers (<see cref="long"/>/<see cref="ulong"/>) → client BigInt-backed `long`.</item>
///   <item><see cref="decimal"/> → client base-10 `Decimal` (a JSON number would round through a
///   double and lose digits beyond ~17 significant figures).</item>
/// </list>
/// Reads stay lenient (string <i>or</i> number). This keeps Server Action payloads and SSR state precise.
/// </summary>
public static class EqJson
{
    /// <summary>Shared options instance (camelCase, Int64/UInt64 as string).</summary>
    public static readonly JsonSerializerOptions Options = Create(JsonNamingPolicy.CamelCase);

    public static JsonSerializerOptions Create(JsonNamingPolicy? namingPolicy = null)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = namingPolicy,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };
        Configure(options);
        return options;
    }

    /// <summary>Adds the eQuantic.UI converters to an existing options instance.</summary>
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new Int64StringConverter());
        options.Converters.Add(new UInt64StringConverter());
        options.Converters.Add(new DecimalStringConverter());
    }

    private sealed class Int64StringConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String
                ? long.Parse(reader.GetString()!, CultureInfo.InvariantCulture)
                : reader.GetInt64();

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class UInt64StringConverter : JsonConverter<ulong>
    {
        public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String
                ? ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture)
                : reader.GetUInt64();

        public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class DecimalStringConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String
                ? decimal.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
                : reader.GetDecimal();

        // Round-trip ("R"-equivalent) form preserves all significant digits and the decimal's scale,
        // which the client base-10 Decimal restores exactly.
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
