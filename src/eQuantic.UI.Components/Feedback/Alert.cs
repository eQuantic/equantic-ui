using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Feedback;

public enum AlertType
{
    Info,
    Success,
    Warning,
    Error
}

public class Alert : StatelessComponent
{
    public new string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertType Type { get; set; } = AlertType.Info;

    public override IComponent Build(RenderContext context)
    {
        var (bgColor, textColor, borderColor) = this.Type switch
        {
            AlertType.Success => ("bg-green-50 dark:bg-green-900/20", "text-green-800 dark:text-green-200", "border-green-200 dark:border-green-800"),
            AlertType.Warning => ("bg-yellow-50 dark:bg-yellow-900/20", "text-yellow-800 dark:text-yellow-200", "border-yellow-200 dark:border-yellow-800"),
            AlertType.Error => ("bg-red-50 dark:bg-red-900/20", "text-red-800 dark:text-red-200", "border-red-200 dark:border-red-800"),
            _ => ("bg-blue-50 dark:bg-blue-900/20", "text-blue-800 dark:text-blue-200", "border-blue-200 dark:border-blue-800")
        };

        var content = new Column { Gap = "4px" };
        
        if (!string.IsNullOrEmpty(this.Title))
        {
            content.Children.Add(new Text(this.Title) { ClassName = "font-bold" });
        }
        
        if (!string.IsNullOrEmpty(this.Message))
        {
            content.Children.Add(new Text(this.Message) { ClassName = "text-sm opacity-90" });
        }

        return new Container
        {
            ClassName = "p-4 rounded-md border " + bgColor + " " + textColor + " " + borderColor + " " + (this.ClassName != null ? this.ClassName : ""),
            Children = { content }
        };
    }
}
