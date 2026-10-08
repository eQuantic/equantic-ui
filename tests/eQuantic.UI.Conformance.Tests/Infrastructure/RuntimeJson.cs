using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// The JSON the .NET side writes a value as: what <c>JSON.stringify</c> writes for the runtime's
/// representation of that value, so the two sides compare the VALUE and a representation the translation
/// got wrong fails. System.Text.Json's own JSON answered the other way round for these (#596), measured
/// on both sides:
/// <list type="table">
/// <listheader><term>C# value</term><description>the runtime holds it as, and JSON.stringify writes</description></listheader>
/// <item><term><c>long</c>, <c>ulong</c></term><description>a BigInt, written by the runtime's
/// <c>BigInt.prototype.toJSON</c> as its digits in a string: <c>"5"</c>. System.Text.Json wrote <c>5</c>,
/// so a long that crossed as a BigInt failed and one that became a JS number passed.</description></item>
/// <item><term><c>decimal</c></term><description>the runtime's Decimal, whose <c>toJSON</c> writes its
/// text, scale kept: <c>"1.50"</c>. A decimal that became a JS number passed against <c>1.50</c>.</description></item>
/// <item><term>an enum</term><description>a <c>[Flags]</c> enum's number, any other enum's member by its
/// twin name (<c>"friday"</c>), and a value no member names by its number. An enum written as its number
/// passed against <c>1</c>.</description></item>
/// <item><term><c>double</c>, <c>float</c></term><description>a double, a float the double that holds the
/// single, written by JavaScript's own <c>Number::toString</c>: <c>100000000000000000</c> for 1e17,
/// <c>0.00001</c>, <c>1e+21</c>, <c>0.10000000149011612</c> for <c>0.1f</c>; NaN and the infinities as
/// <c>null</c>, and -0 as <c>0</c>. System.Text.Json wrote <c>1E+17</c>, <c>1E-05</c>, <c>0.1</c> for the
/// float (so a float left a double passed), and threw on NaN.</description></item>
/// <item><term>a value tuple, a <c>KeyValuePair</c></term><description>an array: <c>["5","x"]</c>,
/// <c>["a",1]</c>. System.Text.Json wrote <c>{}</c> for the tuple, whose items are fields.</description></item>
/// <item><term>a dictionary</term><description>its pairs, in the order it enumerates:
/// <c>[[3,"c"],[1,"a"]]</c>, each pair written as a <c>KeyValuePair</c> is. System.Text.Json wrote an
/// object, which the runtime wrote too until a JSON object lost the order of integer-like keys (#437).</description></item>
/// </list>
/// What both sides already wrote alike stays System.Text.Json's: an int, a bool, a string, a char (a
/// one-character string), null, a <c>DateTime</c>, <c>DateOnly</c>, <c>TimeOnly</c>,
/// <c>DateTimeOffset</c> and <c>TimeSpan</c> (each a twin whose <c>toJSON</c> writes .NET's ISO text), a
/// <c>Guid</c> (its text), an array, a list, a record and an anonymous value.
/// </summary>
public static class RuntimeJson
{
    /// <summary>The converters <see cref="DotNetEvaluator"/> writes with.</summary>
    public static IEnumerable<JsonConverter> Converters { get; } =
    [
        new AsBigInt<long>(),
        new AsBigInt<ulong>(),
        new AsDecimalText(),
        new AsDouble(),
        new AsSingle(),
        new EnumAsTwin(),
        new TupleAsArray(),
        new PairAsArray(),
        new DictionaryAsPairs(),
    ];

    /// <summary>
    /// JavaScript's <c>Number::toString</c> for a finite double (ECMA-262, Number::toString): the
    /// shortest digits that round-trip, which .NET's <c>"R"</c> writes too, laid out by JavaScript's
    /// rules: plain digits up to 21 places before the point and 6 zeros after it, and an exponent with
    /// its sign beyond that (<c>1e+21</c>, <c>1e-7</c>). -0 is <c>0</c>, as JavaScript writes it.
    /// </summary>
    public static string NumberText(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "JavaScript writes only a finite number as one.");
        if (value == 0) return "0";
        if (value < 0) return "-" + NumberText(-value);

        // "1.2345678901234568E+17", "1E-05", "123.456": the digits, and where the point sits among them.
        var shortest = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentAt = shortest.IndexOf('E');
        var mantissa = exponentAt < 0 ? shortest : shortest[..exponentAt];
        var exponent = exponentAt < 0 ? 0 : int.Parse(shortest[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var point = mantissa.IndexOf('.');
        var digits = point < 0 ? mantissa : mantissa.Remove(point, 1);
        // n: the digits stand for 0.digits × 10^n.
        var n = (point < 0 ? mantissa.Length : point) + exponent;
        var leading = 0;
        while (leading < digits.Length - 1 && digits[leading] == '0')
        {
            leading++;
            n--;
        }
        digits = digits[leading..].TrimEnd('0');
        var k = digits.Length;

        if (k <= n && n <= 21) return digits + new string('0', n - k);
        if (0 < n && n <= 21) return digits[..n] + "." + digits[n..];
        if (-6 < n && n <= 0) return "0." + new string('0', -n) + digits;
        var power = n - 1;
        return (k == 1 ? digits : digits[0] + "." + digits[1..])
            + (power < 0 ? "e-" : "e+") + Math.Abs(power).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>A long or a ulong as the runtime's BigInt writes itself: its digits, in a string.</summary>
    private sealed class AsBigInt<T> : JsonConverter<T> where T : struct, IFormattable
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("The harness only writes.");

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(null, CultureInfo.InvariantCulture));

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WritePropertyName(value.ToString(null, CultureInfo.InvariantCulture));
    }

