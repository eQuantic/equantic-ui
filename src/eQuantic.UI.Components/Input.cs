using eQuantic.UI.Core;
using eQuantic.UI.Core.Events;

namespace eQuantic.UI.Components;

/// <summary>
/// Text input component
/// </summary>
public class TextInput : HtmlElement
{
    /// <summary>
    /// Input type (text, password, email, etc.)
    /// </summary>
    public string Type { get; set; } = "text";
    
    /// <summary>
    /// Current value
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Placeholder text
    /// </summary>
    public string? Placeholder { get; set; }
    
    /// <summary>
    /// Whether the input is disabled
    /// </summary>
    public bool Disabled { get; set; }
    
    /// <summary>
    /// Whether the input is read-only
    /// </summary>
    public bool ReadOnly { get; set; }
    
    /// <summary>
    /// Whether the input is required
    /// </summary>
    public bool Required { get; set; }
    
    /// <summary>
    /// Name attribute for forms
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Maximum length
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Autocomplete attribute
    /// </summary>
    public string? AutoComplete { get; set; }
    
    /// <summary>
    /// Change event handler
    /// </summary>
    public Action<string>? OnChange { get; set; }
    
    /// <summary>
    /// Input event handler (fires on every keystroke)
    /// </summary>
    public Action<string>? OnInput { get; set; }
    
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["type"] = Type;
        attrs["value"] = Value;
        
        if (Placeholder != null) attrs["placeholder"] = Placeholder;
        if (Disabled) attrs["disabled"] = "true";
        if (ReadOnly) attrs["readonly"] = "true";
        if (Required) attrs["required"] = "true";
        if (Name != null) attrs["name"] = Name;
        if (MaxLength.HasValue) attrs["maxlength"] = MaxLength.Value.ToString();
        if (AutoComplete != null) attrs["autocomplete"] = AutoComplete;
        
        var events = BuildEvents();
        if (OnChange != null) events["change"] = OnChange;
        if (OnInput != null) events["input"] = OnInput;
        
        return new HtmlNode
        {
            Tag = "input",
            Attributes = attrs,
            Events = events
        };
    }
}

/// <summary>
/// Text area component for multiline input
/// </summary>
public class TextArea : HtmlElement
{
    /// <summary>
    /// Current value
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Placeholder text
    /// </summary>
    public string? Placeholder { get; set; }
    
    /// <summary>
    /// Number of visible rows
    /// </summary>
    public int? Rows { get; set; }
    
    /// <summary>
    /// Whether the textarea is disabled
    /// </summary>
    public bool Disabled { get; set; }
    
    /// <summary>
    /// Whether the textarea is read-only
    /// </summary>
    public bool ReadOnly { get; set; }
    
    /// <summary>
    /// Name attribute for forms
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Change event handler
    /// </summary>
    public Action<string>? OnChange { get; set; }
    
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        
        if (Placeholder != null) attrs["placeholder"] = Placeholder;
        if (Rows.HasValue) attrs["rows"] = Rows.Value.ToString();
        if (Disabled) attrs["disabled"] = "true";
        if (ReadOnly) attrs["readonly"] = "true";
        if (Name != null) attrs["name"] = Name;
        
        var events = BuildEvents();
        if (OnChange != null) events["change"] = OnChange;
        
        return new HtmlNode
        {
            Tag = "textarea",
            Attributes = attrs,
            Events = events,
            Children = new List<HtmlNode> { HtmlNode.Text(Value) }
        };
    }
}
