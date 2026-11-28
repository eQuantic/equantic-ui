namespace eQuantic.UI.Web;

/// <summary>
/// One layer of a box's CSS background (#1937): its entry in each of the seven
/// <c>background-*</c> lists. A gradient and a glow take the defaults; a pattern says where it is
/// placed, how it repeats and what it is clipped to.
/// </summary>
internal sealed record BackgroundLayer(
    string Image,
    string Size = BackgroundLayer.DefaultSize,
    string Repeat = BackgroundLayer.DefaultRepeat,
    string Position = BackgroundLayer.DefaultPosition,
    string Origin = BackgroundLayer.DefaultOrigin,
    string Clip = BackgroundLayer.DefaultClip,
    string Attachment = BackgroundLayer.DefaultAttachment)
{
    // CSS's own initial value of each list, which a list padded for another layer repeats.
    public const string DefaultSize = "auto";
    public const string DefaultRepeat = "repeat";
    public const string DefaultPosition = "0% 0%";
    public const string DefaultOrigin = "padding-box";
    public const string DefaultClip = "border-box";
    public const string DefaultAttachment = "scroll";
}
