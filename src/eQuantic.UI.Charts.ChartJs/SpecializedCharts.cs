using eQuantic.UI.Charts.ChartJs.Models;

namespace eQuantic.UI.Charts.ChartJs;

public class LineChart<T> : ChartJs<T>
{
    public LineChart()
    {
        Type = "line";
    }
}

public class BarChart<T> : ChartJs<T>
{
    public BarChart()
    {
        Type = "bar";
    }
}

public class PieChart<T> : ChartJs<T>
{
    public PieChart()
    {
        Type = "pie";
    }
}

public class DoughnutChart<T> : ChartJs<T>
{
    public DoughnutChart()
    {
        Type = "doughnut";
    }
}

public class RadarChart<T> : ChartJs<T>
{
    public RadarChart()
    {
        Type = "radar";
    }
}
