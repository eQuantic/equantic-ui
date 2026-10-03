using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a vocabulary value type whose browser twin is its DATA alone: a plain object of its
/// positional members, with the type's statics, and each of its instance methods taking the value
/// first, on a companion object of the type's name. The transpiler reads it by symbol and lowers
/// every member of such a type to that shape: a call of an instance method to the companion's
/// static, the value's text to the record text .NET writes, and a construction to the data itself.
/// <para>
/// Every other vocabulary value type is a runtime class, whose methods a value carries on its
/// prototype. A type is data instead when its values reach browser code from producers that never
/// call a constructor (the runtime's own literals, the design system, a hydration payload), so a
/// method on a prototype would be missing exactly on the values that crossed. The reason names them.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false)]
public sealed class TwinIsDataAttribute(string reason) : Attribute
{
    /// <summary>Why the twin is data: the producers that make a value without constructing it.</summary>
    public string Reason { get; } = reason;
}
