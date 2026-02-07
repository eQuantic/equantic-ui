using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Surfaces;
using DefaultUIDashboard.Components;

namespace DefaultUIDashboard.Pages;

[Page("/counter", Title = "Counter Demo")]
public class Counter : StatefulComponent
{
    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter>
{
    private int _count = 0;

    public void Increment()
    {
        SetState(() => _count++);
    }

    public void Decrement()
    {
        SetState(() => _count--);
    }

    public void Reset()
    {
        SetState(() => _count = 0);
    }

    public override IComponent Build(RenderContext context)
    {
        return new DefaultDashboardShell
        {
            ActivePath = "/counter",
            Children = {
                new Box {
                    ClassName = "eq-max-w-md eq-mx-auto eq-mt-20",
                    Children = {
                        new Card {
                            ClassName = "eq-surface-subtle eq-border eq-p-8 eq-text-center",
                            Children = {
                                new Heading("Interactive Counter", 1) { ClassName = "eq-text-3xl eq-font-bold eq-mb-8" },
                                new Box {
                                    ClassName = "eq-text-7xl eq-font-black eq-mb-12 eq-text-info",
                                    Children = { new Text(_count.ToString()) }
                                },
                                new Box {
                                    ClassName = "flex items-center justify-center gap-4",
                                    Children = {
                                        new Button { Text = "−", OnClick = Decrement, ClassName = "eq-w-16 eq-h-16 eq-text-2xl eq-rounded-xl eq-btn-outline eq-transition-all active:eq-scale-95" },
                                        new Button { Text = "Reset", OnClick = Reset, ClassName = "eq-px-8 eq-h-16 eq-text-lg eq-font-medium eq-rounded-xl eq-btn-outline eq-transition-all active:eq-scale-95" },
                                        new Button { Text = "+", OnClick = Increment, ClassName = "eq-w-16 eq-h-16 eq-text-2xl eq-rounded-xl eq-btn-primary eq-transition-all active:eq-scale-95 eq-shadow-lg" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
