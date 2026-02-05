using System.Linq;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Type-safe Tailwind CSS classes using typed objects and implicit operators.
/// Provides the cleanest syntax with full IntelliSense support and compile-time safety.
/// </summary>
/// <example>
/// // Ultra-clean syntax with + operator
/// ClassName = TW.Flex.Row + TW.Flex.ItemsCenter + TW.Gap(4) + TW.Bg.White + TW.Rounded.Lg
///
/// // With variants
/// ClassName = TW.Bg.Blue600 + TW.Hover(TW.Bg.Blue700) + TW.Dark(TW.Bg.Zinc900)
///
/// // Conditional
/// ClassName = TW.P(6) + TW.When(isActive, TW.Bg.Blue50)
/// </example>
public static partial class TW
{
    /// <summary>
    /// Display utilities using typed objects
    /// </summary>
    public static class Display
    {
        public static readonly TailwindClass Block = new("block");
        public static readonly TailwindClass InlineBlock = new("inline-block");
        public static readonly TailwindClass Inline = new("inline");
        public static readonly TailwindClass Flex = new("flex");
        public static readonly TailwindClass InlineFlex = new("inline-flex");
        public static readonly TailwindClass Grid = new("grid");
        public static readonly TailwindClass InlineGrid = new("inline-grid");
        public static readonly TailwindClass Hidden = new("hidden");
        public static readonly TailwindClass Table = new("table");
        public static readonly TailwindClass TableRow = new("table-row");
        public static readonly TailwindClass TableCell = new("table-cell");
    }

    /// <summary>
    /// Flexbox utilities using typed objects
    /// </summary>
    public static class Flex
    {
        // Direction
        public static readonly TailwindClass Row = new("flex-row");
        public static readonly TailwindClass RowReverse = new("flex-row-reverse");
        public static readonly TailwindClass Col = new("flex-col");
        public static readonly TailwindClass ColReverse = new("flex-col-reverse");

        // Justify
        public static readonly TailwindClass JustifyStart = new("justify-start");
        public static readonly TailwindClass JustifyEnd = new("justify-end");
        public static readonly TailwindClass JustifyCenter = new("justify-center");
        public static readonly TailwindClass JustifyBetween = new("justify-between");
        public static readonly TailwindClass JustifyAround = new("justify-around");
        public static readonly TailwindClass JustifyEvenly = new("justify-evenly");

        // Align Items
        public static readonly TailwindClass ItemsStart = new("items-start");
        public static readonly TailwindClass ItemsEnd = new("items-end");
        public static readonly TailwindClass ItemsCenter = new("items-center");
        public static readonly TailwindClass ItemsBaseline = new("items-baseline");
        public static readonly TailwindClass ItemsStretch = new("items-stretch");

        // Sizing
        public static readonly TailwindClass Flex1 = new("flex-1");
        public static readonly TailwindClass FlexAuto = new("flex-auto");
        public static readonly TailwindClass FlexNone = new("flex-none");

        // Wrap
        public static readonly TailwindClass Wrap = new("flex-wrap");
        public static readonly TailwindClass NoWrap = new("flex-nowrap");
    }

    /// <summary>
    /// Grid utilities using typed objects
    /// </summary>
    public static class Grid
    {
        public static readonly TailwindClass Cols1 = new("grid-cols-1");
        public static readonly TailwindClass Cols2 = new("grid-cols-2");
        public static readonly TailwindClass Cols3 = new("grid-cols-3");
        public static readonly TailwindClass Cols4 = new("grid-cols-4");
        public static readonly TailwindClass Cols6 = new("grid-cols-6");
        public static readonly TailwindClass Cols12 = new("grid-cols-12");

        public static readonly TailwindClass PlaceItemsCenter = new("place-items-center");
        public static readonly TailwindClass PlaceItemsStart = new("place-items-start");
    }

    /// <summary>
    /// Background color utilities using typed objects
    /// </summary>
    public static class Bg
    {
        public static readonly TailwindClass Transparent = new("bg-transparent");
        public static readonly TailwindClass White = new("bg-white");
        public static readonly TailwindClass Black = new("bg-black");

