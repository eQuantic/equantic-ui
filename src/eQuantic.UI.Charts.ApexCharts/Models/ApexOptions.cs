using System.Collections.Generic;

namespace eQuantic.UI.Charts.ApexCharts.Models;

public class ApexOptions
{
    public ChartOptions Chart { get; set; } = new();
    public StrokeOptions Stroke { get; set; } = new();
    public XAxisOptions XAxis { get; set; } = new();
    public List<YAxisOptions> YAxis { get; set; } = new();
    public LegendOptions Legend { get; set; } = new();
    public DataLabelsOptions DataLabels { get; set; } = new();
    public TitleOptions Title { get; set; } = new();
    public List<string>? Labels { get; set; }
    public List<string>? Colors { get; set; }
}

public class ChartOptions
{
    public string Type { get; set; } = "line";
    public string? Height { get; set; } = "350";
    public string? Width { get; set; } = "100%";
    public ToolbarOptions Toolbar { get; set; } = new();
    public AnimationsOptions Animations { get; set; } = new();
}

public class ToolbarOptions { public bool Show { get; set; } = true; }
public class AnimationsOptions { public bool Enabled { get; set; } = true; }

public class StrokeOptions
{
    public string Curve { get; set; } = "smooth"; // 'smooth', 'straight', 'stepline'
    public double Width { get; set; } = 2;
}

public class XAxisOptions
{
    public string Type { get; set; } = "category";
    public List<string>? Categories { get; set; }
    public TitleOptions Title { get; set; } = new();
}

public class YAxisOptions
{
    public bool Show { get; set; } = true;
    public TitleOptions Title { get; set; } = new();
}

public class LegendOptions
{
    public bool Show { get; set; } = true;
    public string Position { get; set; } = "top";
}

public class DataLabelsOptions { public bool Enabled { get; set; } = false; }
public class TitleOptions { public string? Text { get; set; } public string Align { get; set; } = "left"; }

public class ApexSeries<T>
{
    public string? Name { get; set; }
    public List<T> Data { get; set; } = new();
}
