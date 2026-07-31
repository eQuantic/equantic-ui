using eQuantic.UI.Core;
using eQuantic.UI.Core.Assets;

namespace eQuantic.UI.Lottie;

/// <summary>
/// Renders a Lottie animation using @dotlottie/player-component.
/// Supports both .json and .dotlottie formats.
/// </summary>
public class LottiePlayer : StatelessComponent, IRequireAssets
{
    /// <summary>
    /// URL to the .json or .dotlottie animation file.
    /// </summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>
    /// If true, the animation will play automatically.
    /// </summary>
    public bool Autoplay { get; set; } = true;

    /// <summary>
    /// If true, the animation will loop.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    /// Playback speed. Default is 1.
    /// </summary>
    public double Speed { get; set; } = 1;

    /// <summary>
    /// Background color (e.g., "transparent", "#ffffff").
    /// </summary>
    public string Background { get; set; } = "transparent";

    /// <summary>
    /// Width of the player (e.g., "300px", "100%").
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// Height of the player (e.g., "300px", "100%").
    /// </summary>
    public string? Height { get; set; }

    /// <summary>
    /// Controls if the player should show its own controls UI.
    /// </summary>
    public bool Controls { get; set; }

    public void ConfigureAssets(AssetBuilder assets)
    {
        // Load the dotlottie player component from CDN
        assets.AddScript("https://unpkg.com/@dotlottie/player-component@latest/dist/dotlottie-player.mjs", defer: true);
    }

    public override IComponent Build(RenderContext context)
    {
        var attributes = new Dictionary<string, string>
        {
            ["src"] = Src,
            ["background"] = Background,
            ["speed"] = Speed.ToString(),
            ["class"] = ClassName ?? ""
        };

        if (Autoplay) attributes["autoplay"] = "";
        if (Loop) attributes["loop"] = "";
        if (Controls) attributes["controls"] = "";

        var style = new List<string>();
        if (!string.IsNullOrEmpty(Width)) style.Add($"width: {Width}");
        if (!string.IsNullOrEmpty(Height)) style.Add($"height: {Height}");
        
        if (style.Count > 0)
        {
            attributes["style"] = string.Join("; ", style);
        }

        return new DynamicElement
        {
            TagName = "dotlottie-player",
            CustomAttributes = attributes
        };
    }
}