        // Gray scale
        public static readonly TailwindClass Gray50 = new("bg-gray-50");
        public static readonly TailwindClass Gray100 = new("bg-gray-100");
        public static readonly TailwindClass Gray200 = new("bg-gray-200");
        public static readonly TailwindClass Gray700 = new("bg-gray-700");
        public static readonly TailwindClass Gray900 = new("bg-gray-900");

        // Zinc scale
        public static readonly TailwindClass Zinc800 = new("bg-zinc-800");
        public static readonly TailwindClass Zinc900 = new("bg-zinc-900");
        public static readonly TailwindClass Zinc950 = new("bg-zinc-950");

        // Blue scale
        public static readonly TailwindClass Blue50 = new("bg-blue-50");
        public static readonly TailwindClass Blue100 = new("bg-blue-100");
        public static readonly TailwindClass Blue200 = new("bg-blue-200");
        public static readonly TailwindClass Blue400 = new("bg-blue-400");
        public static readonly TailwindClass Blue500 = new("bg-blue-500");
        public static readonly TailwindClass Blue600 = new("bg-blue-600");
        public static readonly TailwindClass Blue700 = new("bg-blue-700");
        public static readonly TailwindClass Blue800 = new("bg-blue-800");

        // Indigo scale
        public static readonly TailwindClass Indigo50 = new("bg-indigo-50");
        public static readonly TailwindClass Indigo600 = new("bg-indigo-600");

        // Purple scale
        public static readonly TailwindClass Purple50 = new("bg-purple-50");
        public static readonly TailwindClass Purple600 = new("bg-purple-600");
        public static readonly TailwindClass Purple700 = new("bg-purple-700");

        // Red/Green
        public static readonly TailwindClass Red50 = new("bg-red-50");
        public static readonly TailwindClass Red100 = new("bg-red-100");
        public static readonly TailwindClass Red600 = new("bg-red-600");
        public static readonly TailwindClass Green50 = new("bg-green-50");
        public static readonly TailwindClass Green100 = new("bg-green-100");
        public static readonly TailwindClass Green200 = new("bg-green-200");
        public static readonly TailwindClass Green600 = new("bg-green-600");
        public static readonly TailwindClass Green800 = new("bg-green-800");

        // Gradients
        public static readonly TailwindClass GradientToR = new("bg-gradient-to-r");
        public static readonly TailwindClass GradientToL = new("bg-gradient-to-l");
        public static readonly TailwindClass GradientToBr = new("bg-gradient-to-br");
        public static readonly TailwindClass GradientToBl = new("bg-gradient-to-bl");

        // Clip
        public static readonly TailwindClass ClipText = new("bg-clip-text");
    }

    /// <summary>
    /// Text utilities using typed objects
    /// </summary>
    public static class Text
    {
        // Colors
        public static readonly TailwindClass Transparent = new("text-transparent");
        public static readonly TailwindClass White = new("text-white");
        public static readonly TailwindClass Gray300 = new("text-gray-300");
        public static readonly TailwindClass Gray400 = new("text-gray-400");
        public static readonly TailwindClass Gray500 = new("text-gray-500");
        public static readonly TailwindClass Gray600 = new("text-gray-600");
        public static readonly TailwindClass Gray700 = new("text-gray-700");
        public static readonly TailwindClass Gray800 = new("text-gray-800");
        public static readonly TailwindClass Gray900 = new("text-gray-900");
        public static readonly TailwindClass Blue400 = new("text-blue-400");
        public static readonly TailwindClass Blue600 = new("text-blue-600");
        public static readonly TailwindClass Blue700 = new("text-blue-700");
        public static readonly TailwindClass Purple400 = new("text-purple-400");
        public static readonly TailwindClass Purple600 = new("text-purple-600");
        public static readonly TailwindClass Red400 = new("text-red-400");
        public static readonly TailwindClass Red600 = new("text-red-600");
        public static readonly TailwindClass Green300 = new("text-green-300");
        public static readonly TailwindClass Green400 = new("text-green-400");
        public static readonly TailwindClass Green600 = new("text-green-600");
        public static readonly TailwindClass Green800 = new("text-green-800");

