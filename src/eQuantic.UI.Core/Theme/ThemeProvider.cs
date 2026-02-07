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
        console.log('[eQuantic.UI] __registerTheme called');
        if (window.__EQUANTIC_THEME_READY) return;

        const themeData = window.__EQUANTIC_THEME_DATA;
        if (!themeData) {{
            console.error('[eQuantic.UI] No theme data found in window.__EQUANTIC_THEME_DATA');
            return;
        }}

        const createThemeHelper = (data) => {{
            if (!data) return {{ base: '', getVariant: () => '', getSize: () => '' }};
            return {{
                base: data.base || '',
                getVariant: (variant) => {{
                    if (variant === undefined || variant === null) return '';
                    const key = variant.toString().toLowerCase();
                    return data.variants[key] || '';
                }},
                getSize: (size) => {{
                    if (size === undefined || size === null) return '';
                    const key = size.toString().toLowerCase();
                    return data.sizes[key] || '';
                }}
            }};
        }};

        try {{
            const theme = {{
                button: createThemeHelper(themeData.button),
                input: createThemeHelper(themeData.input),
                card: themeData.card ? {{
                    container: themeData.card.container || '',
                    header: themeData.card.header || '',
                    body: themeData.card.body || '',
                    footer: themeData.card.footer || '',
                    title: themeData.card.title || '',
                    description: themeData.card.description || '',
                    getShadow: (shadow) => themeData.card.shadows ? (themeData.card.shadows[shadow.toLowerCase()] || themeData.card.shadows['medium'] || '') : '',
                    getShadowInfo: (shadow) => themeData.card.shadows ? (themeData.card.shadows[shadow.toLowerCase()] || themeData.card.shadows['medium'] || '') : '',
                    getVariant: (variant) => {{
                        if (variant === undefined || variant === null) return '';
                        const key = variant.toString().toLowerCase();
                        return themeData.card.variants[key] || '';
                    }}
                }} : {{ container: '', header: '', body: '', footer: '', title: '', description: '', getShadow: () => '', getVariant: () => '' }},
                typography: themeData.typography ? {{
                    base: themeData.typography.base || '',
                    getVariant: (variant) => {{
                        if (variant === undefined || variant === null) return '';
                        const key = variant.toString().toLowerCase();
                        return themeData.typography.variants[key] || '';
                    }},
                    getHeading: (level) => themeData.typography.headings ? (themeData.typography.headings[level.toString()] || themeData.typography.base || '') : ''
                }} : {{ base: '', getVariant: () => '', getHeading: () => '' }}
            }};

            console.log('[eQuantic.UI] Registering IAppTheme instance:', theme);
            getRootServiceProvider().registerInstance('IAppTheme', theme);
            getRootServiceProvider().registerInstance('eQuantic.UI.Core.Theme.IAppTheme', theme);
            window.__EQUANTIC_THEME_READY = true;
            console.log('[eQuantic.UI] Theme registered successfully');
        }} catch (e) {{
            console.error('[eQuantic.UI] Failed to register theme:', e);
        }}
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
        var variants = Enum.GetValues<Variant>();
        var sizes = Enum.GetValues<Size>();

        var themeData = new
        {
            button = new
            {
                @base = theme.Button.Base,
                variants = variants.ToDictionary(v => v.ToString().ToLowerInvariant(), v => theme.Button.GetVariant(v))
                    .Concat(variants.ToDictionary(v => ((int)v).ToString(), v => theme.Button.GetVariant(v)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                sizes = sizes.ToDictionary(s => s.ToString().ToLowerInvariant(), s => theme.Button.GetSize(s))
                    .Concat(sizes.ToDictionary(s => ((int)s).ToString(), s => theme.Button.GetSize(s)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value)
            },
            input = theme.Input == null ? null : new
            {
                @base = theme.Input.Base,
                variants = variants.ToDictionary(v => v.ToString().ToLowerInvariant(), v => theme.Input.GetVariant(v))
                    .Concat(variants.ToDictionary(v => ((int)v).ToString(), v => theme.Input.GetVariant(v)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                sizes = sizes.ToDictionary(s => s.ToString().ToLowerInvariant(), s => theme.Input.GetSize(s))
                    .Concat(sizes.ToDictionary(s => ((int)s).ToString(), s => theme.Input.GetSize(s)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value)
            },
            card = theme.Card == null ? null : new
            {
                container = theme.Card.Container,
                header = theme.Card.Header,
                body = theme.Card.Body,
                footer = theme.Card.Footer,
                title = theme.Card.Title,
                description = theme.Card.Description,
                shadows = theme.Card.Shadows,
                variants = Enum.GetValues<CardVariant>().ToDictionary(v => v.ToString().ToLowerInvariant(), v => theme.Card.GetVariant(v))
                    .Concat(Enum.GetValues<CardVariant>().ToDictionary(v => ((int)v).ToString(), v => theme.Card.GetVariant(v)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value)
            },
            typography = theme.Typography == null ? null : new
            {
                @base = theme.Typography.Base,
                variants = variants.ToDictionary(v => v.ToString().ToLowerInvariant(), v => theme.Typography.GetVariant(v))
                    .Concat(variants.ToDictionary(v => ((int)v).ToString(), v => theme.Typography.GetVariant(v)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                headings = Enumerable.Range(1, 6).ToDictionary(l => l.ToString(), l => theme.Typography.GetHeading(l))
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(themeData, jsonOptions);
    }
}
