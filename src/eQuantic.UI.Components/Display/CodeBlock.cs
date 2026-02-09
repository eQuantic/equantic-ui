using System;
using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Assets;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Components.Display;

/// <summary>
/// Renders a code block with syntax highlighting and copy/expand features.
/// </summary>
public class CodeBlock : StatelessComponent, IRequireAssets
{
    /// <summary>
    /// The source code to display.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Programming language (e.g., "csharp", "html"). Default: "csharp".
    /// </summary>
    public string Language { get; set; } = "csharp";

    /// <summary>
    /// If true, shows "Expand Code" button and collapses content initially.
    /// </summary>
    public bool Collapsible { get; set; }

    public CodeBlock() { }

    public CodeBlock(string code, string language = "csharp")
    {
        Code = code;
        Language = language;
    }

    public void ConfigureAssets(AssetBuilder assets)
    {
        // Prism.js syntax highlighting with dark/light theme support
        assets.AddStylesheet(
            "https://cdn.jsdelivr.net/npm/prismjs@1.29.0/themes/prism-tomorrow.min.css",
            id: "prism-theme");
        assets.AddScript("https://cdn.jsdelivr.net/npm/prismjs@1.29.0/prism.min.js");
        assets.AddScript("https://cdn.jsdelivr.net/npm/prismjs@1.29.0/plugins/autoloader/prism-autoloader.min.js");

        // CodeBlock utility functions + theme switching + highlight trigger
        assets.AddInlineScript(
            "function copyToClipboard(id){var e=document.getElementById(id);if(e){navigator.clipboard.writeText(e.textContent||'').then(function(){var b=document.getElementById('btn-'+id);if(b){b.textContent='Copied!';setTimeout(function(){b.textContent='Copy'},2e3)}})}}" +
            "function toggleCodeBlock(id){var c=document.getElementById('container-'+id);var b=document.getElementById('expand-'+id);if(c){var ex=c.classList.contains('max-h-32');c.classList.toggle('max-h-32');c.classList.toggle('overflow-hidden');if(b)b.textContent=ex?'Collapse Code':'Expand Code'}}" +
            "(function(){" +
                "var base='https://cdn.jsdelivr.net/npm/prismjs@1.29.0/themes/';" +
                "function updateTheme(){var l=document.getElementById('prism-theme');if(l){var d=document.documentElement.classList.contains('dark');var h=base+(d?'prism-tomorrow.min.css':'prism.min.css');if(l.getAttribute('href')!==h)l.setAttribute('href',h)}}" +
                "updateTheme();" +
                "new MutationObserver(function(){updateTheme()}).observe(document.documentElement,{attributes:true,attributeFilter:['class']});" +
                "document.addEventListener('DOMContentLoaded',function(){if(typeof Prism!=='undefined')Prism.highlightAll()});" +
            "})();"
        );
    }

    public override IComponent Build(RenderContext context)
    {
        var id = Id ?? $"code-{Guid.NewGuid():N}";
        var languageClass = $"language-{Language}";
        var isCollapsible = Collapsible; // Could add line count check here
        
        var containerClass = StyleBuilder.Create("relative rounded-lg bg-zinc-50 dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 group my-4")
            .Add(ClassName)
            .Build();

        var contentClass = StyleBuilder.Create("overflow-x-auto p-4 !text-xs text-zinc-900 dark:text-zinc-50 scrollbar-thin scrollbar-thumb-zinc-300 dark:scrollbar-thumb-zinc-700 scrollbar-track-transparent")
            .Add("max-h-32 overflow-hidden", isCollapsible)
            .Build();

        var children = new List<IComponent>
        {
            // Copy Button (Absolute Top Right)
            new DynamicElement {
                TagName = "button",
                InnerText = "Copy",
                CustomAttributes = new Dictionary<string, string> {
                    ["id"] = $"btn-{id}",
                    ["onclick"] = $"copyToClipboard('{id}')",
                    ["class"] = "absolute top-2 right-2 opacity-0 group-hover:opacity-100 px-2 py-1 text-xs font-medium text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white bg-zinc-200/50 dark:bg-zinc-800/50 hover:bg-zinc-300 dark:hover:bg-zinc-700 border border-zinc-300/50 dark:border-zinc-700/50 rounded transition-all backdrop-blur-sm z-10"
                }
            },
            
            // Script trigger for highlighting (if specific block needs it, but Autoloader handles strictly)
             
            // Code Wrapper
            new DynamicElement {
                TagName = "pre",
                CustomAttributes = new Dictionary<string, string> {
                    ["id"] = $"container-{id}",
                    ["class"] = contentClass,
                    ["style"] = "margin: 0;" // Override user agent pre margin
                },
                Children = {
                    new DynamicElement {
                        TagName = "code",
                        InnerText = Code,
                        CustomAttributes = new Dictionary<string, string> {
                            ["id"] = id,
                            ["class"] = $"font-mono {languageClass}"
                        }
                    }
                }
            }
        };

        // Expand Button (Bottom)
        if (isCollapsible)
        {
            children.Add(new DynamicElement {
                TagName = "button",
                InnerText = "Expand Code",
                CustomAttributes = new Dictionary<string, string> {
                    ["id"] = $"expand-{id}",
                    ["onclick"] = $"toggleCodeBlock('{id}')",
                    ["class"] = "w-full py-2 text-xs font-medium text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white bg-zinc-100/80 dark:bg-zinc-900/80 hover:bg-zinc-200 dark:hover:bg-zinc-800 border-t border-zinc-200 dark:border-zinc-800 transition-colors rounded-b-lg"
                }
            });
        }

        var wrapper = new DynamicElement {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string> { ["class"] = containerClass }
        };
        
        foreach (var child in children)
        {
            wrapper.Children.Add(child);
        }

        return wrapper;
    }
}