        // Sizes
        public static readonly TailwindClass Xs = new("text-xs");
        public static readonly TailwindClass Sm = new("text-sm");
        public static readonly TailwindClass Base = new("text-base");
        public static readonly TailwindClass Lg = new("text-lg");
        public static readonly TailwindClass Xl = new("text-xl");
        public static readonly TailwindClass Xl2 = new("text-2xl");
        public static readonly TailwindClass Xl3 = new("text-3xl");
        public static readonly TailwindClass Xl4 = new("text-4xl");

        // Alignment
        public static readonly TailwindClass Left = new("text-left");
        public static readonly TailwindClass Center = new("text-center");
        public static readonly TailwindClass Right = new("text-right");

        // Transform
        public static readonly TailwindClass Uppercase = new("uppercase");
        public static readonly TailwindClass Lowercase = new("lowercase");
        public static readonly TailwindClass Capitalize = new("capitalize");

        // Decoration
        public static readonly TailwindClass LineThrough = new("line-through");
    }

    /// <summary>
    /// Font utilities using typed objects
    /// </summary>
    public static class Font
    {
        public static readonly TailwindClass Sans = new("font-sans");
        public static readonly TailwindClass Serif = new("font-serif");
        public static readonly TailwindClass Mono = new("font-mono");
        public static readonly TailwindClass Italic = new("italic");

        public static readonly TailwindClass Thin = new("font-thin");
        public static readonly TailwindClass Light = new("font-light");
        public static readonly TailwindClass Normal = new("font-normal");
        public static readonly TailwindClass Medium = new("font-medium");
        public static readonly TailwindClass Semibold = new("font-semibold");
        public static readonly TailwindClass Bold = new("font-bold");
        public static readonly TailwindClass Black = new("font-black");
    }

    /// <summary>
    /// Border utilities using typed objects
    /// </summary>
    public static class Border
    {
        public static readonly TailwindClass Base = new("border");
        public static readonly TailwindClass B2 = new("border-2");
        public static readonly TailwindClass B4 = new("border-4");
        public static readonly TailwindClass T = new("border-t");
        public static readonly TailwindClass B = new("border-b");
        public static readonly TailwindClass L = new("border-l");
        public static readonly TailwindClass R = new("border-r");
        public static readonly TailwindClass None = new("border-none");

        public static readonly TailwindClass Transparent = new("border-transparent");
        public static readonly TailwindClass Gray100 = new("border-gray-100");
        public static readonly TailwindClass Gray200 = new("border-gray-200");
        public static readonly TailwindClass Gray300 = new("border-gray-300");
        public static readonly TailwindClass Blue500 = new("border-blue-500");
        public static readonly TailwindClass Green200 = new("border-green-200");
        public static readonly TailwindClass Green800 = new("border-green-800");
        public static readonly TailwindClass Zinc700 = new("border-zinc-700");
        public static readonly TailwindClass Zinc800 = new("border-zinc-800");

        public static readonly TailwindClass Dashed = new("border-dashed");
        public static readonly TailwindClass Solid = new("border-solid");
    }

    /// <summary>
    /// Rounded corners using typed objects
    /// </summary>
    public static class Rounded
    {
        public static readonly TailwindClass None = new("rounded-none");
        public static readonly TailwindClass Sm = new("rounded-sm");
        public static readonly TailwindClass Base = new("rounded");
        public static readonly TailwindClass Md = new("rounded-md");
        public static readonly TailwindClass Lg = new("rounded-lg");
        public static readonly TailwindClass Xl = new("rounded-xl");
        public static readonly TailwindClass Xl2 = new("rounded-2xl");
        public static readonly TailwindClass Full = new("rounded-full");
    }

    /// <summary>
    /// Shadow utilities using typed objects
    /// </summary>
    public static class Shadow
    {
        public static readonly TailwindClass Sm = new("shadow-sm");
        public static readonly TailwindClass Base = new("shadow");
        public static readonly TailwindClass Md = new("shadow-md");
        public static readonly TailwindClass Lg = new("shadow-lg");
        public static readonly TailwindClass Xl = new("shadow-xl");
        public static readonly TailwindClass Xl2 = new("shadow-2xl");
        public static readonly TailwindClass None = new("shadow-none");
    }

