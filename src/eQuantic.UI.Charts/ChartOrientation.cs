namespace eQuantic.UI.Charts;

/// <summary>Which way the bars grow. Vertical bars are columns; horizontal bars read long category
/// names best, which is what the method recommends for part-to-whole with many categories.</summary>
public enum ChartOrientation : byte
{
    Vertical = 0,
    Horizontal = 1,
}
