using System.Collections.Generic;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class AppTheme : IAppTheme
{
    public ICardTheme Card { get; } = new CardTheme();
    public IButtonTheme Button { get; } = new ButtonTheme();
    public IInputTheme Input { get; } = new InputTheme();
    public ICheckboxTheme Checkbox { get; } = new CheckboxTheme();
    public ITextTheme Typography { get; } = new TextTheme();
}

public class CardTheme : ICardTheme
{
    public string Container => "flex flex-col bg-white dark:bg-zinc-900 rounded-xl overflow-hidden border border-gray-100 dark:border-zinc-800 transition-all duration-200";
    public string Header => "w-full px-6 py-4 border-b border-gray-50 dark:border-zinc-800 bg-white/50 dark:bg-zinc-900/50 backdrop-blur-sm";
    public string Body => "w-full p-6";
    public string Footer => "w-full px-6 py-4 bg-gray-50/30 dark:bg-zinc-900/50 border-t border-gray-50 dark:border-zinc-800 flex items-center";

    public Dictionary<string, string> Shadows { get; } = new Dictionary<string, string>
    {
        ["none"] = "shadow-none",
        ["small"] = "shadow-sm",
        ["medium"] = "shadow-md hover:shadow-lg",
        ["large"] = "shadow-lg hover:shadow-xl",
        ["xlarge"] = "shadow-2xl"
    };

    public string GetShadowInfo(string shadow) => Shadows.TryGetValue(shadow?.ToLower() ?? "medium", out var s) ? s : "shadow-md";
}

public class ButtonTheme : IButtonTheme
{
    public string Base => "inline-flex items-center justify-center rounded-lg px-4 py-2 text-sm font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:pointer-events-none active:scale-[0.98] cursor-pointer";

    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["primary"] = "bg-blue-600 text-white hover:bg-blue-700 shadow-sm hover:shadow dark:bg-blue-600 dark:hover:bg-blue-500",
        ["secondary"] = "bg-white text-gray-900 border border-gray-200 hover:bg-gray-50 hover:text-gray-900 dark:bg-zinc-900 dark:text-gray-100 dark:border-zinc-800 dark:hover:bg-zinc-800",
        ["outline"] = "border border-gray-200 bg-transparent hover:bg-gray-100 dark:border-zinc-800 dark:text-gray-100 dark:hover:bg-zinc-800",
        ["ghost"] = "hover:bg-gray-100 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-zinc-800 dark:hover:text-white",
        ["destructive"] = "bg-red-500 text-white hover:bg-red-600 shadow-sm hover:shadow",
        ["text"] = "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white",
        ["link"] = "text-blue-600 underline-offset-4 hover:underline dark:text-blue-400"
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "primary", out var v) ? v : Variants["primary"];
}

public class InputTheme : IInputTheme
{
    public string Base => "flex w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm ring-offset-white file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-gray-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-800 dark:bg-zinc-900 dark:ring-offset-zinc-950 dark:text-gray-100 dark:placeholder:text-gray-500 transition-colors";
}

public class CheckboxTheme : ICheckboxTheme
{
    public string Base => "peer h-4 w-4 shrink-0 rounded border border-gray-200 ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-blue-600 data-[state=checked]:text-white dark:border-zinc-700 dark:ring-offset-zinc-950 dark:data-[state=checked]:bg-blue-600 dark:data-[state=checked]:text-white transition-all";
    public string Checked => ""; // Handled by data-[state=checked]
    public string Unchecked => ""; // Handled by base
}

public class TextTheme : ITextTheme
{
    public string Base => "text-gray-900 dark:text-gray-100";
    
    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["h1"] = "scroll-m-20 text-4xl font-extrabold tracking-tight lg:text-5xl",
        ["h2"] = "scroll-m-20 border-b pb-2 text-3xl font-semibold tracking-tight first:mt-0",
        ["h3"] = "scroll-m-20 text-2xl font-semibold tracking-tight",
        ["h4"] = "scroll-m-20 text-xl font-semibold tracking-tight",
        ["p"] = "leading-7 [&:not(:first-child)]:mt-6",
        ["lead"] = "text-xl text-gray-500 dark:text-gray-400",
        ["large"] = "text-lg font-semibold",
        ["small"] = "text-sm font-medium leading-none",
        ["muted"] = "text-sm text-gray-500 dark:text-gray-400"
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "", out var v) ? v : "";
}
