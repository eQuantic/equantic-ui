namespace eQuantic.UI.Core.Theming;

/// <summary>
/// Comprehensive design tokens system using CSS custom properties.
/// Provides semantic naming independent of implementation (Tailwind, EQ, or custom).
/// All tokens map to CSS variables for maximum flexibility and theme-ability.
/// </summary>
/// <remarks>
/// Usage Philosophy:
/// - Use SEMANTIC names (Primary, Surface) not LITERAL (Blue600, Gray50)
/// - Tokens adapt to light/dark mode automatically via CSS
/// - Framework-agnostic - works with any styling system
/// </remarks>
public static class DesignTokens
{
    /// <summary>
    /// Semantic color tokens - use these instead of hardcoded color values.
    /// Automatically adapts to light/dark mode via CSS custom properties.
    /// </summary>
    public static class Colors
    {
        // ===== Surface Colors (Backgrounds) =====
        /// <summary>Primary background surface (white in light, dark in dark mode)</summary>
        public const string Surface = "var(--eq-surface)";
        /// <summary>Hover state for surfaces</summary>
        public const string SurfaceHover = "var(--eq-surface-hover)";
        /// <summary>Active/pressed state for surfaces</summary>
        public const string SurfaceActive = "var(--eq-surface-active)";
        /// <summary>Elevated surface (cards, modals, popovers)</summary>
        public const string SurfaceElevated = "var(--eq-surface)";  // Maps to same for now

        // ===== Text Colors =====
        /// <summary>Primary text color (high contrast - headings, body)</summary>
        public const string TextPrimary = "var(--eq-text-primary)";
        /// <summary>Secondary text color (medium contrast - labels, captions)</summary>
        public const string TextSecondary = "var(--eq-text-secondary)";
        /// <summary>Tertiary text color (low contrast - placeholders, hints)</summary>
        public const string TextTertiary = "var(--eq-text-tertiary)";
        /// <summary>Disabled text color</summary>
        public const string TextDisabled = "var(--eq-text-disabled)";
        /// <summary>Inverted text color (for use on colored backgrounds)</summary>
        public const string TextInverted = "#ffffff";

        // ===== Brand Colors =====
        /// <summary>Primary brand color (CTAs, primary buttons, links)</summary>
        public const string Primary = "var(--eq-primary)";
        public const string PrimaryHover = "var(--eq-primary-hover)";
        public const string PrimaryActive = "var(--eq-primary)";  // Darker version needed
        /// <summary>Subtle/muted primary (backgrounds, badges)</summary>
        public const string PrimarySubtle = "var(--eq-primary)";  // Lighter version needed

        /// <summary>Secondary brand color</summary>
        public const string Secondary = "var(--eq-secondary)";
        public const string SecondaryHover = "var(--eq-secondary-hover)";
        public const string SecondaryActive = "var(--eq-secondary)";
        public const string SecondarySubtle = "var(--eq-secondary)";

        /// <summary>Accent color (highlights, badges, notifications)</summary>
        public const string Accent = "var(--eq-info)";  // Maps to info for now
        public const string AccentHover = "var(--eq-info)";

        // ===== Status Colors =====
        /// <summary>Success state (confirmations, completed actions)</summary>
        public const string Success = "var(--eq-success)";
        public const string SuccessHover = "var(--eq-success)";  // Darker needed
        /// <summary>Subtle success background</summary>
        public const string SuccessSubtle = "var(--eq-success)";  // Light bg needed

        /// <summary>Warning state (cautions, reversible actions)</summary>
        public const string Warning = "var(--eq-warning)";
        public const string WarningHover = "var(--eq-warning)";
        public const string WarningSubtle = "var(--eq-warning)";

        /// <summary>Error/destructive state (errors, dangerous actions)</summary>
        public const string Error = "var(--eq-error)";
        public const string ErrorHover = "var(--eq-error)";
        public const string ErrorSubtle = "var(--eq-error)";

