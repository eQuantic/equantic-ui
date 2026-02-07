// Counter.cs - Sample eQuantic.UI Page Component
using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Inputs;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Components.Surfaces;

namespace CounterApp.Pages;

[Page("/", Title = "Counter Demo")]
[Page("/counter")]
public class Counter : StatefulComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo)
    {
        seo.Title("Live Counter | eQuantic.UI")
           .Description("A fast and interactive counter built with C# and eQuantic.UI");
    }
    /// <summary>
    /// Example Server Action - processes on the server and returns result.
    /// </summary>
    [ServerAction]
    public async Task<int> IncrementOnServer(int current)
    {
        // Simulate server-side processing
        await Task.Delay(100);
        return current + 1;
    }

    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter>
{
    private int _count = 0;
    private string _message = "";

    private void _increment() => SetState(() => _count++);
    private void _decrement() => SetState(() => _count--);

    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            Id = "main-container",
            ClassName = "eq-flex eq-items-center eq-justify-center eq-min-h-screen eq-p-4",
            Children =
            {
                new Card
                {
                    ClassName = "eq-w-full eq-max-w-md eq-shadow-xl eq-animate-fade-in",
                    Children =
                    {
                        new Container
                        {
                            ClassName = "eq-card-header eq-text-center",
                            Children =
                            {
                                new Heading("eQuantic.UI Counter", 1)
                                {
                                    ClassName = "eq-mb-2"
                                },
                                new Text("Experience the power of C# in the browser")
                                {
                                    Variant = Variant.Secondary,
                                    ClassName = "eq-text-sm"
                                }
                            }
                        },

                        new Container
                        {
                            ClassName = "eq-card-body eq-flex eq-flex-col eq-gap-8",
                            Children =
                            {
                                new Container
                                {
                                    ClassName = "eq-flex eq-flex-col eq-gap-2",
                                    Children =
                                    {
                                        new Text("Dynamic Message") { ClassName = "eq-font-semibold" },
                                        new TextInput
                                        {
                                            Id = "message-input",
                                            Value = _message,
                                            Placeholder = "Type something to see it update below...",
                                            OnChange = (value) => SetState(() => _message = value),
                                            ClassName = "eq-shadow-sm"
                                        }
                                    }
                                },

                                new Container
                                {
                                    ClassName = "eq-bg-surface-subtle eq-p-8 eq-rounded-xl eq-text-center eq-border eq-border-dashed eq-border-primary/20",
                                    Children =
                                    {
                                        new Row
                                        {
                                            Gap = "24px",
                                            Justify = JustifyContent.Center,
                                            Align = AlignItem.Center,
                                            Children =
                                            {
                                                new Button
                                                {
                                                    Id = "decrement-btn",
                                                    Variant = Variant.Secondary,
                                                    OnClick = _decrement,
                                                    Text = "-",
                                                    ClassName = "eq-rounded-full eq-w-12 eq-h-12 eq-p-0 eq-text-xl eq-shadow-md eq-transition-all hover:eq-scale-110"
                                                },

                                                new Text($"{_count}")
                                                {
                                                    ClassName = "eq-text-6xl eq-font-bold eq-text-primary eq-min-w-[80px]"
                                                },

                                                new Button
                                                {
                                                    Id = "increment-btn",
                                                    Variant = Variant.Primary,
                                                    OnClick = _increment,
                                                    Text = "+",
                                                    ClassName = "eq-rounded-full eq-w-12 eq-h-12 eq-p-0 eq-text-xl eq-shadow-md eq-transition-all hover:eq-scale-110"
                                                }
                                            }
                                        }
                                    }
                                },

                                !string.IsNullOrWhiteSpace(_message)
                                    ? new Container
                                      {
                                          ClassName = "eq-p-4 eq-bg-primary-subtle eq-text-primary eq-rounded-lg eq-border eq-border-primary/10 eq-animate-slide-up",
                                          Children = { new Text($"Preview: {_message}") }
                                      }
                                    : new Container()
                            }
                        },

                        new Container
                        {
                            ClassName = "eq-card-footer eq-text-center eq-text-xs eq-text-tertiary",
                            Children = { new Text("Built with eQuantic.UI & .NET 8") }
                        }
                    }
                }
            }
        };
    }
}