    /// <summary>
    /// Transition utilities using typed objects
    /// </summary>
    public static class Transition
    {
        public static readonly TailwindClass None = new("transition-none");
        public static readonly TailwindClass All = new("transition-all");
        public static readonly TailwindClass Base = new("transition");
        public static readonly TailwindClass Colors = new("transition-colors");
        public static readonly TailwindClass Opacity = new("transition-opacity");
        public static readonly TailwindClass Shadow = new("transition-shadow");
        public static readonly TailwindClass Transform = new("transition-transform");

        public static readonly TailwindClass Duration200 = new("duration-200");
        public static readonly TailwindClass Duration300 = new("duration-300");
        public static readonly TailwindClass Duration500 = new("duration-500");
    }

    /// <summary>
    /// Transform utilities using typed objects
    /// </summary>
    public static class Transform
    {
        public static readonly TailwindClass Scale105 = new("scale-105");
        public static readonly TailwindClass Scale110 = new("scale-110");
        public static readonly TailwindClass Scale125 = new("scale-125");
        public static readonly TailwindClass Scale150 = new("scale-150");
        public static readonly TailwindClass Rotate90 = new("rotate-90");
    }

    /// <summary>
    /// Tracking utilities
    /// </summary>
    public static class Tracking
    {
        public static readonly TailwindClass Tighter = new("tracking-tighter");
        public static readonly TailwindClass Tight = new("tracking-tight");
        public static readonly TailwindClass Normal = new("tracking-normal");
        public static readonly TailwindClass Wide = new("tracking-wide");
        public static readonly TailwindClass Wider = new("tracking-wider");
        public static readonly TailwindClass Widest = new("tracking-widest");
    }

    /// <summary>
    /// Ring utilities (focus rings)
    /// </summary>
    public static class Ring
    {
        public static readonly TailwindClass R0 = new("ring-0");
        public static readonly TailwindClass R1 = new("ring-1");
        public static readonly TailwindClass R2 = new("ring-2");
        public static readonly TailwindClass R4 = new("ring-4");
        public static readonly TailwindClass Base = new("ring");
    }

    /// <summary>
    /// Placeholder utilities
    /// </summary>
    public static class Placeholder
    {
        public static readonly TailwindClass Gray400 = new("placeholder:text-gray-400");
        public static readonly TailwindClass Gray600 = new("placeholder:text-gray-600");
    }

    /// <summary>
    /// Backdrop utilities using typed objects
    /// </summary>
    public static class Backdrop
    {
        public static readonly TailwindClass BlurSm = new("backdrop-blur-sm");
        public static readonly TailwindClass Blur = new("backdrop-blur");
        public static readonly TailwindClass BlurXl = new("backdrop-blur-xl");
        public static readonly TailwindClass Blur2xl = new("backdrop-blur-2xl");
    }

    /// <summary>
    /// Opacity utilities using typed objects
    /// </summary>
    public static class Opacity
    {
        public static readonly TailwindClass Op0 = new("opacity-0");
        public static readonly TailwindClass Op50 = new("opacity-50");
        public static readonly TailwindClass Op100 = new("opacity-100");
    }

    /// <summary>
    /// Position utilities using typed objects
    /// </summary>
    public static class Position
    {
        public static readonly TailwindClass Relative = new("relative");
        public static readonly TailwindClass Absolute = new("absolute");
        public static readonly TailwindClass Fixed = new("fixed");
        public static readonly TailwindClass Sticky = new("sticky");

        public static readonly TailwindClass Inset0 = new("inset-0");
        public static readonly TailwindClass Top0 = new("top-0");
        public static readonly TailwindClass Right0 = new("right-0");
        public static readonly TailwindClass Bottom0 = new("bottom-0");
        public static readonly TailwindClass Left0 = new("left-0");

