using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Extension methods for enabling Tailwind CSS in eQuantic.UI.
/// </summary>
public static class TailwindExtensions
{
    /// <summary>
    /// Enables Tailwind CSS integration by adding the default stylesheet link to the HTML shell.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="cssPath">The path to the generated CSS file. Defaults to "/css/app.css".</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseTailwind(this IApplicationBuilder app, string cssPath = "/css/app.css")
    {
        var options = app.ApplicationServices.GetService<UIOptions>();
        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseTailwind().");
        }

        // Add the Tailwind link to the head tags
        var buildId = eQuantic.UI.Server.UIExtensions.BuildId;
        var linkTag = $"<link rel=\"stylesheet\" href=\"{cssPath}?v={buildId}\">";
        if (!options.HtmlShell.HeadTags.Any(t => t.StartsWith($"<link rel=\"stylesheet\" href=\"{cssPath}")))
        {
            options.HtmlShell.HeadTags.Add(linkTag);
        }
 
        // Inject AppTheme as JS service
        var theme = new eQuantic.UI.Tailwind.Theme.AppTheme();

        // Serialize theme methods as lookup dictionaries
        var themeData = new
        {
            button = new
            {
                @base = theme.Button.Base,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Default),
                    ["primary"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Primary),
                    ["secondary"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Secondary),
                    ["outline"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Outline),
                    ["ghost"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Ghost),
                    ["destructive"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["link"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Link),
                    ["success"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["info"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Info)
                },
                sizes = new Dictionary<string, string>
                {
                    ["small"] = theme.Button.GetSize(Core.Theme.Types.Size.Small),
                    ["medium"] = theme.Button.GetSize(Core.Theme.Types.Size.Medium),
                    ["large"] = theme.Button.GetSize(Core.Theme.Types.Size.Large),
                    ["xlarge"] = theme.Button.GetSize(Core.Theme.Types.Size.XLarge)
                }
            },
            typography = new
            {
                @base = theme.Typography.Base,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Default),
                    ["primary"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Primary),
                    ["secondary"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Secondary),
                    ["ghost"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Ghost),
                    ["custom"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Custom)
                },
                headings = new Dictionary<string, string>
                {
                    ["h1"] = theme.Typography.GetHeading(1),
                    ["h2"] = theme.Typography.GetHeading(2),
                    ["h3"] = theme.Typography.GetHeading(3),
                    ["h4"] = theme.Typography.GetHeading(4),
                    ["h5"] = theme.Typography.GetHeading(5),
                    ["h6"] = theme.Typography.GetHeading(6)
                }
            },
            card = new
            {
                container = theme.Card.Container,
                header = theme.Card.Header,
                body = theme.Card.Body,
                footer = theme.Card.Footer,
                title = theme.Card.Title,
                description = theme.Card.Description,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Default),
                    ["outline"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Outline),
                    ["elevated"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Elevated),
                    ["subtle"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Subtle),
                    ["ghost"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Ghost)
                },
                shadows = theme.Card.Shadows
            },
            input = new
            {
                @base = theme.Input.Base,
                variants = new Dictionary<string, string>
                {
                    ["success"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["destructive"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["ghost"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Ghost)
                },
                sizes = new Dictionary<string, string>
                {
                    ["small"] = theme.Input.GetSize(Core.Theme.Types.Size.Small),
                    ["medium"] = theme.Input.GetSize(Core.Theme.Types.Size.Medium),
                    ["large"] = theme.Input.GetSize(Core.Theme.Types.Size.Large)
                }
            },
            checkbox = new
            {
                @base = theme.Checkbox.Base,
                root = theme.Checkbox.Root,
                indicator = theme.Checkbox.Indicator,
                @checked = theme.Checkbox.Checked,
                @unchecked = theme.Checkbox.Unchecked
            },
            badge = new
            {
                @base = theme.Badge.Base,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Default),
                    ["primary"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Primary),
                    ["secondary"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Secondary),
                    ["outline"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Outline),
                    ["destructive"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["success"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["info"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Info)
                },
            },
            alert = new
            {
                @base = theme.Alert.Base,
                title = theme.Alert.Title,
                description = theme.Alert.Description,
                icon = theme.Alert.Icon,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Default),
                    ["destructive"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["success"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["info"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Info)
                }
            }
        };

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var themeJson = System.Text.Json.JsonSerializer.Serialize(themeData, jsonOptions);

        // Register theme with method wrappers to match C# interface
        // Use inline script (not module) to ensure theme is available before hydration
        var script = $@"
<script>
    // Store theme data globally to be registered after runtime loads
    window.__EQUANTIC_THEME_DATA = {themeJson};
    window.__EQUANTIC_THEME_READY = false;

    // Register theme function (called by runtime after it loads)
    window.__registerTheme = function() {{
        console.log('[__registerTheme] Called');

        if (window.__EQUANTIC_THEME_READY) return;

        const themeData = window.__EQUANTIC_THEME_DATA;

        const variantMap = {{
            0: 'default',
            1: 'primary',
            2: 'secondary',
            3: 'destructive',
            4: 'outline',
            5: 'ghost',
            6: 'link',
            7: 'success',
            8: 'warning',
            9: 'info',
            10: 'custom'
        }};

        const sizeMap = {{
            0: 'small',
            1: 'medium',
            2: 'large',
            3: 'xlarge',
            4: 'custom'
        }};

        const shadowMap = {{
            0: 'none',
            1: 'small',
            2: 'medium',
            3: 'large',
            4: 'xlarge'
        }};

        const cardVariantMap = {{
            0: 'default',
            1: 'outline',
            2: 'elevated',
            3: 'subtle',
            4: 'ghost'
        }};

        // Helper to resolve stringified enum values from TS
        const resolveKey = (val, map) => {{
            if (val == null) return null;
            if (typeof val === 'number') return map[val] || val.toString().toLowerCase();
            if (typeof val === 'string') {{
                // Handle stringified numbers (e.g. ""3"" -> 3 -> ""large"")
                const num = parseInt(val, 10);
                if (!isNaN(num) && map[num]) return map[num];
                return val.toLowerCase();
            }}
            return val.toString().toLowerCase();
        }};

        // Add method wrappers to match IAppTheme interface
        const theme = {{
            button: {{
                base: themeData.button.base,
                getVariant: (variant) => {{
                    const key = resolveKey(variant, variantMap) || 'primary';
                    return themeData.button.variants[key] || themeData.button.variants.primary;
                }},
                getSize: (size) => {{
                    const key = resolveKey(size, sizeMap) || 'medium';
                    return themeData.button.sizes[key] || themeData.button.sizes.medium;
                }}
            }},
            typography: {{
                base: themeData.typography.base,
                getVariant: (variant) => {{
                    const key = resolveKey(variant, variantMap);
                    return key ? (themeData.typography.variants[key] || '') : '';
                }},
                getHeading: (level) => {{
                    // Default heading level is 1
                    const key = `h${{level || 1}}`;
                    return themeData.typography.headings[key] || themeData.typography.headings.h1;
                }}
            }},
            card: {{
                container: themeData.card.container,
                header: themeData.card.header,
                body: themeData.card.body,
                footer: themeData.card.footer,
                title: themeData.card.title,
                description: themeData.card.description,
                getVariant: (variant) => {{
                    const key = resolveKey(variant, cardVariantMap) || 'default';
                    return themeData.card.variants[key] || themeData.card.variants.default;
                }},
                getShadowInfo: (shadow) => {{
                    const key = resolveKey(shadow, shadowMap) || 'medium';
                    return themeData.card.shadows[key] || themeData.card.shadows.medium;
                }}
            }},
            input: {{
                base: themeData.input.base,
                getVariant: (variant) => {{
                    const key = resolveKey(variant, variantMap) || 'default';
                    return themeData.input.variants[key] || '';
                }},
                getSize: (size) => {{
                    const key = resolveKey(size, sizeMap) || 'medium';
                    return themeData.input.sizes[key] || themeData.input.sizes.medium;
                }}
            }},
            checkbox: {{
                base: themeData.checkbox.base,
                root: themeData.checkbox.root,
                indicator: themeData.checkbox.indicator,
                checked: themeData.checkbox.checked,
                unchecked: themeData.checkbox.unchecked
            }},
            badge: {{
                base: themeData.badge.base,
                getVariant: (variant) => {{
                    const key = resolveKey(variant, variantMap) || 'default';
                    return themeData.badge.variants[key] || themeData.badge.variants.default;
                }}
            }},
            alert: {{
                base: themeData.alert.base,
                title: themeData.alert.title,
                description: themeData.alert.description,
                icon: themeData.alert.icon,
                getVariant: (variant) => {{
                    const key = resolveKey(variant, variantMap) || 'default';
                    return themeData.alert.variants[key] || themeData.alert.variants.default;
                }}
            }}
        }};

        console.log('[__registerTheme] Registering theme');
        getRootServiceProvider().registerInstance('IAppTheme', theme);
        getRootServiceProvider().registerInstance('eQuantic.UI.Core.Theme.IAppTheme', theme);
        window.__EQUANTIC_THEME_READY = true;
    }};
</script>";
        options.HtmlShell.HeadTags.Add(script);

        // Add dark mode support script
        var darkModeScript = @"
<script>
    // Tailwind dark mode support - add 'dark' class based on system preference
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
        document.documentElement.classList.add('dark');
    }
    // Listen for changes in system preference
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (e.matches) {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
    });
