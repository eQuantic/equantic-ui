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
    public IBadgeTheme Badge { get; } = new BadgeTheme();
    public IAlertTheme Alert { get; } = new AlertTheme();
    public ISwitchTheme Switch { get; } = new SwitchTheme();
    public ISelectTheme Select { get; } = new SelectTheme();
    public ITableTheme Table { get; } = new TableTheme();
    public IAvatarTheme Avatar { get; } = new AvatarTheme();
    public IDialogTheme Dialog { get; } = new DialogTheme();
    public ITabsTheme Tabs { get; } = new TabsTheme();
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

public class BadgeTheme : IBadgeTheme
{
    public string Base => "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2";
    
    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["default"] = "border-transparent bg-blue-600 text-white hover:bg-blue-600/80 dark:bg-blue-500 dark:text-white dark:hover:bg-blue-500/80",
        ["secondary"] = "border-transparent bg-gray-100 text-gray-900 hover:bg-gray-100/80 dark:bg-zinc-800 dark:text-gray-100 dark:hover:bg-zinc-800/80",
        ["destructive"] = "border-transparent bg-red-500 text-white hover:bg-red-500/80 dark:bg-red-900 dark:text-white dark:hover:bg-red-900/80",
        ["outline"] = "text-gray-950 dark:text-gray-50 bg-transparent border-gray-200 dark:border-zinc-800",
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "default", out var v) ? v : Variants["default"];
}

public class AlertTheme : IAlertTheme
{
    public string Base => "relative w-full rounded-lg border p-4 [&>svg~*]:pl-7 [&>svg+div]:translate-y-[-3px] [&>svg]:absolute [&>svg]:left-4 [&>svg]:top-4 [&>svg]:text-gray-950 dark:[&>svg]:text-gray-50";
    public string Icon => "h-4 w-4";
    public string Title => "mb-1 font-medium leading-none tracking-tight";
    public string Description => "text-sm [&_p]:leading-relaxed";
    
    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["default"] = "bg-white text-gray-950 border-gray-200 dark:bg-zinc-950 dark:text-gray-50 dark:border-zinc-800",
        ["destructive"] = "border-red-500/50 text-red-500 dark:border-red-500 [&>svg]:text-red-500 dark:border-red-900/50 dark:text-red-900 dark:dark:border-red-900 dark:[&>svg]:text-red-900",
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "default", out var v) ? v : Variants["default"];
}

public class SwitchTheme : ISwitchTheme
{
    public string Root => "peer inline-flex h-[24px] w-[44px] shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 focus-visible:ring-offset-white disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-blue-600 data-[state=unchecked]:bg-gray-200 dark:focus-visible:ring-blue-600 dark:focus-visible:ring-offset-zinc-950 dark:data-[state=checked]:bg-blue-600 dark:data-[state=unchecked]:bg-zinc-700";
    public string Input => ""; // Styling handled by Root
    public string Thumb => "pointer-events-none block h-5 w-5 rounded-full bg-white shadow-lg ring-0 transition-transform data-[state=checked]:translate-x-5 data-[state=unchecked]:translate-x-0 dark:bg-zinc-950";
    public string Track => "";
}

public class SelectTheme : ISelectTheme
{
    public string Trigger => "flex h-10 w-full items-center justify-between rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm ring-offset-white placeholder:text-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 [&>span]:line-clamp-1 dark:border-zinc-800 dark:bg-zinc-950 dark:ring-offset-zinc-950 dark:placeholder:text-gray-400 dark:focus:ring-blue-600";
    public string Content => "relative z-50 min-w-[8rem] overflow-hidden rounded-md border border-gray-200 bg-white text-gray-950 shadow-md data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 dark:border-zinc-800 dark:bg-zinc-950 dark:text-gray-50";
    public string Item => "relative flex w-full cursor-default select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none focus:bg-gray-100 focus:text-gray-900 data-[disabled]:pointer-events-none data-[disabled]:opacity-50 dark:focus:bg-zinc-800 dark:focus:text-gray-50";
    public string Base => "flex h-10 w-full rounded-md border border-gray-200 bg-white px-3 py-2 text-sm ring-offset-white file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-gray-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-800 dark:bg-zinc-950 dark:ring-offset-zinc-950 dark:placeholder:text-gray-400 dark:focus-visible:ring-blue-600";
}

public class TableTheme : ITableTheme
{
    public string Wrapper => "relative w-full overflow-auto";
    public string Table => "w-full caption-bottom text-sm";
    public string Header => "[&_tr]:border-b";
    public string Row => "border-b transition-colors hover:bg-gray-100/50 data-[state=selected]:bg-gray-100 dark:hover:bg-zinc-800/50 dark:data-[state=selected]:bg-zinc-800";
    public string HeadCell => "h-12 px-4 text-left align-middle font-medium text-gray-500 [&:has([role=checkbox])]:pr-0 dark:text-gray-400";
    public string Cell => "p-4 align-middle [&:has([role=checkbox])]:pr-0";
}

public class AvatarTheme : IAvatarTheme
{
    public string Root => "relative flex h-10 w-10 shrink-0 overflow-hidden rounded-full";
    public string Image => "aspect-square h-full w-full";
    public string Fallback => "flex h-full w-full items-center justify-center rounded-full bg-gray-100 dark:bg-zinc-800";
}

public class DialogTheme : IDialogTheme
{
    public string Overlay => "fixed inset-0 z-50 bg-black/80  data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0";
    public string Content => "fixed left-[50%] top-[50%] z-50 grid w-full max-w-lg translate-x-[-50%] translate-y-[-50%] gap-4 border border-gray-200 bg-white p-6 shadow-lg duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[state=closed]:slide-out-to-left-1/2 data-[state=closed]:slide-out-to-top-[48%] data-[state=open]:slide-in-from-left-1/2 data-[state=open]:slide-in-from-top-[48%] sm:rounded-lg dark:border-zinc-800 dark:bg-zinc-950";
    public string Header => "flex flex-col space-y-1.5 text-center sm:text-left";
    public string Title => "text-lg font-semibold leading-none tracking-tight";
    public string Description => "text-sm text-gray-500 dark:text-gray-400";
    public string Footer => "flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2";
}

public class TabsTheme : ITabsTheme
{
    public string List => "inline-flex h-10 items-center justify-center rounded-md bg-gray-100 p-1 text-gray-500 dark:bg-zinc-800 dark:text-gray-400";
    public string Trigger => "inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-white transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 dark:ring-offset-zinc-950 dark:focus-visible:ring-blue-600 cursor-pointer";
    public string Content => "mt-2 ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 dark:ring-offset-zinc-950 dark:focus-visible:ring-blue-600 animate-in fade-in duration-200";
    public string ActiveTrigger => "bg-white text-gray-950 shadow-sm dark:bg-zinc-950 dark:text-gray-50";
    public string InactiveTrigger => "hover:bg-gray-200 hover:text-gray-900 dark:hover:bg-zinc-700 dark:hover:text-gray-100";
}
