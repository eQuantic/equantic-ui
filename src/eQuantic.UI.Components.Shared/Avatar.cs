using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components.Shared;

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

    public SizeVariant Size { get; init; }

    /// <summary>Tint source (deterministic hash) — falls back to the initials when omitted.</summary>
    public string? Name { get; init; }

    /// <summary>Presence dot (spec B6): size/3.3, Success/TextMuted fill, 2dp Surface ring,
    /// bottom-end anchor — realized through Stack + Positioned.</summary>
    public PresenceStatus Status { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var side = Size switch
        {
            SizeVariant.Small => 24f,
            SizeVariant.Medium => 32f,
            SizeVariant.Large => 40f,
            _ => 56f,
        };
        // Initials scale with the tier: 10/13/16/22 at 600 (derived — the spec pins only the sides).
        var labelSize = Size switch
        {
            SizeVariant.Small => 10f,
            SizeVariant.Medium => 13f,
            SizeVariant.Large => 16f,
            _ => 22f,
        };

        var seed = Name ?? Initials;
        var tint = theme.Colors(TintPalette[seed.Length % TintPalette.Length]);
        var clipped = Initials.Length > 2 ? Initials.Substring(0, 2) : Initials;

        var label = new Text(clipped, TypeRole.Caption, tint.OnSubtle, maxLines: 1)
        {
            StyleOverride = new TypeStyle(labelSize, labelSize, FontWeight.SemiBold, 0, 1.3f),
        };
        var content = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
        content.Add(label);

        var circle = new Box(new BoxStyle
        {
            Width = side,
            Height = side,
            Background = tint.Subtle,
            CornerRadius = new CornerRadii(Radius.Full),
        }, content);

        if (Status == PresenceStatus.None) return circle;

        var dotSide = MathF.Round(side / 3.3f);
        var dotFill = Status == PresenceStatus.Online ? theme.Colors(Variant.Success).Base : theme.TextMuted;
        var dot = new Box(new BoxStyle
        {
            Width = dotSide,
            Height = dotSide,
            Background = dotFill,
            CornerRadius = new CornerRadii(Radius.Full),
            BorderWidth = 2f,
            BorderColor = theme.Surface,
        });

        var stack = new Stack();
        stack.Add(circle);
        stack.Add(new Positioned(dot, bottom: 0, end: 0));
        return stack;
    }
}
