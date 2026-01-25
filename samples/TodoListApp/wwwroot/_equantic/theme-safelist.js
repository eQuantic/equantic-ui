// This file ensures that Tailwind classes used in eQuantic.UI.Tailwind are detected by the scanner.
// The scanner is configured to look at ./wwwroot/_equantic/**/*.js
// Since the AppTheme C# code is external, we must verify these classes exist here.

const themeSafelist = [
    // CARD THEME
    "flex", "flex-col", "bg-white", "dark:bg-zinc-800", "rounded-lg", "overflow-hidden", "border", "border-gray-200", "dark:border-zinc-700", "transition-all",
    "w-full", "px-6", "py-4", "border-b", "bg-gray-50", "dark:bg-zinc-800/50",
    "p-6",
    "border-t",
    "shadow-none", "shadow-sm", "shadow-md", "shadow-lg", "shadow-xl",

    // BUTTON THEME
    "inline-flex", "items-center", "justify-center", "rounded-md", "font-medium", "transition-colors", 
    "focus-visible:outline-none", "focus-visible:ring-2", "focus-visible:ring-offset-2", 
    "disabled:opacity-50", "disabled:pointer-events-none",
    
    // Button Variants
    "bg-blue-600", "text-white", "hover:bg-blue-700",
    "bg-gray-100", "text-gray-900", "hover:bg-gray-200", "dark:text-gray-100", "dark:hover:bg-zinc-700",
    "border-gray-300", "bg-transparent", "hover:bg-gray-50", "dark:border-zinc-600", "dark:hover:bg-zinc-800",
    "hover:text-gray-900", "dark:hover:text-gray-50",
    "bg-red-500", "hover:bg-red-600",
    "text-gray-600", "hover:bg-gray-50", "dark:text-gray-300", "dark:hover:text-white",
    "text-primary", "underline-offset-4", "hover:underline",

    // INPUT THEME
    "bg-background", "px-3", "py-2", "text-sm", "ring-offset-background", 
    "file:border-0", "file:bg-transparent", "file:text-sm", "file:font-medium", 
    "placeholder:text-muted-foreground", "focus-visible:ring-ring", 
    "disabled:cursor-not-allowed",
    "dark:text-white", "dark:placeholder:text-gray-400",

    // CHECKBOX THEME
    "peer", "h-4", "w-4", "shrink-0", "rounded-sm", "border-primary", 
    "data-[state=checked]:bg-primary", "data-[state=checked]:text-primary-foreground",
    "border-blue-600",

    // TYPOGRAPHY THEME
    "scroll-m-20", "text-4xl", "font-extrabold", "tracking-tight", "lg:text-5xl",
    "pb-2", "text-3xl", "font-semibold", "first:mt-0",
    "text-2xl", "text-xl",
    "leading-7", "[&:not(:first-child)]:mt-6",
    "text-muted-foreground",
    "text-lg",
    "font-medium", "leading-none"
];
