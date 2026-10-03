using System;
using System.ComponentModel;

namespace eQuantic.UI.Primitives;

/// <summary>
/// One value a component carries from its server render to the browser: the unit of the hydration
/// manifest, which the source generator writes into the app's assembly and nothing else writes.
/// <para>
/// Both halves of the crossing read it. The server writes a component's state from these entries alone,
/// and eqc writes the twin's adoption from the same entries, so the two cannot disagree about what
/// crosses. Each value crosses under the name the twin declares the member under, the one rule both of
/// them apply, which is why an entry records no name.
/// </para>
/// <para>
/// <see cref="Projection"/> says what of the value crosses. <c>null</c> means the value crosses whole.
/// An empty string means only whether it is null: the server writes <c>null</c>, or an empty object.
/// Otherwise it is a comma-separated list of paths on the value, each a chain of C# member names joined
/// by <c>.</c>, where a segment ending in <c>[]</c> means every element of that collection
/// (<c>DisplayName,Roles[].Name</c>). The server writes exactly those members, each under the twin's
/// name, and nothing else of the value.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HydratedMemberAttribute : Attribute
{
    /// <param name="component">The component whose state carries the value.</param>
    /// <param name="declaringType">The type that declares the member: the component or one of its bases.</param>
    /// <param name="member">The member's name as C# declares it.</param>
    /// <param name="kind">How C# declares it, which is how the server reads it.</param>
    public HydratedMemberAttribute(Type component, Type declaringType, string member, HydratedMemberKind kind)
    {
        Component = component;
        DeclaringType = declaringType;
        Member = member;
        Kind = kind;
    }

    /// <summary>The component whose state carries the value.</summary>
    public Type Component { get; }

    /// <summary>The type that declares the member: the component or one of its bases.</summary>
    public Type DeclaringType { get; }

    /// <summary>The member's name as C# declares it.</summary>
    public string Member { get; }

    /// <summary>How C# declares the member, which is how the server reads it.</summary>
    public HydratedMemberKind Kind { get; }

    /// <summary>
    /// What of the value crosses: <c>null</c> for all of it, an empty string for whether it is null, or
    /// the paths the browser reads (see the type's remarks).
    /// </summary>
    public string? Projection { get; set; }
}
