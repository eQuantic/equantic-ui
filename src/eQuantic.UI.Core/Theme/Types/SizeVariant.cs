namespace eQuantic.UI.Core.Theme.Types;

/// <summary>
/// The component sizing scale (Small/Medium/Large/XLarge, plus Custom). Named <c>SizeVariant</c> rather
/// than <c>Size</c> so it does not collide with the native HTML <c>size</c> attribute that
/// <see cref="eQuantic.UI.Core.HtmlElement"/> mirrors as its <c>Size</c> property — components inherit
/// that <c>int? Size</c>, which would otherwise shadow the enum's type name inside their methods.
/// </summary>
public enum SizeVariant
{
    Small,
    Medium,
    Large,
    XLarge,
    Custom
}
