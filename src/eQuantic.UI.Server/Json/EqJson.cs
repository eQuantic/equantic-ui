using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
/// A dictionary crosses as its <c>[key, value]</c> pairs, which keep the order a JSON object loses for
/// integer-like keys (<see cref="DictionaryPairsConverter"/>).
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
        options.Converters.Add(new CamelCaseEnumConverter());
        options.Converters.Add(new FlagsEnumConverter());
        options.Converters.Add(new DictionaryPairsConverter());
    }

    /// <summary>
    /// The key and value types of a dictionary of .NET's own collections (<c>System.Collections.*</c>),
    /// which crosses the wire as its pairs, or null for any other type: one that is a dictionary only in
    /// shape (an <c>ExpandoObject</c>, a <c>JsonObject</c>, a type of an app's own) is written as
    /// System.Text.Json writes it, an object. The conformance harness writes a .NET value by the same rule.
    /// </summary>
    internal static (Type Key, Type Value)? DictionaryEntries(Type type)
    {
        if (!type.IsGenericType || type.Namespace?.StartsWith("System.Collections", StringComparison.Ordinal) != true)
            return null;
        foreach (var candidate in type.IsInterface ? type.GetInterfaces().Prepend(type) : type.GetInterfaces())
        {
            if (!candidate.IsGenericType) continue;
            var definition = candidate.GetGenericTypeDefinition();
            if (definition != typeof(IReadOnlyDictionary<,>) && definition != typeof(IDictionary<,>)) continue;
            var arguments = candidate.GetGenericArguments();
            return (arguments[0], arguments[1]);
        }
        return null;
    }

    /// <summary>
    /// A dictionary crosses as its PAIRS, a JSON array of <c>[key, value]</c> arrays in the order it
    /// enumerates (#437). A JSON object lists every integer-like name first and ascending in the browser,
    /// which parses one before any code sees it, so a <c>Dictionary&lt;int, string&gt;</c> holding 3, then
    /// 1 arrived as 1, 3. A key is written as a value of its type, by the converter that writes that type
    /// everywhere else, and a double's or a single's NaN and infinities, which no JSON number holds, as
    /// the text .NET and the browser's <c>Number</c> read back.
    /// <para>
    /// It takes every dictionary of .NET's own collections (<see cref="DictionaryEntries"/>): the shapes the
    /// browser holds as its class, and whatever implements them behind a member typed <c>object</c>, a
    /// read-only view, a frozen, an immutable or a concurrent dictionary, each in the order .NET enumerates
    /// it. Reads take the pairs, the only form the browser writes, into the shapes it holds as its class
    /// and a read-only view; a repeated key keeps its last value, as a repeated property name does.
    /// </para>
    /// </summary>
    private sealed class DictionaryPairsConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => DictionaryEntries(typeToConvert) is not null;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var (key, value) = DictionaryEntries(typeToConvert)!.Value;
            return (JsonConverter)Activator.CreateInstance(
                typeof(DictionaryPairsConverter<,,>).MakeGenericType(typeToConvert, key, value))!;
        }
    }

    private sealed class DictionaryPairsConverter<TDictionary, TKey, TValue> : JsonConverter<TDictionary>
        where TDictionary : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        /// <summary>What a read builds, chosen once for the type: the dictionary itself behind every shape
        /// it implements, the sorted two as their own classes, a read-only view over one. Null for a type
        /// the browser never sends, which is refused.</summary>
        private static readonly Func<Dictionary<TKey, TValue>, TDictionary>? Build = Builder();

        public override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var (key, item) in value)
            {
                writer.WriteStartArray();
                WriteKey(writer, key, options);
                JsonSerializer.Serialize(writer, item, options);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        public override TDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (Build is null)
                throw new JsonException($"A {typeof(TDictionary).Name} is written as its pairs, and read back only as a dictionary the browser holds as its class.");
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"A dictionary is read from its pairs, an array of [key, value] arrays, not from {reader.TokenType}.");
            var dictionary = new Dictionary<TKey, TValue>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartArray || !reader.Read() || reader.TokenType == JsonTokenType.EndArray)
                    throw NotAPair();
                var key = ReadKey(ref reader, options);
                if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray) throw NotAPair();
                var value = JsonSerializer.Deserialize<TValue>(ref reader, options)!;
                if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray) throw NotAPair();
                dictionary[key] = value;
            }
            return Build(dictionary);
        }

        private static void WriteKey(Utf8JsonWriter writer, TKey key, JsonSerializerOptions options)
        {
            if (key is double d && !double.IsFinite(d)) writer.WriteStringValue(d.ToString(CultureInfo.InvariantCulture));
            else if (key is float f && !float.IsFinite(f)) writer.WriteStringValue(f.ToString(CultureInfo.InvariantCulture));
            else JsonSerializer.Serialize(writer, key, options);
        }

        private static TKey ReadKey(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && typeof(TKey) == typeof(double))
                return (TKey)(object)double.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (reader.TokenType == JsonTokenType.String && typeof(TKey) == typeof(float))
                return (TKey)(object)float.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
            return JsonSerializer.Deserialize<TKey>(ref reader, options)
                ?? throw new JsonException("A dictionary's key cannot be null.");
        }

        private static Func<Dictionary<TKey, TValue>, TDictionary>? Builder()
        {
            if (typeof(TDictionary).IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
                return dictionary => (TDictionary)(object)dictionary;
            var definition = typeof(TDictionary).GetGenericTypeDefinition();
            if (definition == typeof(SortedDictionary<,>))
                return dictionary => (TDictionary)(object)new SortedDictionary<TKey, TValue>(dictionary);
            if (definition == typeof(SortedList<,>))
                return dictionary => (TDictionary)(object)new SortedList<TKey, TValue>(dictionary);
            if (definition == typeof(ReadOnlyDictionary<,>))
                return dictionary => (TDictionary)(object)new ReadOnlyDictionary<TKey, TValue>(dictionary);
            return null;
        }

        private static JsonException NotAPair() => new("A dictionary's pair is a [key, value] array.");
    }

    /// <summary>
    /// Enums cross as their MEMBER NAME in camelCase — the representation transpiled code compares
    /// against (`PackageCategory.Data` is the string `'data'` in the browser). A JSON number would
    /// silently fail every comparison the client makes, ANYWHERE in the payload: SSR state, Server
    /// Action arguments and results alike. Reads accept the name (either casing) or the ordinal, so
    /// a client that still sends a number keeps working, and refuse a name no member has, as
    /// System.Text.Json's own enum converter does: read as the enum's default, a stale or forged value
    /// became its first member, and two such keys of a dictionary collapsed into one.
    /// <para>Flags enums are the exception the transpiler already carves out — they are numeric on
    /// both sides (a combination has no member name), so they stay numbers here too.</para>
    /// </summary>
    private sealed class CamelCaseEnumConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsEnum && !typeToConvert.IsDefined(typeof(FlagsAttribute), inherit: false);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(
                typeof(CamelCaseEnumConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class CamelCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
                return (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt64());
            return Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Name(value));

        /// <summary>
        /// A dictionary's key, which is the same camelCase name as a property name. System.Text.Json
        /// writes a key only through a converter that says how, and this one did not, so a dictionary
        /// keyed by an enum was refused both ways (#442).
        /// </summary>
        public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WritePropertyName(Name(value));

        public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Parse(reader.GetString());

        private static string Name(TEnum value)
        {
            var name = value.ToString();
            return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
        }

        private static TEnum Parse(string? name) => ParseOrRefuse<TEnum>(name);
    }

    /// <summary>
    /// A <c>[Flags]</c> enum crosses as its NUMBER, which the transpiled side holds, a dictionary's key
    /// included. System.Text.Json's own converter already writes the value as a number, but it writes a
    /// key by the member names (<c>"Read, Write"</c>), which the browser does not hold (#442). Reads
    /// take the number, as a number or as text, or the names, and refuse anything else.
    /// </summary>
    private sealed class FlagsEnumConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsEnum && typeToConvert.IsDefined(typeof(FlagsAttribute), inherit: false);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(
                typeof(FlagsEnumConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class FlagsEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        private static readonly bool Unsigned = Enum.GetUnderlyingType(typeof(TEnum)) == typeof(ulong);

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Number
                ? (TEnum)(Unsigned ? Enum.ToObject(typeof(TEnum), reader.GetUInt64()) : Enum.ToObject(typeof(TEnum), reader.GetInt64()))
                : Parse(reader.GetString());

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            if (Unsigned) writer.WriteNumberValue(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
            else writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WritePropertyName(Unsigned
                ? Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
                : Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));

        public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Parse(reader.GetString());

        // A number's text and a name both parse, as Enum.Parse reads them.
        private static TEnum Parse(string? text) => ParseOrRefuse<TEnum>(text);
    }

    /// <summary>An enum's value from its text, a name in either casing or a number, as
    /// <c>Enum.Parse</c> reads it, or a <see cref="JsonException"/> for text that names no member.</summary>
    private static TEnum ParseOrRefuse<TEnum>(string? text) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(text, ignoreCase: true, out var value)
            ? value
            : throw new JsonException($"'{text}' is not a value of {typeof(TEnum).Name}.");

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