        public static TailwindClass Top(int size) => new($"top-{size}");
        public static TailwindClass Right(int size) => new($"right-{size}");
        public static TailwindClass Bottom(int size) => new($"bottom-{size}");
        public static TailwindClass Left(int size) => new($"left-{size}");
    }

    /// <summary>
    /// Z-index utilities using typed objects
    /// </summary>
    public static class Z
    {
        public static readonly TailwindClass Z0 = new("z-0");
        public static readonly TailwindClass Z10 = new("z-10");
        public static readonly TailwindClass Z20 = new("z-20");
        public static readonly TailwindClass Z30 = new("z-30");
        public static readonly TailwindClass Z40 = new("z-40");
        public static readonly TailwindClass Z50 = new("z-50");
        public static readonly TailwindClass ZAuto = new("z-auto");
    }

    /// <summary>
    /// Overflow utilities using typed objects
    /// </summary>
    public static class Overflow
    {
        public static readonly TailwindClass Auto = new("overflow-auto");
        public static readonly TailwindClass Hidden = new("overflow-hidden");
        public static readonly TailwindClass Scroll = new("overflow-scroll");
        public static readonly TailwindClass Visible = new("overflow-visible");

        public static readonly TailwindClass XAuto = new("overflow-x-auto");
        public static readonly TailwindClass XHidden = new("overflow-x-hidden");
        public static readonly TailwindClass YAuto = new("overflow-y-auto");
        public static readonly TailwindClass YHidden = new("overflow-y-hidden");
    }

    /// <summary>
    /// Cursor utilities using typed objects
    /// </summary>
    public static class Cursor
    {
        public static readonly TailwindClass Auto = new("cursor-auto");
        public static readonly TailwindClass Default = new("cursor-default");
        public static readonly TailwindClass Pointer = new("cursor-pointer");
        public static readonly TailwindClass Wait = new("cursor-wait");
        public static readonly TailwindClass NotAllowed = new("cursor-not-allowed");
    }

    /// <summary>
    /// Gradient color stops using typed objects
    /// </summary>
    public static class From
    {
        public static readonly TailwindClass White = new("from-white");
        public static readonly TailwindClass Gray50 = new("from-gray-50");
        public static readonly TailwindClass Gray100 = new("from-gray-100");
        public static readonly TailwindClass Gray300 = new("from-gray-300");
        public static readonly TailwindClass Gray700 = new("from-gray-700");
        public static readonly TailwindClass Gray900 = new("from-gray-900");
        public static readonly TailwindClass Blue50 = new("from-blue-50");
        public static readonly TailwindClass Blue400 = new("from-blue-400");
        public static readonly TailwindClass Blue600 = new("from-blue-600");
        public static readonly TailwindClass Blue700 = new("from-blue-700");
        public static readonly TailwindClass Indigo50 = new("from-indigo-50");
        public static readonly TailwindClass Purple50 = new("from-purple-50");
        public static readonly TailwindClass Purple600 = new("from-purple-600");
        public static readonly TailwindClass Purple700 = new("from-purple-700");
        public static readonly TailwindClass Zinc800 = new("from-zinc-800");
        public static readonly TailwindClass Zinc900 = new("from-zinc-900");
        public static readonly TailwindClass Zinc950 = new("from-zinc-950");
    }

    public static class Via
    {
        public static readonly TailwindClass Indigo50 = new("via-indigo-50");
        public static readonly TailwindClass Purple600 = new("via-purple-600");
        public static readonly TailwindClass Zinc900 = new("via-zinc-900");
    }

    public static class To
    {
        public static readonly TailwindClass White = new("to-white");
        public static readonly TailwindClass Gray300 = new("to-gray-300");
        public static readonly TailwindClass Gray700 = new("to-gray-700");
        public static readonly TailwindClass Blue50 = new("to-blue-50");
        public static readonly TailwindClass Blue400 = new("to-blue-400");
        public static readonly TailwindClass Blue600 = new("to-blue-600");
        public static readonly TailwindClass Blue700 = new("to-blue-700");
        public static readonly TailwindClass Purple50 = new("to-purple-50");
        public static readonly TailwindClass Purple400 = new("to-purple-400");
        public static readonly TailwindClass Purple600 = new("to-purple-600");
        public static readonly TailwindClass Purple700 = new("to-purple-700");
        public static readonly TailwindClass Zinc800 = new("to-zinc-800");
        public static readonly TailwindClass Zinc900 = new("to-zinc-900");
        public static readonly TailwindClass Zinc950 = new("to-zinc-950");
    }

