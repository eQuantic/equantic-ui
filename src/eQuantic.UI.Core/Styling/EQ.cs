namespace eQuantic.UI.Core.Styling;

/// <summary>
/// Built-in eQuantic styling classes.
/// Works out-of-the-box without any external CSS framework.
/// All methods are compile-time evaluated for zero runtime overhead.
/// </summary>
[CompileTimeEvaluate]
public struct EQClass
{
    private readonly string _value;

    public EQClass(string value) => _value = value;

    public static implicit operator string(EQClass c) => c._value;
}

/// <summary>
/// eQuantic built-in styling helpers (no external dependencies required).
/// Provides type-safe CSS class generation with compile-time evaluation.
/// </summary>
public static class EQ
{
    // Layout
    public static readonly EQClass Flex = new("eq-flex");
    public static readonly EQClass FlexCol = new("eq-flex-col");
    public static readonly EQClass FlexRow = new("eq-flex-row");
    public static readonly EQClass Grid = new("eq-grid");
    public static readonly EQClass Block = new("eq-block");
    public static readonly EQClass Inline = new("eq-inline");
    public static readonly EQClass InlineBlock = new("eq-inline-block");
    public static readonly EQClass Hidden = new("eq-hidden");

    // Alignment
    public static readonly EQClass ItemsStart = new("eq-items-start");
    public static readonly EQClass ItemsCenter = new("eq-items-center");
    public static readonly EQClass ItemsEnd = new("eq-items-end");
    public static readonly EQClass JustifyStart = new("eq-justify-start");
    public static readonly EQClass JustifyCenter = new("eq-justify-center");
    public static readonly EQClass JustifyEnd = new("eq-justify-end");
    public static readonly EQClass JustifyBetween = new("eq-justify-between");

    // Spacing helpers
    public static EQClass P(int size) => new($"eq-p-{size}");
    public static EQClass Px(int size) => new($"eq-px-{size}");
    public static EQClass Py(int size) => new($"eq-py-{size}");
    public static EQClass Pt(int size) => new($"eq-pt-{size}");
    public static EQClass Pr(int size) => new($"eq-pr-{size}");
    public static EQClass Pb(int size) => new($"eq-pb-{size}");
    public static EQClass Pl(int size) => new($"eq-pl-{size}");

    public static EQClass M(int size) => new($"eq-m-{size}");
    public static EQClass Mx(int size) => new($"eq-mx-{size}");
    public static EQClass My(int size) => new($"eq-my-{size}");
    public static EQClass Mt(int size) => new($"eq-mt-{size}");
    public static EQClass Mr(int size) => new($"eq-mr-{size}");
    public static EQClass Mb(int size) => new($"eq-mb-{size}");
    public static EQClass Ml(int size) => new($"eq-ml-{size}");

    public static EQClass Gap(int size) => new($"eq-gap-{size}");

    // Sizing
    public static EQClass W(int size) => new($"eq-w-{size}");
    public static EQClass H(int size) => new($"eq-h-{size}");
    public static readonly EQClass WFull = new("eq-w-full");
    public static readonly EQClass HFull = new("eq-h-full");

    // Text
    public static readonly EQClass TextXs = new("eq-text-xs");
    public static readonly EQClass TextSm = new("eq-text-sm");
    public static readonly EQClass TextBase = new("eq-text-base");
    public static readonly EQClass TextLg = new("eq-text-lg");
    public static readonly EQClass TextXl = new("eq-text-xl");
    public static readonly EQClass Text2xl = new("eq-text-2xl");
    public static readonly EQClass Text3xl = new("eq-text-3xl");

    public static readonly EQClass TextCenter = new("eq-text-center");
    public static readonly EQClass TextLeft = new("eq-text-left");
    public static readonly EQClass TextRight = new("eq-text-right");

    public static readonly EQClass FontNormal = new("eq-font-normal");
    public static readonly EQClass FontMedium = new("eq-font-medium");
    public static readonly EQClass FontSemibold = new("eq-font-semibold");
    public static readonly EQClass FontBold = new("eq-font-bold");

