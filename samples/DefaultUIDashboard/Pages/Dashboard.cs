using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using DefaultUIDashboard.Components;

namespace DefaultUIDashboard.Pages;

[Page("/", Title = "Default Dashboard")]
public class Dashboard : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DefaultDashboardShell
        {
            ActivePath = "/",
            Children = {
                new Grid {
                    Columns = 4,
                    Gap = "1.5rem",
                    Children = {
                        new QuickStat("Live Users", "1,284", "eq-text-success"),
                        new QuickStat("Server Load", "42%", "eq-text-warning"),
                        new QuickStat("Response Time", "12ms", "eq-text-info"),
                        new QuickStat("Error Rate", "0.02%", "eq-text-error")
                    }
                },
                new Box {
                    ClassName = "eq-mt-12 eq-grid eq-grid-cols-3 eq-gap-8",
                    Children = {
                        new Box {
                            ClassName = "eq-col-span-2 eq-surface-subtle eq-border eq-rounded-xl eq-p-8",
                            Children = {
                                new Heading("System Performance", 2) { ClassName = "eq-text-xl eq-font-bold eq-mb-6" },
                                new Box { ClassName = "eq-h-64 eq-border eq-border-dashed eq-rounded-lg eq-flex eq-items-center eq-justify-center eq-text-secondary", Children = { new Text("Real-time graph") } }
                            }
                        },
                        new Box {
                            ClassName = "eq-surface-subtle eq-border eq-rounded-xl eq-p-8",
                            Children = {
                                new Heading("Internal Logs", 2) { ClassName = "eq-text-xl eq-font-bold eq-mb-6" },
                                new Box {
                                    ClassName = "eq-space-y-4",
                                    Children = {
                                        new LogEntry("System boot sequence complete", "12:00:01"),
                                        new LogEntry("Registry sync started", "12:00:05"),
                                        new LogEntry("Tailwind styles extracted", "12:00:10"),
                                        new LogEntry("C# hydration successful", "12:00:15")
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

public class QuickStat : StatelessComponent
{
    private readonly string _title;
    private readonly string _value;
    private readonly string _colorClass;

    public QuickStat(string title, string value, string colorClass)
    {
        _title = title;
        _value = value;
        _colorClass = colorClass;
    }

    public override IComponent Build(RenderContext context)
    {
        return new Box {
            ClassName = "eq-surface-hover eq-border eq-p-6 eq-rounded-xl eq-shadow-sm",
            Children = {
                new Text(_title) { ClassName = "eq-text-secondary eq-text-sm eq-font-medium eq-mb-2 eq-block" },
                new Box {
                    ClassName = "flex items-end justify-between",
                    Children = {
                        new Text(_value) { ClassName = "eq-text-3xl eq-font-black" },
                        new Box { ClassName = $"eq-px-3 eq-py-1 eq-rounded-full eq-text-xs eq-font-bold {_colorClass}", Children = { new Text("LIVE") } }
                    }
                }
            }
        };
    }
}

public class LogEntry : StatelessComponent
{
    private readonly string _msg;
    private readonly string _time;

    public LogEntry(string msg, string time)
    {
        _msg = msg;
        _time = time;
    }

    public override IComponent Build(RenderContext context)
    {
        return new Box {
            ClassName = "flex items-start gap-3",
            Children = {
                new Box { ClassName = "eq-w-1.5 eq-h-1.5 eq-rounded-full eq-surface-active eq-mt-2" },
                new Box {
                    Children = {
                        new Text(_msg) { ClassName = "eq-text-sm eq-text-primary eq-block" },
                        new Text(_time) { ClassName = "eq-text-xs eq-text-secondary" }
                    }
                }
            }
        };
    }
}
