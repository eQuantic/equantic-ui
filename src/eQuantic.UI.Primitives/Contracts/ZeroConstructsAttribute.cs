using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a vocabulary STRUCT whose browser twin builds its default — every component zero — from a
/// bare <c>new T()</c>, so the transpiler can spell <c>default(T)</c> on the other side instead of
/// answering <c>null</c>.
/// <para>
/// C# never has a null struct: a record struct built with no arguments, a field nobody assigned, an
/// <c>OrDefault</c> over an empty sequence all hold the zero instance. The twin of a struct the
/// compiler EMITS zero-constructs by construction (its parameters default to their own types'
/// zeros), but a hand-written twin only does if it was written to — which is what this says, where
/// the struct is declared, so the rule and the reason sit together. Without it a
/// <c>new CodeGrid()</c> held a null <c>Point</c> on the web and failed at its first read.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class ZeroConstructsAttribute(string reason) : Attribute
{
    /// <summary>Why the twin's bare constructor yields the zero instance — the code that makes it so,
    /// named.</summary>
    public string Reason { get; } = reason;
}