        /// <summary>Info state (helpful tips, neutral information)</summary>
        public const string Info = "var(--eq-info)";
        public const string InfoHover = "var(--eq-info)";
        public const string InfoSubtle = "var(--eq-info)";

        // ===== Border Colors =====
        /// <summary>Default border color</summary>
        public const string Border = "var(--eq-border)";
        /// <summary>Border on hover</summary>
        public const string BorderHover = "var(--eq-border-hover)";
        /// <summary>Border on focus (inputs, interactive elements)</summary>
        public const string BorderFocus = "var(--eq-border-focus)";
        /// <summary>Subtle border (dividers, separators)</summary>
        public const string BorderSubtle = "var(--eq-border)";
        /// <summary>Strong border (emphasis, contrast)</summary>
        public const string BorderStrong = "var(--eq-border-hover)";

        // ===== Overlay/Backdrop =====
        /// <summary>Modal/dialog backdrop overlay</summary>
        public const string Overlay = "rgba(0, 0, 0, 0.5)";
        /// <summary>Stronger overlay for more emphasis</summary>
        public const string OverlayStrong = "rgba(0, 0, 0, 0.75)";
    }

    /// <summary>
    /// Spacing scale tokens - use for padding, margin, gap.
    /// Based on 4px grid system (Xs=4px, Sm=8px, Md=16px, etc.)
    /// </summary>
    public static class Spacing
    {
        /// <summary>4px - Minimal spacing</summary>
        public const string Xs = "var(--eq-spacing-xs)";
        /// <summary>8px - Small spacing</summary>
        public const string Sm = "var(--eq-spacing-sm)";
        /// <summary>16px - Medium spacing (default)</summary>
        public const string Md = "var(--eq-spacing-md)";
        /// <summary>24px - Large spacing</summary>
        public const string Lg = "var(--eq-spacing-lg)";
        /// <summary>32px - Extra large spacing</summary>
        public const string Xl = "var(--eq-spacing-xl)";
        /// <summary>48px - Double extra large spacing</summary>
        public const string Xxl = "var(--eq-spacing-xxl)";

        // Numeric scale (for use with helpers like P(), M(), Gap())
        public const int XsNum = 1;    // Maps to eq-p-1, etc.
        public const int SmNum = 2;
        public const int MdNum = 4;
        public const int LgNum = 6;
        public const int XlNum = 8;
        public const int XxlNum = 12;
    }

    /// <summary>
    /// Border radius tokens - use for rounded corners
    /// </summary>
    public static class BorderRadius
    {
        /// <summary>0.25rem (4px) - Small radius</summary>
        public const string Sm = "var(--eq-radius-sm)";
        /// <summary>0.5rem (8px) - Medium radius (default)</summary>
        public const string Md = "var(--eq-radius-md)";
        /// <summary>0.75rem (12px) - Large radius</summary>
        public const string Lg = "var(--eq-radius-lg)";
        /// <summary>1rem (16px) - Extra large radius</summary>
        public const string Xl = "var(--eq-radius-xl)";
        /// <summary>9999px - Fully rounded (pills, circles)</summary>
        public const string Full = "var(--eq-radius-full)";
        /// <summary>0 - No radius (sharp corners)</summary>
        public const string None = "0";
    }

    /// <summary>
    /// Shadow/elevation tokens - use for depth hierarchy
    /// </summary>
    public static class Shadow
    {
        /// <summary>None - Flat, no elevation</summary>
        public const string None = "none";
        /// <summary>Small shadow - Subtle elevation (raised buttons)</summary>
        public const string Sm = "var(--eq-shadow-sm)";
        /// <summary>Medium shadow - Default elevation (cards)</summary>
        public const string Md = "var(--eq-shadow-md)";
        /// <summary>Large shadow - High elevation (modals, popovers)</summary>
        public const string Lg = "var(--eq-shadow-lg)";
        /// <summary>Extra large shadow - Maximum elevation (tooltips, dropdowns)</summary>
        public const string Xl = "var(--eq-shadow-xl)";
    }

