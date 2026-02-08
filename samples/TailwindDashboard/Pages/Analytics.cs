using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Components.Surfaces;
using eQuantic.UI.Charts.ChartJs;
using eQuantic.UI.Charts.ChartJs.Models;
using eQuantic.UI.Charts.ApexCharts;
using eQuantic.UI.Charts.ApexCharts.Models;
using eQuantic.UI.Lucide;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages;

[Page("/analytics", Title = "Analytics")]
public class Analytics : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            PageTitle = "Analytics",
            ActivePath = "/analytics",
            Children =
            {
                new Box
                {
                    ClassName = "space-y-8",
                    Children =
                    {
                        // KPI Cards row
                        new Grid
                        {
                            Columns = 4,
                            Gap = "1.5rem",
                            ClassName = "grid-cols-1 sm:grid-cols-2 lg:grid-cols-4",
                            Children =
                            {
                                CreateKpiCard("Total Revenue", "$45,231.89", "+20.1% from last month",
                                    LucideIcons.DollarSign(size: 20, strokeWidth: 1.75)),
                                CreateKpiCard("Subscriptions", "+2,350", "+180.1% from last month",
                                    LucideIcons.Users(size: 20, strokeWidth: 1.75)),
                                CreateKpiCard("Sales", "+12,234", "+19% from last month",
                                    LucideIcons.CreditCard(size: 20, strokeWidth: 1.75)),
                                CreateKpiCard("Active Now", "+573", "+201 since last hour",
                                    LucideIcons.Activity(size: 20, strokeWidth: 1.75))
                            }
                        },

                        // Charts row 1: Revenue (Chart.js Line) + Users (ApexCharts Bar)
                        new Grid
                        {
                            Columns = 2,
                            Gap = "1.5rem",
                            ClassName = "grid-cols-1 lg:grid-cols-2",
                            Children =
                            {
                                // Chart.js - Revenue line chart
                                new Card
                                {
                                    Children =
                                    {
                                        new CardHeader
                                        {
                                            Children =
                                            {
                                                new CardTitle { Text = "Revenue Over Time" },
                                                new Text("Monthly revenue for the current year") { ClassName = "text-sm text-muted-foreground" }
                                            }
                                        },
                                        new CardBody
                                        {
                                            Children =
                                            {
                                                new LineChart<double>
                                                {
                                                    Height = "320px",
                                                    Data = new()
                                                    {
                                                        Labels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
                                                        Datasets =
                                                        [
                                                            new ChartJsDataset<double>
                                                            {
                                                                Label = "Revenue ($)",
                                                                Data = [4200, 5100, 6800, 5900, 8200, 9100, 7800, 10200, 11500, 9800, 12400, 14100],
                                                                BorderColor = "#3b82f6",
                                                                BackgroundColor = "rgba(59, 130, 246, 0.08)",
                                                                Fill = true,
                                                                Tension = 0.4,
                                                                BorderWidth = 2
                                                            },
                                                            new ChartJsDataset<double>
                                                            {
                                                                Label = "Expenses ($)",
                                                                Data = [3100, 3400, 4200, 3800, 5100, 5600, 4900, 6100, 6800, 5900, 7200, 8100],
                                                                BorderColor = "#f43f5e",
                                                                BackgroundColor = "rgba(244, 63, 94, 0.08)",
                                                                Fill = true,
                                                                Tension = 0.4,
                                                                BorderWidth = 2
                                                            }
                                                        ]
                                                    },
                                                    Options = new()
                                                    {
                                                        Plugins = new()
                                                        {
                                                            Legend = new() { Position = "bottom" }
                                                        },
                                                        Scales = new()
                                                        {
                                                            Y = new()
                                                            {
                                                                Grid = new() { Color = "rgba(255,255,255,0.06)" }
                                                            },
                                                            X = new()
                                                            {
                                                                Grid = new() { Display = false }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },

                                // ApexCharts - User engagement bar chart
                                new Card
                                {
                                    Children =
                                    {
                                        new CardHeader
                                        {
                                            Children =
                                            {
                                                new CardTitle { Text = "User Engagement" },
                                                new Text("Active vs new users by day") { ClassName = "text-sm text-muted-foreground" }
                                            }
                                        },
                                        new CardBody
                                        {
                                            Children =
                                            {
                                                new ApexChart<int>
                                                {
                                                    Series =
                                                    [
                                                        new ApexSeries<int> { Name = "Active Users", Data = [44, 55, 41, 67, 22, 43, 56] },
                                                        new ApexSeries<int> { Name = "New Users", Data = [13, 23, 20, 8, 13, 27, 18] }
                                                    ],
                                                    Options = new()
                                                    {
                                                        Chart = new() { Type = "bar", Height = "320" },
                                                        Colors = ["#10b981", "#6366f1"],
                                                        XAxis = new() { Categories = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] },
                                                        Legend = new() { Position = "bottom" },
                                                        DataLabels = new() { Enabled = false },
                                                        Stroke = new() { Width = 0 }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },

                        // Charts row 2: Distribution (Doughnut) + Traffic (Area) + Performance (Radar)
                        new Grid
                        {
                            Columns = 3,
                            Gap = "1.5rem",
                            ClassName = "grid-cols-1 md:grid-cols-2 lg:grid-cols-3",
                            Children =
                            {
                                // Chart.js - Doughnut chart
                                new Card
                                {
                                    Children =
                                    {
                                        new CardHeader
                                        {
                                            Children =
                                            {
                                                new CardTitle { Text = "Traffic Sources" },
                                                new Text("Where your visitors come from") { ClassName = "text-sm text-muted-foreground" }
                                            }
                                        },
                                        new CardBody
                                        {
                                            Children =
                                            {
                                                new DoughnutChart<double>
                                                {
                                                    Height = "280px",
                                                    Data = new()
                                                    {
                                                        Labels = ["Direct", "Organic", "Referral", "Social", "Email"],
                                                        Datasets =
                                                        [
                                                            new ChartJsDataset<double>
                                                            {
                                                                Data = [35, 28, 18, 12, 7],
                                                                BackgroundColor = "#3b82f6",
                                                                BorderWidth = 0
                                                            }
                                                        ]
                                                    },
                                                    Options = new()
                                                    {
                                                        Plugins = new()
                                                        {
                                                            Legend = new() { Position = "bottom" }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },

                                // ApexCharts - Area chart
                                new Card
                                {
                                    Children =
                                    {
                                        new CardHeader
                                        {
                                            Children =
                                            {
                                                new CardTitle { Text = "Page Views" },
                                                new Text("Weekly page view trends") { ClassName = "text-sm text-muted-foreground" }
                                            }
                                        },
                                        new CardBody
                                        {
                                            Children =
                                            {
                                                new ApexChart<int>
                                                {
                                                    Series =
                                                    [
                                                        new ApexSeries<int> { Name = "Views", Data = [31, 40, 28, 51, 42, 109, 100] }
                                                    ],
                                                    Options = new()
                                                    {
                                                        Chart = new() { Type = "area", Height = "280" },
                                                        Colors = ["#8b5cf6"],
                                                        Stroke = new() { Curve = "smooth", Width = 2 },
                                                        DataLabels = new() { Enabled = false },
                                                        XAxis = new() { Categories = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },

                                // Chart.js - Radar chart
                                new Card
                                {
                                    Children =
                                    {
                                        new CardHeader
                                        {
                                            Children =
                                            {
                                                new CardTitle { Text = "Performance" },
                                                new Text("This month vs last month") { ClassName = "text-sm text-muted-foreground" }
                                            }
                                        },
                                        new CardBody
                                        {
                                            Children =
                                            {
                                                new RadarChart<double>
                                                {
                                                    Height = "280px",
                                                    Data = new()
                                                    {
                                                        Labels = ["Speed", "Reliability", "Comfort", "Safety", "Efficiency", "Support"],
                                                        Datasets =
                                                        [
                                                            new ChartJsDataset<double>
                                                            {
                                                                Label = "This Month",
                                                                Data = [85, 90, 78, 92, 88, 76],
                                                                BorderColor = "#3b82f6",
                                                                BackgroundColor = "rgba(59, 130, 246, 0.15)",
                                                                BorderWidth = 2
                                                            },
                                                            new ChartJsDataset<double>
                                                            {
                                                                Label = "Last Month",
                                                                Data = [72, 85, 70, 88, 80, 82],
                                                                BorderColor = "#f59e0b",
                                                                BackgroundColor = "rgba(245, 158, 11, 0.15)",
                                                                BorderWidth = 2
                                                            }
                                                        ]
                                                    },
                                                    Options = new()
                                                    {
                                                        Plugins = new()
                                                        {
                                                            Legend = new() { Position = "bottom" }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },

                        // Charts row 3: Mixed bar chart (full width)
                        new Card
                        {
                            Children =
                            {
                                new CardHeader
                                {
                                    Children =
                                    {
                                        new CardTitle { Text = "Monthly Comparison" },
                                        new Text("Revenue, costs, and profit across all months") { ClassName = "text-sm text-muted-foreground" }
                                    }
                                },
                                new CardBody
                                {
                                    Children =
                                    {
                                        new BarChart<double>
                                        {
                                            Height = "350px",
                                            Data = new()
                                            {
                                                Labels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
                                                Datasets =
                                                [
                                                    new ChartJsDataset<double>
                                                    {
                                                        Label = "Revenue",
                                                        Data = [4200, 5100, 6800, 5900, 8200, 9100, 7800, 10200, 11500, 9800, 12400, 14100],
                                                        BackgroundColor = "rgba(59, 130, 246, 0.7)",
                                                        BorderColor = "#3b82f6",
                                                        BorderWidth = 1
                                                    },
                                                    new ChartJsDataset<double>
                                                    {
                                                        Label = "Costs",
                                                        Data = [3100, 3400, 4200, 3800, 5100, 5600, 4900, 6100, 6800, 5900, 7200, 8100],
                                                        BackgroundColor = "rgba(244, 63, 94, 0.7)",
                                                        BorderColor = "#f43f5e",
                                                        BorderWidth = 1
                                                    },
                                                    new ChartJsDataset<double>
                                                    {
                                                        Label = "Profit",
                                                        Data = [1100, 1700, 2600, 2100, 3100, 3500, 2900, 4100, 4700, 3900, 5200, 6000],
                                                        BackgroundColor = "rgba(16, 185, 129, 0.7)",
                                                        BorderColor = "#10b981",
                                                        BorderWidth = 1
                                                    }
                                                ]
                                            },
                                            Options = new()
                                            {
                                                Plugins = new()
                                                {
                                                    Legend = new() { Position = "bottom" }
                                                },
                                                Scales = new()
                                                {
                                                    Y = new()
                                                    {
                                                        Grid = new() { Color = "rgba(255,255,255,0.06)" }
                                                    },
                                                    X = new()
                                                    {
                                                        Grid = new() { Display = false }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static IComponent CreateKpiCard(string title, string value, string change, IComponent icon)
    {
        return new Card
        {
            Children =
            {
                new CardBody
                {
                    ClassName = "p-6",
                    Children =
                    {
                        new Flex
                        {
                            Justify = JustifyContent.SpaceBetween,
                            Align = AlignItem.FlexStart,
                            Children =
                            {
                                new Box
                                {
                                    ClassName = "space-y-1",
                                    Children =
                                    {
                                        new Text(title) { ClassName = "text-sm font-medium text-muted-foreground" },
                                        new Text(value) { ClassName = "text-2xl font-bold tracking-tight" }
                                    }
                                },
                                new Box
                                {
                                    ClassName = "p-2 rounded-lg bg-muted/50 text-muted-foreground",
                                    Children = { icon }
                                }
                            }
                        },
                        new Text(change) { ClassName = "text-xs text-muted-foreground mt-2" }
                    }
                }
            }
        };
    }
}
