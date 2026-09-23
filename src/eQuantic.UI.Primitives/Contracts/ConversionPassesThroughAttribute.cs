using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a user-defined CONVERSION of a vocabulary type whose browser twin takes the operand AS IT
/// IS, so the transpiler hands the value through instead of calling the conversion. Every other
/// conversion a vocabulary type declares CROSSES: eqc calls the static its twin carries
/// (<c>IconGlyph.fromIcons('search')</c> for <c>IconGlyph g = Icons.Search</c>), because a value
/// that reaches a twin in a shape the twin does not declare is a signature describing its own
/// argument wrongly. The call is the default so that a conversion added without its static fails
/// at the call, loudly, rather than passing a value of the wrong shape in silence.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ConversionPassesThroughAttribute(string reason) : Attribute
{
    /// <summary>Why the twin takes the operand as it is — the code that makes it so, named.</summary>
    public string Reason { get; } = reason;
}
