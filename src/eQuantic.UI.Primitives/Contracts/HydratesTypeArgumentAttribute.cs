using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a type parameter of a vocabulary type whose values the browser REVIVES: the C# type of
/// the argument is erased in JavaScript, so the transpiler hands the twin the argument's hydration
/// spec (the one a Server Action's result is revived with) after the constructor's own arguments,
/// one per marked parameter, in declaration order. Null where the argument needs no revival.
/// <para>
/// <c>ServerTopic&lt;[HydratesTypeArgument] T&gt;</c> is the first: a payload published to the topic
/// crosses as JSON, and the topic built in the component is what knows it is a <c>RoomEvent</c>, so
/// the topic carries the spec and every payload is revived with it. Any vocabulary type whose browser
/// half receives values of a type argument takes the attribute rather than a rule of its own.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.GenericParameter, Inherited = false)]
public sealed class HydratesTypeArgumentAttribute : Attribute
{
}
