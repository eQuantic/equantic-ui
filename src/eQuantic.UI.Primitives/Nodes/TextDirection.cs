namespace eQuantic.UI.Primitives;

/// <summary>
/// Which way text runs, and so which side a start and an end are on: after Flutter's
/// <c>TextDirection</c>. A component reads its own from <see cref="ComponentContext.TextDirection"/>,
/// which is the nearest <see cref="Directionality"/> above it, or its UI culture's.
/// <para>
/// Append-only, as every enum of the vocabulary is (<c>EnumValueAbiTests</c>).
/// </para>
/// </summary>
public enum TextDirection : byte
{
    /// <summary>The start is on the left: English, Portuguese, Chinese.</summary>
    LeftToRight = 0,

    /// <summary>The start is on the right: Arabic, Hebrew, Persian, Urdu.</summary>
    RightToLeft = 1,
}
