using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Feedback;

/// <summary>
/// Displays a progress bar indicating completion status (0-100).
/// </summary>
public class Progress : StatelessComponent
{
    /// <summary>
    /// Current progress value (0-100). Default: 0.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Maximum value. Default: 100.
    /// </summary>
    public int Max { get; set; } = 100;

    public Progress() { }
    public Progress(int value) => Value = value;

    public override IComponent Build(RenderContext context)
    {
        var percentage = Max > 0 ? (int)((double)Value / Max * 100) : 0;
        if (percentage > 100) percentage = 100;
        if (percentage < 0) percentage = 0;

        return new Box
        {
            As = "div",
            ClassName = $"eq-progress {ClassName}".Trim(),
            Role = "progressbar",
            AriaValueMin = 0,
            AriaValueMax = Max,
            AriaValueNow = Value,
            Children = {
                new Box
                {
                    As = "div",
                    ClassName = "eq-progress-indicator",
                    Style = new HtmlStyle { Width = $"{percentage}%" }
                }
            }
        };
    }
}