    // Border Radius
    public static readonly EQClass RoundedSm = new("eq-rounded-sm");
    public static readonly EQClass RoundedMd = new("eq-rounded-md");
    public static readonly EQClass RoundedLg = new("eq-rounded-lg");
    public static readonly EQClass RoundedXl = new("eq-rounded-xl");
    public static readonly EQClass RoundedFull = new("eq-rounded-full");

    // Shadow
    public static readonly EQClass ShadowSm = new("eq-shadow-sm");
    public static readonly EQClass ShadowMd = new("eq-shadow-md");
    public static readonly EQClass ShadowLg = new("eq-shadow-lg");
    public static readonly EQClass ShadowXl = new("eq-shadow-xl");

    // Transition
    public static readonly EQClass TransitionAll = new("eq-transition-all");
    public static readonly EQClass TransitionColors = new("eq-transition-colors");

    // Cursor
    public static readonly EQClass CursorPointer = new("eq-cursor-pointer");
    public static readonly EQClass CursorNotAllowed = new("eq-cursor-not-allowed");

    /// <summary>
    /// Component presets
    /// </summary>
    public static class Button
    {
        public static readonly EQClass Base = new("eq-btn");
        public static readonly EQClass Primary = new("eq-btn eq-btn-primary");
        public static readonly EQClass Secondary = new("eq-btn eq-btn-secondary");
        public static readonly EQClass Outline = new("eq-btn eq-btn-outline");
        public static readonly EQClass Ghost = new("eq-btn eq-btn-ghost");
        public static readonly EQClass Destructive = new("eq-btn eq-btn-destructive");

        public static readonly EQClass Sm = new("eq-btn-sm");
        public static readonly EQClass Md = new("eq-btn-md");
        public static readonly EQClass Lg = new("eq-btn-lg");
    }

    public static class Input
    {
        public static readonly EQClass Base = new("eq-input");
        public static readonly EQClass Sm = new("eq-input-sm");
        public static readonly EQClass Md = new("eq-input-md");
        public static readonly EQClass Lg = new("eq-input-lg");
    }

    public static class Card
    {
        public static readonly EQClass Base = new("eq-card");
        public static readonly EQClass Header = new("eq-card-header");
        public static readonly EQClass Body = new("eq-card-body");
        public static readonly EQClass Footer = new("eq-card-footer");
    }

    /// <summary>
    /// Variant helpers (similar to TW)
    /// </summary>
    public static EQClass Dark(string className) => new($"dark:{className}");
    public static EQClass Dark(EQClass eqClass) => new($"dark:{(string)eqClass}");

    public static EQClass Hover(string className) => new($"hover:{className}");
    public static EQClass Hover(EQClass eqClass) => new($"hover:{(string)eqClass}");

    public static EQClass Focus(string className) => new($"focus:{className}");
    public static EQClass Focus(EQClass eqClass) => new($"focus:{(string)eqClass}");

    /// <summary>
    /// Responsive breakpoint helpers - apply styles at specific screen sizes.
    /// Uses mobile-first approach (styles apply at breakpoint and above).
    /// Breakpoints: Sm=640px, Md=768px, Lg=1024px, Xl=1280px, Xxl=1536px
    /// </summary>
    public static class Responsive
    {
        /// <summary>Small devices (≥640px) - Phones in landscape</summary>
        public static EQClass Sm(string className) => new($"sm:{className}");
        public static EQClass Sm(EQClass eqClass) => new($"sm:{(string)eqClass}");

        /// <summary>Medium devices (≥768px) - Tablets</summary>
        public static EQClass Md(string className) => new($"md:{className}");
        public static EQClass Md(EQClass eqClass) => new($"md:{(string)eqClass}");

        /// <summary>Large devices (≥1024px) - Laptops</summary>
        public static EQClass Lg(string className) => new($"lg:{className}");
        public static EQClass Lg(EQClass eqClass) => new($"lg:{(string)eqClass}");

        /// <summary>Extra large devices (≥1280px) - Desktops</summary>
        public static EQClass Xl(string className) => new($"xl:{className}");
        public static EQClass Xl(EQClass eqClass) => new($"xl:{(string)eqClass}");

        /// <summary>2X large devices (≥1536px) - Large desktops</summary>
        public static EQClass Xxl(string className) => new($"2xl:{className}");
        public static EQClass Xxl(EQClass eqClass) => new($"2xl:{(string)eqClass}");
    }
}