    /// <summary>
    /// Padding helper methods returning TailwindClass
    /// </summary>
    public static TailwindClass P(int size) => new($"p-{size}");
    public static TailwindClass Px(int size) => new($"px-{size}");
    public static TailwindClass Py(int size) => new($"py-{size}");
    public static TailwindClass Pt(int size) => new($"pt-{size}");
    public static TailwindClass Pr(int size) => new($"pr-{size}");
    public static TailwindClass Pb(int size) => new($"pb-{size}");
    public static TailwindClass Pl(int size) => new($"pl-{size}");

    /// <summary>
    /// Margin helper methods returning TailwindClass
    /// </summary>
    public static TailwindClass M(int size) => new($"m-{size}");
    public static TailwindClass Mx(int size) => new($"mx-{size}");
    public static TailwindClass My(int size) => new($"my-{size}");
    public static TailwindClass Mt(int size) => new($"mt-{size}");
    public static TailwindClass Mr(int size) => new($"mr-{size}");
    public static TailwindClass Mb(int size) => new($"mb-{size}");
    public static TailwindClass Ml(int size) => new($"ml-{size}");
    public static readonly TailwindClass MAuto = new("m-auto");
    public static readonly TailwindClass MxAuto = new("mx-auto");
    public static readonly TailwindClass MyAuto = new("my-auto");

    /// <summary>
    /// Gap helper methods returning TailwindClass
    /// </summary>
    public static TailwindClass Gap(int size) => new($"gap-{size}");
    public static TailwindClass GapX(int size) => new($"gap-x-{size}");
    public static TailwindClass GapY(int size) => new($"gap-y-{size}");

    /// <summary>
    /// Width helper methods returning TailwindClass
    /// </summary>
    public static TailwindClass W(int size) => new($"w-{size}");
    public static readonly TailwindClass WFull = new("w-full");
    public static readonly TailwindClass WScreen = new("w-screen");
    public static readonly TailwindClass WAuto = new("w-auto");

    /// <summary>
    /// Height helper methods returning TailwindClass
    /// </summary>
    public static TailwindClass H(int size) => new($"h-{size}");
    public static readonly TailwindClass HFull = new("h-full");
    public static readonly TailwindClass HScreen = new("h-screen");
    public static readonly TailwindClass HAuto = new("h-auto");

    /// <summary>
    /// Sizing constants
    /// </summary>
    public static class Size
    {
        public static readonly TailwindClass MinHScreen = new("min-h-screen");
        public static readonly TailwindClass MinWFull = new("min-w-full");

        public static readonly TailwindClass MaxWXs = new("max-w-xs");
        public static readonly TailwindClass MaxWSm = new("max-w-sm");
        public static readonly TailwindClass MaxWMd = new("max-w-md");
        public static readonly TailwindClass MaxWLg = new("max-w-lg");
        public static readonly TailwindClass MaxWXl = new("max-w-xl");
        public static readonly TailwindClass MaxW2xl = new("max-w-2xl");
        public static readonly TailwindClass MaxW3xl = new("max-w-3xl");
        public static readonly TailwindClass MaxW4xl = new("max-w-4xl");
        public static readonly TailwindClass MaxWFull = new("max-w-full");
        public static readonly TailwindClass MaxWScreen = new("max-w-screen");
    }

    /// <summary>
    /// Items (align-items) namespace
    /// </summary>
    public static class Items
    {
        public static readonly TailwindClass Start = new("items-start");
        public static readonly TailwindClass End = new("items-end");
        public static readonly TailwindClass Center = new("items-center");
        public static readonly TailwindClass Baseline = new("items-baseline");
        public static readonly TailwindClass Stretch = new("items-stretch");
    }