    /// <summary>
    /// Typography tokens - use for consistent text styling
    /// </summary>
    public static class Typography
    {
        // Font Families
        /// <summary>Sans-serif font stack</summary>
        public const string FontFamily = "var(--eq-font-family)";
        /// <summary>Monospace font stack (code, technical)</summary>
        public const string FontFamilyMono = "var(--eq-font-family-mono)";

        // Font Sizes
        /// <summary>0.75rem (12px) - Extra small text</summary>
        public const string FontSizeXs = "var(--eq-font-size-xs)";
        /// <summary>0.875rem (14px) - Small text (captions, labels)</summary>
        public const string FontSizeSm = "var(--eq-font-size-sm)";
        /// <summary>1rem (16px) - Base text (body, default)</summary>
        public const string FontSizeBase = "var(--eq-font-size-base)";
        /// <summary>1.125rem (18px) - Large text (subheadings)</summary>
        public const string FontSizeLg = "var(--eq-font-size-lg)";
        /// <summary>1.25rem (20px) - Extra large text (h6)</summary>
        public const string FontSizeXl = "var(--eq-font-size-xl)";
        /// <summary>1.5rem (24px) - h5</summary>
        public const string FontSize2xl = "var(--eq-font-size-2xl)";
        /// <summary>1.875rem (30px) - h4</summary>
        public const string FontSize3xl = "var(--eq-font-size-3xl)";

        // Font Weights
        /// <summary>400 - Normal weight (body text)</summary>
        public const string FontWeightNormal = "var(--eq-font-weight-normal)";
        /// <summary>500 - Medium weight (emphasis)</summary>
        public const string FontWeightMedium = "var(--eq-font-weight-medium)";
        /// <summary>600 - Semibold (subheadings)</summary>
        public const string FontWeightSemibold = "var(--eq-font-weight-semibold)";
        /// <summary>700 - Bold (headings)</summary>
        public const string FontWeightBold = "var(--eq-font-weight-bold)";

        // Line Heights
        public const string LineHeightTight = "1.25";    // Headings
        public const string LineHeightNormal = "1.5";    // Body
        public const string LineHeightRelaxed = "1.75";  // Long-form content
    }

    /// <summary>
    /// Transition/animation tokens - use for consistent motion
    /// </summary>
    public static class Transition
    {
        /// <summary>150ms - Fast transitions (hover, active states)</summary>
        public const string Fast = "var(--eq-transition-fast)";
        /// <summary>200ms - Base transitions (most interactions)</summary>
        public const string Base = "var(--eq-transition-base)";
        /// <summary>300ms - Slow transitions (modals, complex animations)</summary>
        public const string Slow = "var(--eq-transition-slow)";

        // Easing functions
        public const string EaseInOut = "cubic-bezier(0.4, 0, 0.2, 1)";
        public const string EaseOut = "cubic-bezier(0, 0, 0.2, 1)";
        public const string EaseIn = "cubic-bezier(0.4, 0, 1, 1)";
        public const string EaseLinear = "linear";
    }

    /// <summary>
    /// Z-index tokens - use for stacking order consistency
    /// </summary>
    public static class ZIndex
    {
        public const int Base = 0;
        public const int Dropdown = 1000;
        public const int Sticky = 1100;
        public const int Fixed = 1200;
        public const int Overlay = 1300;
        public const int Modal = 1400;
        public const int Popover = 1500;
        public const int Tooltip = 1600;
    }

    /// <summary>
    /// Breakpoint tokens - use for responsive design
    /// </summary>
    public static class Breakpoints
    {
        /// <summary>640px - Small devices (phones)</summary>
        public const string Sm = "640px";
        /// <summary>768px - Medium devices (tablets)</summary>
        public const string Md = "768px";
        /// <summary>1024px - Large devices (laptops)</summary>
        public const string Lg = "1024px";
        /// <summary>1280px - Extra large devices (desktops)</summary>
        public const string Xl = "1280px";
        /// <summary>1536px - 2XL devices (large desktops)</summary>
        public const string Xxl = "1536px";
    }
}
