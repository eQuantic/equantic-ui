namespace eQuantic.UI.Primitives;

/// <summary>Spec S6 — the Material window size classes: the ONLY responsive vocabulary app code
/// speaks. Web realizes them as build-time media queries; Photon resolves from the window width.</summary>
public enum WindowSizeClass : byte
{
    /// <summary>&lt; 600dp — phones portrait.</summary>
    Compact = 0,
    /// <summary>600–839dp — tablets portrait, foldables.</summary>
    Medium = 1,
    /// <summary>≥ 840dp — tablets landscape, desktop.</summary>
    Expanded = 2,
}