</script>";
        options.HtmlShell.HeadTags.Add(darkModeScript);

        return app;
    }

    /// <summary>
    /// Registers Tailwind CSS services and component themes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTailwind(this IServiceCollection services)
    {
        // Register main theme
        services.AddSingleton<eQuantic.UI.Core.Theme.IAppTheme, eQuantic.UI.Tailwind.Theme.AppTheme>();
        
        // Register color theme (required by other themes)
        services.AddSingleton<eQuantic.UI.Core.Theme.IColorTheme, eQuantic.UI.Tailwind.Theme.ColorTheme>();

        // Register individual component themes
        services.AddSingleton<eQuantic.UI.Core.Theme.IButtonTheme, eQuantic.UI.Tailwind.Theme.ButtonTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.ICardTheme, eQuantic.UI.Tailwind.Theme.CardTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.IInputTheme, eQuantic.UI.Tailwind.Theme.InputTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.ICheckboxTheme, eQuantic.UI.Tailwind.Theme.CheckboxTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.IBadgeTheme, eQuantic.UI.Tailwind.Theme.BadgeTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.IAlertTheme, eQuantic.UI.Tailwind.Theme.AlertTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.ITextTheme, eQuantic.UI.Tailwind.Theme.TextTheme>();

        return services;
    }
}
