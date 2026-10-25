namespace eQuantic.UI.Primitives;

/// <summary>
/// Control size tiers — heights 32 · 40 · 48 · 56 dp (spec §09). Named <c>SizeVariant</c> (not the
/// design sketch's <c>Size</c>) for parity with the web SDK and because <c>Size</c> is the engine's
/// geometry struct.
/// </summary>
public enum SizeVariant : byte
{
    Small = 0,
    Medium = 1,
    Large = 2,
    XLarge = 3,
}
