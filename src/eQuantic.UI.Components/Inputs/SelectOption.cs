namespace eQuantic.UI.Components.Inputs;

public class SelectOption
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Disabled { get; set; }
    public bool Selected { get; set; }

    public SelectOption() { }
    
    public SelectOption(string label, string value)
    {
        Label = label;
        Value = value;
    }
}
