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
/// A type is named by its metadata name, the one <see cref="Type.FullName"/> answers
/// (<c>Shop.Pages.Cart+Summary</c> for a nested type, <c>Shop.Grid`1</c> for a generic one), and not by
/// <c>typeof</c>: an assembly attribute cannot reach a private nested type through <c>typeof</c>, and a
/// component may be one.
/// </para>
/// <para>
/// <see cref="Projection"/> says what of the value crosses. <c>null</c> means the value crosses whole.
/// Otherwise it crosses as a plain object holding only what the browser reads of it, or as null when it
/// is null, and the string lists those reads, comma-separated: each a chain of C# member names joined by
/// <c>.</c> (<c>DisplayName,Profile.Avatar</c>). A read crosses the value it reaches, and one ending in
/// <c>?</c> only whether that value is null, as null or an empty object (<c>Profile?</c>). An empty
/// string lists no read, so only whether the value itself is null crosses. The server writes exactly
/// those members, each under the twin's name, and nothing else of the value.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HydratedMemberAttribute : Attribute
{
    /// <param name="component">The metadata name of the component whose state carries the value.</param>
    /// <param name="declaringType">The metadata name of the type that declares the member: the component or one of its bases.</param>
    /// <param name="member">The member's name as C# declares it.</param>
    /// <param name="kind">How C# declares it, which is how the server reads it.</param>
    public HydratedMemberAttribute(string component, string declaringType, string member, HydratedMemberKind kind)
    {
        Component = component;
        DeclaringType = declaringType;
        Member = member;
        Kind = kind;
    }

    /// <summary>The metadata name of the component whose state carries the value.</summary>
    public string Component { get; }

    /// <summary>The metadata name of the type that declares the member: the component or one of its bases.</summary>
    public string DeclaringType { get; }

    /// <summary>The member's name as C# declares it.</summary>
    public string Member { get; }

    /// <summary>How C# declares the member, which is how the server reads it.</summary>
    public HydratedMemberKind Kind { get; }

    /// <summary>
    /// What of the value crosses: <c>null</c> for all of it, or the reads of it the browser makes (see
    /// the type's remarks).
    /// </summary>
    public string? Projection { get; set; }
}
