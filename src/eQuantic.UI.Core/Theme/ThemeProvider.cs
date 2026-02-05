using System.Text.Json;
using eQuantic.UI.Core.Styling;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Provides runtime theme initialization and management.
/// Automatically uses EQ theme when Tailwind is not available.
/// </summary>
public static class ThemeProvider
{
    /// <summary>
    /// Gets the JavaScript initialization code for the theme system.
    /// This includes theme data and registration logic.
    /// </summary>
    public static string GetInitializationScript()
    {
        var styleSystem = StyleSystemDetector.PreferredSystem;

        if (styleSystem == StyleSystem.Tailwind)
        {
            // Tailwind package will handle its own theme initialization
            return string.Empty;
        }

        // Use EQ theme
        var theme = new AppThemeEQ();
        var themeData = SerializeThemeData(theme);

        return $@"
<script>
    // Store EQ theme data globally to be registered after runtime loads
    window.__EQUANTIC_THEME_DATA = {themeData};
    window.__EQUANTIC_THEME_READY = false;

    // Register theme function (called by runtime after it loads)
    window.__registerTheme = function() {{
        if (window.__EQUANTIC_THEME_READY) return;

        const themeData = window.__EQUANTIC_THEME_DATA;

        // Add method wrappers to match IButtonTheme interface
        const theme = {{
            button: {{
                base: themeData.button.base,
                getVariant: (variant) => {{
                    const key = typeof variant === 'string' ? variant : variant.toString();
                    return themeData.button.variants[key.toLowerCase()] || themeData.button.variants.primary;
                }},
                getSize: (size) => {{
                    const key = typeof size === 'string' ? size : size.toString();
                    return themeData.button.sizes[key.toLowerCase()] || themeData.button.sizes.medium;
                }}
            }}
            // TODO: Add other component themes as they are implemented
        }};

        getRootServiceProvider().registerInstance('IAppTheme', theme);
        getRootServiceProvider().registerInstance('eQuantic.UI.Core.Theme.IAppTheme', theme);
        window.__EQUANTIC_THEME_READY = true;
    }};
</script>";
    }

    /// <summary>
    /// Gets the dark mode support script for EQ theme.
    /// Uses data-theme attribute instead of class.
    /// </summary>
    public static string GetDarkModeScript()
    {
        return @"
<script>
    // EQ dark mode support - use data-theme attribute
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
        document.documentElement.setAttribute('data-theme', 'dark');
    }
    // Listen for changes in system preference
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (e.matches) {
            document.documentElement.setAttribute('data-theme', 'dark');
        } else {
            document.documentElement.removeAttribute('data-theme');
        }
    });
</script>";
    }

    private static string SerializeThemeData(IAppTheme theme)
    {
        var themeData = new
        {
            button = new
            {
                @base = theme.Button.Base,
                variants = new Dictionary<string, string>
                {
                    ["primary"] = theme.Button.GetVariant(Variant.Primary),
                    ["secondary"] = theme.Button.GetVariant(Variant.Secondary),
                    ["outline"] = theme.Button.GetVariant(Variant.Outline),
                    ["ghost"] = theme.Button.GetVariant(Variant.Ghost),
                    ["destructive"] = theme.Button.GetVariant(Variant.Destructive),
                    ["link"] = theme.Button.GetVariant(Variant.Link),
                    ["success"] = theme.Button.GetVariant(Variant.Success),
                    ["warning"] = theme.Button.GetVariant(Variant.Warning),
                    ["info"] = theme.Button.GetVariant(Variant.Info)
                },
                sizes = new Dictionary<string, string>
                {
                    ["small"] = theme.Button.GetSize(Size.Small),
                    ["medium"] = theme.Button.GetSize(Size.Medium),
                    ["large"] = theme.Button.GetSize(Size.Large),
                    ["xlarge"] = theme.Button.GetSize(Size.XLarge)
                }
            }
            // TODO: Add other theme components as they are implemented
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(themeData, jsonOptions);
    }
}
