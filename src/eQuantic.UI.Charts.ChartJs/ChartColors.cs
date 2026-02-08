namespace eQuantic.UI.Charts.ChartJs;

public static class ChartColors
{
    public const string Blue = "rgba(54, 162, 235, 0.5)";
    public const string Red = "rgba(255, 99, 132, 0.5)";
    public const string Yellow = "rgba(255, 205, 86, 0.5)";
    public const string Green = "rgba(75, 192, 192, 0.5)";
    public const string Purple = "rgba(153, 102, 255, 0.5)";
    public const string Orange = "rgba(255, 159, 64, 0.5)";
    public const string Grey = "rgba(201, 203, 207, 0.5)";

    public static string WithOpacity(string color, double opacity)
    {
        if (color.StartsWith("rgba("))
        {
            var parts = color.Split(',');
            if (parts.Length == 4)
            {
                return $"{parts[0]},{parts[1]},{parts[2]},{opacity})";
            }
        }
        return color;
    }
}
