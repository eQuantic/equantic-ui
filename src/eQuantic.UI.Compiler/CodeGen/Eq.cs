namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Canonical JS paths for the runtime <c>$eq</c> namespace that the transpiler emits for .NET-compat
/// helpers. Emitting any of these requires a single import of <see cref="Import"/> from
/// <c>@equantic/runtime</c> (resolved by the page's import map) — a strategy signals that by adding
/// <see cref="Import"/> to <c>UsedHelpers</c>. One import per module instead of N loose helper imports,
/// and the <c>$eq.*</c> form can never collide with a user identifier in the generated scope.
/// </summary>
public static class Eq
{
    /// <summary>The single symbol imported from <c>@equantic/runtime</c> when any <c>$eq.*</c> is emitted.</summary>
    public const string Import = "$eq";

    public const string Dec = "$eq.num.dec";
    public const string Long = "$eq.num.long";
    public const string Round = "$eq.math.round";
    public const string Format = "$eq.text.format";
    public const string StringBuilder = "$eq.text.stringBuilder";
    public const string DateTime = "$eq.time.dateTime";
    public const string TimeSpan = "$eq.time.timeSpan";
    public const string DateTimeOffset = "$eq.time.dateTimeOffset";
    public const string ParseEnum = "$eq.enums.parse";

    /// <summary>Lifted Nullable&lt;T&gt; arithmetic — <c>null</c> if either operand is null.</summary>
    public const string LiftArith = "$eq.nullable.arith";
    /// <summary>Lifted Nullable&lt;T&gt; relational — <c>false</c> if either operand is null.</summary>
    public const string LiftCmp = "$eq.nullable.cmp";

    /// <summary>Structural (value) equality for records/structs/tuples — backs ==, Contains, Distinct.</summary>
    public const string Equals = "$eq.equals";

    /// <summary>Factory for a structurally-keyed dictionary (<c>Dictionary&lt;RecordKey, V&gt;</c>).</summary>
    public const string ValueMap = "$eq.collections.valueMap";

    /// <summary>Factory for a key-sorted dictionary (<c>SortedDictionary&lt;K, V&gt;</c>).</summary>
    public const string SortedDictionary = "$eq.collections.sortedDictionary";
    /// <summary>Factory for a key-sorted list (<c>SortedList&lt;K, V&gt;</c>).</summary>
    public const string SortedList = "$eq.collections.sortedList";
}
