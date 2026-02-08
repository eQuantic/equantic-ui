using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

public class CodeBlockScripts : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DynamicElement
        {
            TagName = "div",
            Children = {
                new DynamicElement
                {
                    TagName = "link",
                    CustomAttributes = new Dictionary<string, string>
                    {
                        ["rel"] = "stylesheet",
                        ["href"] = "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css"
                    }
                },
                new DynamicElement
                {
                    TagName = "script",
                    CustomAttributes = new Dictionary<string, string>
                    {
                        ["src"] = "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-core.min.js"
                    }
                },
                new DynamicElement
                {
                    TagName = "script",
                    CustomAttributes = new Dictionary<string, string>
                    {
                        ["src"] = "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/plugins/autoloader/prism-autoloader.min.js"
                    }
                },
                new DynamicElement
                {
                    TagName = "script",
                    InnerText = """
                        window.copyToClipboard = function(id) {
                            const code = document.getElementById(id).innerText;
                            navigator.clipboard.writeText(code).then(() => {
                                const btn = document.getElementById('btn-' + id);
                                const original = btn.innerText;
                                btn.innerText = 'Copied!';
                                setTimeout(() => btn.innerText = original, 2000);
                            });
                        }

                        window.toggleCodeBlock = function(id) {
                            const container = document.getElementById('container-' + id);
                            const btn = document.getElementById('expand-' + id);
                            const isCollapsed = container.classList.contains('max-h-32');
                            
                            if (isCollapsed) {
                                container.classList.remove('max-h-32');
                                container.classList.remove('overflow-hidden');
                                btn.innerText = 'Collapse Code';
                            } else {
                                container.classList.add('max-h-32');
                                container.classList.add('overflow-hidden');
                                btn.innerText = 'Expand Code';
                            }
                        }
                    """
                }
            }
        };
    }
}