    /// <summary>A decimal as the runtime's Decimal writes itself: its text, scale kept.</summary>
    private sealed class AsDecimalText : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("The harness only writes.");

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));

        public override void WriteAsPropertyName(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WritePropertyName(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>A double as JavaScript writes one, NaN and the infinities as JSON.stringify's null.</summary>
    private sealed class AsDouble : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("The harness only writes.");

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsFinite(value)) writer.WriteRawValue(NumberText(value));
            else writer.WriteNullValue();
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
            writer.WritePropertyName(double.IsFinite(value) ? NumberText(value) : double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity");
    }

    /// <summary>A float as the double the browser holds it in: the single, widened exactly.</summary>
    private sealed class AsSingle : JsonConverter<float>
    {
        private static readonly AsDouble Double = new();

        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("The harness only writes.");

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options) =>
            Double.Write(writer, value, options);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, float value, JsonSerializerOptions options) =>
            Double.WriteAsPropertyName(writer, value, options);
    }

    /// <summary>
    /// An enum as the runtime holds it: a <c>[Flags]</c> enum's number, a member by its twin name
    /// (<c>TwinName</c>, the rule eqc names it by), the first declared when two share a value, and a
    /// value no member names by its number. The number is a JS number whatever the underlying type, as
    /// eqc writes an enum's value, so an enum over a long writes no BigInt: it is written as the double
    /// it is (found in review).
    /// </summary>
    private sealed class EnumAsTwin : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(Of<>).MakeGenericType(typeToConvert))!;

        private sealed class Of<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
        {
            private static readonly bool Flags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);

            /// <summary>Each value's first declared member, in declaration order.</summary>
            private static readonly Dictionary<TEnum, string> Names = typeof(TEnum)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(field => field.MetadataToken)
                .GroupBy(field => (TEnum)field.GetValue(null)!)
                .ToDictionary(group => group.Key, group => group.First().Name.ToCamelCase());

            public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException("The harness only writes.");

            public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
            {
                if (!Flags && Names.TryGetValue(value, out var name)) writer.WriteStringValue(name);
                else writer.WriteRawValue(Number(value));
            }

            public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
                writer.WritePropertyName(!Flags && Names.TryGetValue(value, out var name)
                    ? name
                    : Number(value));

            /// <summary>The value's number, as the JS number the browser holds it in.</summary>
            private static string Number(TEnum value) => NumberText(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>A value tuple as the array the runtime holds it in, its items in order, a long tuple's
    /// <c>Rest</c> flattened into it as <c>ITuple</c> reads it.</summary>
    private sealed class TupleAsArray : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsValueType && typeToConvert.IsGenericType && typeof(ITuple).IsAssignableFrom(typeToConvert)
            && typeToConvert.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(Of<>).MakeGenericType(typeToConvert))!;

        private sealed class Of<TTuple> : JsonConverter<TTuple> where TTuple : struct, ITuple
        {
            public override TTuple Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException("The harness only writes.");

            public override void Write(Utf8JsonWriter writer, TTuple value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                for (var at = 0; at < value.Length; at++)
                    JsonSerializer.Serialize(writer, value[at], value[at]?.GetType() ?? typeof(object), options);
                writer.WriteEndArray();
            }
        }
    }

    /// <summary>A <c>KeyValuePair</c> as the pair the runtime yields: an array of its key and its value.</summary>
    private sealed class PairAsArray : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(Of<,>).MakeGenericType(typeToConvert.GetGenericArguments()))!;

        private sealed class Of<TKey, TValue> : JsonConverter<KeyValuePair<TKey, TValue>>
        {
            public override KeyValuePair<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException("The harness only writes.");

            public override void Write(Utf8JsonWriter writer, KeyValuePair<TKey, TValue> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, value.Key, options);
                JsonSerializer.Serialize(writer, value.Value, options);
                writer.WriteEndArray();
            }
        }
    }

    /// <summary>
    /// A dictionary as the runtime's <c>toJSON</c> writes its class, <c>Dictionary</c> or <c>SortedMap</c>:
    /// its pairs in the order it enumerates, each one an array of its key and its value (#437).
    /// </summary>
    private sealed class DictionaryAsPairs : JsonConverterFactory
    {
        private static readonly HashSet<Type> Shapes =
        [
            typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
            typeof(SortedDictionary<,>), typeof(SortedList<,>),
        ];

        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && Shapes.Contains(typeToConvert.GetGenericTypeDefinition());

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(
                typeof(Of<,,>).MakeGenericType([typeToConvert, .. typeToConvert.GetGenericArguments()]))!;

        private sealed class Of<TDictionary, TKey, TValue> : JsonConverter<TDictionary>
            where TDictionary : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            public override TDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException("The harness only writes.");

            public override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                foreach (var pair in value) JsonSerializer.Serialize(writer, pair, options);
                writer.WriteEndArray();
            }
        }
    }
}
