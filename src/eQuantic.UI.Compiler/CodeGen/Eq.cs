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
}