    /// <summary>
    /// MinH namespace
    /// </summary>
    public static class MinH
    {
        public static readonly TailwindClass Screen = new("min-h-screen");
        public static readonly TailwindClass Full = new("min-h-full");
    }

    /// <summary>
    /// MaxW namespace
    /// </summary>
    public static class MaxW
    {
        public static readonly TailwindClass Xs = new("max-w-xs");
        public static readonly TailwindClass Sm = new("max-w-sm");
        public static readonly TailwindClass Md = new("max-w-md");
        public static readonly TailwindClass Lg = new("max-w-lg");
        public static readonly TailwindClass Xl = new("max-w-xl");
        public static readonly TailwindClass MaxW2xl = new("max-w-2xl");
        public static readonly TailwindClass MaxW3xl = new("max-w-3xl");
        public static readonly TailwindClass MaxW4xl = new("max-w-4xl");
        public static readonly TailwindClass Full = new("max-w-full");
        public static readonly TailwindClass Screen = new("max-w-screen");
    }

    /// <summary>
    /// Duration (transition-duration) namespace
    /// </summary>
    public static class Duration
    {
        public static readonly TailwindClass _75 = new("duration-75");
        public static readonly TailwindClass _100 = new("duration-100");
        public static readonly TailwindClass _150 = new("duration-150");
        public static readonly TailwindClass _200 = new("duration-200");
        public static readonly TailwindClass _300 = new("duration-300");
        public static readonly TailwindClass _500 = new("duration-500");
        public static readonly TailwindClass _700 = new("duration-700");
        public static readonly TailwindClass _1000 = new("duration-1000");
    }

    /// <summary>
    /// Helper methods for flex direction
    /// </summary>
    public static TailwindClass FlexRow() => Flex.Row;
    public static TailwindClass FlexCol() => Flex.Col;

    /// <summary>
    /// Special effects and utilities
    /// </summary>
    public static readonly TailwindClass Group = new("group");
    public static readonly TailwindClass SelfCenter = new("self-center");

    /// <summary>
    /// Adds opacity to a Tailwind class (e.g., WithOpacity("bg-white", 80) => "bg-white/80").
    /// </summary>
    public static TailwindClass WithOpacity(string className, int opacity) => new($"{className}/{opacity}");

    /// <summary>
    /// Adds opacity to a TailwindClass (e.g., WithOpacity(TW.Bg.White, 80) => "bg-white/80").
    /// </summary>
    public static TailwindClass WithOpacity(TailwindClass tailwindClass, int opacity) => new($"{(string)tailwindClass}/{opacity}");

    /// <summary>
    /// Dark mode variant for a single string class
    /// </summary>
    public static TailwindClass Dark(string className) => new($"dark:{className}");

    /// <summary>
    /// Dark mode variant for a single TailwindClass
    /// </summary>
    public static TailwindClass Dark(TailwindClass tailwindClass) => new($"dark:{(string)tailwindClass}");

    /// <summary>
    /// Hover variant for a single string class
    /// </summary>
    public static TailwindClass Hover(string className) => new($"hover:{className}");

    /// <summary>
    /// Hover variant for a single TailwindClass
    /// </summary>
    public static TailwindClass Hover(TailwindClass tailwindClass) => new($"hover:{(string)tailwindClass}");

    /// <summary>
    /// Focus-within variant for a single string class
    /// </summary>
    public static TailwindClass FocusWithin(string className) => new($"focus-within:{className}");

    /// <summary>
    /// Focus-within variant for a single TailwindClass
    /// </summary>
    public static TailwindClass FocusWithin(TailwindClass tailwindClass) => new($"focus-within:{(string)tailwindClass}");

    /// <summary>
    /// Group-hover variant for a single string class
    /// </summary>
    public static TailwindClass GroupHover(string className) => new($"group-hover:{className}");

    /// <summary>
    /// Group-hover variant for a single TailwindClass
    /// </summary>
    public static TailwindClass GroupHover(TailwindClass tailwindClass) => new($"group-hover:{(string)tailwindClass}");
}

