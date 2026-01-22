using eQuantic.UI.Core.Events;

namespace eQuantic.UI.Core;

/// <summary>
/// Abstract base class for all HTML elements - provides native HTML characteristics
/// </summary>
public abstract class HtmlElement : IComponent
{
    #region HTML Native Attributes
    
    /// <inheritdoc />
    public string? Id { get; set; }
    
    /// <inheritdoc />
    public string? ClassName { get; set; }
    
    /// <inheritdoc />
    public HtmlStyle? Style { get; set; }
    
    /// <inheritdoc />
    public StyleClass? StyleClass { get; set; }
    
    /// <inheritdoc />
    public IReadOnlyList<StyleClass>? StyleClasses { get; set; }
    
    /// <summary>
    /// Title attribute (tooltip)
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Hidden attribute
    /// </summary>
    public bool? Hidden { get; set; }
    
    /// <summary>
    /// Tab index for keyboard navigation
    /// </summary>
    public int? TabIndex { get; set; }
    
    /// <inheritdoc />
    public Dictionary<string, string>? DataAttributes { get; set; }
    
    /// <inheritdoc />
    public Dictionary<string, string>? AriaAttributes { get; set; }
    
    #endregion
    
    #region Native HTML Events
    
    /// <summary>
    /// Click event handler
    /// </summary>
    public Action? OnClick { get; set; }
    
    /// <summary>
    /// Double click event handler
    /// </summary>
    public Action? OnDoubleClick { get; set; }
    
    /// <summary>
    /// Focus event handler
    /// </summary>
    public Action? OnFocus { get; set; }
    
    /// <summary>
    /// Blur event handler
    /// </summary>
    public Action? OnBlur { get; set; }
    
    /// <summary>
    /// Mouse enter event handler
    /// </summary>
    public Action<MouseEventArgs>? OnMouseEnter { get; set; }
    
    /// <summary>
    /// Mouse leave event handler
    /// </summary>
    public Action<MouseEventArgs>? OnMouseLeave { get; set; }
    
    /// <summary>
    /// Mouse down event handler
    /// </summary>
    public Action<MouseEventArgs>? OnMouseDown { get; set; }
    
    /// <summary>
    /// Mouse up event handler
    /// </summary>
    public Action<MouseEventArgs>? OnMouseUp { get; set; }
    
    /// <summary>
    /// Key down event handler
    /// </summary>
    public Action<KeyboardEventArgs>? OnKeyDown { get; set; }
    
    /// <summary>
    /// Key up event handler
    /// </summary>
    public Action<KeyboardEventArgs>? OnKeyUp { get; set; }
    
    /// <summary>
    /// Key press event handler
    /// </summary>
    public Action<KeyboardEventArgs>? OnKeyPress { get; set; }
    
    #endregion
    
    #region Composite Pattern
    
    protected readonly List<IComponent> _children = new();
    
    /// <inheritdoc />
    public IList<IComponent> Children => _children;
    
    /// <inheritdoc />
    public virtual void AddChild(IComponent child) => _children.Add(child);
    
    /// <inheritdoc />
    public virtual void RemoveChild(IComponent child) => _children.Remove(child);
    
    #endregion
    
    /// <summary>
    /// Render the element to a virtual DOM node
    /// </summary>
    public abstract HtmlNode Render();
    
    /// <summary>
    /// Build common HTML attributes from properties
    /// </summary>
    protected Dictionary<string, string?> BuildAttributes()
    {
        var attrs = new Dictionary<string, string?>();
        
        if (Id != null) attrs["id"] = Id;
        if (Title != null) attrs["title"] = Title;
        if (Hidden == true) attrs["hidden"] = "true";
        if (TabIndex.HasValue) attrs["tabindex"] = TabIndex.Value.ToString();
        
        // Build className from ClassName + StyleClass(es)
        var classNames = new List<string>();
        if (!string.IsNullOrEmpty(ClassName)) classNames.Add(ClassName);
        if (StyleClass != null) classNames.Add(StyleClass.GeneratedClassName);
        if (StyleClasses != null)
        {
            foreach (var sc in StyleClasses)
            {
                classNames.Add(sc.GeneratedClassName);
            }
        }
        if (classNames.Count > 0) attrs["class"] = string.Join(" ", classNames);
        
        // Inline styles
        if (Style != null) attrs["style"] = Style.ToCssString();
        
        // Data attributes
        if (DataAttributes != null)
        {
            foreach (var (key, value) in DataAttributes)
            {
                attrs[$"data-{key}"] = value;
            }
        }
        
        // ARIA attributes
        if (AriaAttributes != null)
        {
            foreach (var (key, value) in AriaAttributes)
            {
                attrs[$"aria-{key}"] = value;
            }
        }
        
        return attrs;
    }
    
    /// <summary>
    /// Build event handlers dictionary
    /// </summary>
    protected Dictionary<string, Delegate> BuildEvents()
    {
        var events = new Dictionary<string, Delegate>();
        
        if (OnClick != null) events["click"] = OnClick;
        if (OnDoubleClick != null) events["dblclick"] = OnDoubleClick;
        if (OnFocus != null) events["focus"] = OnFocus;
        if (OnBlur != null) events["blur"] = OnBlur;
        if (OnMouseEnter != null) events["mouseenter"] = OnMouseEnter;
        if (OnMouseLeave != null) events["mouseleave"] = OnMouseLeave;
        if (OnMouseDown != null) events["mousedown"] = OnMouseDown;
        if (OnMouseUp != null) events["mouseup"] = OnMouseUp;
        if (OnKeyDown != null) events["keydown"] = OnKeyDown;
        if (OnKeyUp != null) events["keyup"] = OnKeyUp;
        if (OnKeyPress != null) events["keypress"] = OnKeyPress;
        
        return events;
    }
}
