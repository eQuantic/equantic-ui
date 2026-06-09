namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Canonical JS paths for the runtime <c>$eq</c> namespace that the transpiler emits for .NET-compat
/// helpers. <c>$eq</c> is provided as a global (<c>window.$eq</c>) by the runtime (like <c>StyleBuilder</c>),
/// so emitting these does <b>not</b> require a per-module import — and the <c>$eq.*</c> form can never
/// collide with a user identifier in the generated scope.
/// </summary>
public static class Eq
{
    public const string Dec = "$eq.num.dec";
    public const string Long = "$eq.num.long";
    public const string Round = "$eq.math.round";
    public const string Format = "$eq.text.format";
    public const string StringBuilder = "$eq.text.stringBuilder";
    public const string DateTime = "$eq.time.dateTime";
    public const string TimeSpan = "$eq.time.timeSpan";
    public const string ParseEnum = "$eq.enums.parse";
}
