namespace eQuantic.UI.Charts.ChartJs.Models;

public class ChartJsOptions
{
    public bool Responsive { get; set; } = true;
    public bool MaintainAspectRatio { get; set; } = false;
    public PluginsOptions Plugins { get; set; } = new();
    public ScalesOptions Scales { get; set; } = new();
}

public class PluginsOptions
{
    public LegendOptions Legend { get; set; } = new();
    public TitleOptions Title { get; set; } = new();
    public TooltipOptions Tooltip { get; set; } = new();
}

public class LegendOptions
{
    public bool Display { get; set; } = true;
    public string Position { get; set; } = "top"; // Better to use Enum later
}

public class TitleOptions
{
    public bool Display { get; set; } = false;
    public string? Text { get; set; }
}

public class TooltipOptions
{
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "index";
    public bool Intersect { get; set; } = false;
}

public class ScalesOptions
{
    public ScaleOptions? X { get; set; }
    public ScaleOptions? Y { get; set; }
}

public class ScaleOptions
{
    public bool Display { get; set; } = true;
    public string Type { get; set; } = "linear";
    public GridOptions Grid { get; set; } = new();
    public TitleOptions Title { get; set; } = new();
}

public class GridOptions
{
    public bool Display { get; set; } = true;
    public string? Color { get; set; }
}
