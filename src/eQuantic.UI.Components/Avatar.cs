using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>Presence tiers (spec B6): Online = Success fill, Offline = TextMuted.</summary>
public enum PresenceStatus : byte
{
    None = 0,
    Online = 1,
    Offline = 2,
}

/// <summary>
/// The design system's Avatar (spec B6): sizes 24/32/40/56 (S/M/L/XL), Radius.Full — never square
/// for people. v1 renders the INITIALS fallback tier: a Subtle variant pair tinted deterministically
/// from the name (the spec's 2-stop gradient hash upgrades this when gradient tokens land); the
/// image tier joins with the Image primitive, the status dot with Stack.
/// </summary>
public sealed class Avatar : StatelessComponent
{
    private static readonly Variant[] TintPalette =
    [
        Variant.Primary, Variant.Success, Variant.Info, Variant.Warning, Variant.Destructive,
    ];

    public Avatar(string initials, SizeVariant size = SizeVariant.Medium, string? name = null)
    {
        Initials = initials;
        Size = size;
        Name = name;
    }

    /// <summary>Up to 2 characters (clipped per spec).</summary>
    public string Initials { get; init; }

    /// <summary>Photo tier (top of the B6 fallback chain) — center-crop cover, circle-clipped.</summary>
    public string? ImageSource { get; init; }

    public SizeVariant Size { get; init; }

    /// <summary>Tint source (deterministic hash) — falls back to the initials when omitted.</summary>
    public string? Name { get; init; }

    /// <summary>Presence dot (spec B6): size/3.3, Success/TextMuted fill, 2dp Surface ring,
    /// bottom-end anchor — realized through Stack + Positioned.</summary>
    public PresenceStatus Status { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var side = Sizing.Avatar(Size);
        // Initials scale with the tier: 10/13/16/22 at 600 (derived — the spec pins only the sides).
        var labelSize = Size switch
        {
            SizeVariant.Small => 10f,
            SizeVariant.Medium => 13f,
            SizeVariant.Large => 16f,
            _ => 22f,
        };

        if (ImageSource is { } source)
        {
            var photo = new Image(source, side, side, ImageFit.Cover, Name ?? Initials)
            {
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            };
            return Status == PresenceStatus.None ? photo : WithStatusDot(photo, side, theme);
        }

        var seed = Name ?? Initials;
        var tint = theme.Colors(TintPalette[seed.Length % TintPalette.Length]);
        var clipped = Initials.Length > 2 ? Initials.Substring(0, 2) : Initials;

        // Spec B6 fallback chain (v1, no Image yet): initials → person glyph on SurfaceSubtle.
        var hasInitials = clipped.Length > 0;
        var glyphSize = Size switch
        {
            SizeVariant.Small => IconSize.Sm,
            SizeVariant.Medium => IconSize.Dense,
            SizeVariant.Large => IconSize.Md,
            _ => IconSize.Lg,
        };
        VisualNode face = hasInitials
            ? new Text(clipped, TypeRole.Caption, tint.OnSubtle, maxLines: 1)
            {
                StyleOverride = new TypeStyle(labelSize, labelSize, FontWeight.SemiBold, 0, 1.3f),
            }
            : new Icon(Icons.Person, glyphSize, theme.TextMuted);
        var content = face.Centered();

        var circle = new Box(new BoxStyle
        {
            Width = side,
            Height = side,
            Background = hasInitials ? tint.Subtle : theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        }, content);

        return Status == PresenceStatus.None ? circle : WithStatusDot(circle, side, theme);
    }

    private VisualNode WithStatusDot(VisualNode face, float side, IAppTheme theme)
    {
        var dotSide = MathF.Round(side / 3.3f);
        var dotFill = Status == PresenceStatus.Online ? theme.Colors(Variant.Success).Base : theme.TextMuted;
        var dot = new Box(new BoxStyle
        {
            Width = dotSide,
            Height = dotSide,
            Background = dotFill,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            BorderWidth = 2f,
            BorderColor = theme.Surface,
        });

        var stack = new Stack();
        stack.Add(face);
        stack.Add(new Positioned(dot, bottom: 0, end: 0));
        return stack;
    }
}
