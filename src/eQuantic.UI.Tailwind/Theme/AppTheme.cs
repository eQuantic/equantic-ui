using System;
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
    public string Container => "flex flex-col bg-white dark:bg-zinc-800 rounded-lg overflow-hidden border border-gray-200 dark:border-zinc-700 transition-all";
    public string Header => "w-full px-6 py-4 border-b border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800/50";
    public string Body => "w-full p-6";
    public string Footer => "w-full px-6 py-4 bg-gray-50 dark:bg-zinc-800/50 border-t border-gray-200 dark:border-zinc-700";

    public string GetShadowInfo(string shadow) => shadow switch
    {
        "none" => "shadow-none",
        "small" => "shadow-sm",
        "medium" => "shadow-md",
        "large" => "shadow-lg",
        "xlarge" => "shadow-xl",
        _ => "shadow-md"
    };
}

public class ButtonTheme : IButtonTheme
{
    public string Base => "inline-flex items-center justify-center rounded-md font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 disabled:opacity-50 disabled:pointer-events-none";

    public string GetVariant(string variant) => variant?.ToLower() switch
    {
        "primary" => "bg-blue-600 text-white hover:bg-blue-700 shadow-sm",
        "secondary" => "bg-gray-100 text-gray-900 hover:bg-gray-200 dark:bg-zinc-800 dark:text-gray-100 dark:hover:bg-zinc-700",
        "outline" => "border border-gray-300 bg-transparent hover:bg-gray-50 dark:border-zinc-600 dark:hover:bg-zinc-800",
        "ghost" => "hover:bg-gray-100 hover:text-gray-900 dark:hover:bg-zinc-800 dark:hover:text-gray-50",
        "destructive" => "bg-red-500 text-white hover:bg-red-600 shadow-sm",
        "text" => "text-gray-600 hover:text-gray-900 hover:bg-gray-50 dark:text-gray-300 dark:hover:text-white dark:hover:bg-zinc-800",
        "link" => "text-primary underline-offset-4 hover:underline",
        _ => "bg-blue-600 text-white hover:bg-blue-700 shadow-sm"
    };
}

public class InputTheme : IInputTheme
{
    public string Base => "flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 dark:text-white dark:placeholder:text-gray-400";
}

public class CheckboxTheme : ICheckboxTheme
{
    public string Base => "peer h-4 w-4 shrink-0 rounded-sm border border-primary ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground border-gray-300 dark:border-zinc-600";
    public string Checked => "bg-blue-600 border-blue-600 text-white";
    public string Unchecked => "bg-white dark:bg-zinc-800";
}

public class TextTheme : ITextTheme
{
    public string Base => "text-gray-900 dark:text-gray-100";
    public string GetVariant(string variant) => variant?.ToLower() switch
    {
        "h1" => "scroll-m-20 text-4xl font-extrabold tracking-tight lg:text-5xl",
        "h2" => "scroll-m-20 border-b pb-2 text-3xl font-semibold tracking-tight first:mt-0",
        "h3" => "scroll-m-20 text-2xl font-semibold tracking-tight",
        "h4" => "scroll-m-20 text-xl font-semibold tracking-tight",
        "p" => "leading-7 [&:not(:first-child)]:mt-6",
        "lead" => "text-xl text-muted-foreground",
        "large" => "text-lg font-semibold",
        "small" => "text-sm font-medium leading-none",
        "muted" => "text-sm text-muted-foreground",
        _ => ""
    };
}
