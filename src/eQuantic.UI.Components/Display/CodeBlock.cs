using System;
using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Components.Display;

/// <summary>
/// Renders a code block with syntax highlighting and copy/expand features.
/// </summary>
public class CodeBlock : StatelessComponent
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

    public override IComponent Build(RenderContext context)
    {
        var id = Id ?? $"code-{Guid.NewGuid():N}";
        var languageClass = $"language-{Language}";
        var isCollapsible = Collapsible; // Could add line count check here
        
        var containerClass = StyleBuilder.Create("relative rounded-lg bg-zinc-950 border border-zinc-800 group my-4")
            .Add(ClassName)
            .Build();

        var contentClass = StyleBuilder.Create("overflow-x-auto p-4 text-sm text-zinc-50 scrollbar-thin scrollbar-thumb-zinc-700 scrollbar-track-transparent")
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
                    ["class"] = "absolute top-2 right-2 opacity-0 group-hover:opacity-100 px-2 py-1 text-xs font-medium text-zinc-400 hover:text-white bg-zinc-800/50 hover:bg-zinc-700 border border-zinc-700/50 rounded transition-all backdrop-blur-sm z-10"
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
                    ["class"] = "w-full py-2 text-xs font-medium text-zinc-400 hover:text-white bg-zinc-900/80 hover:bg-zinc-800 border-t border-zinc-800 transition-colors rounded-b-lg"
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
