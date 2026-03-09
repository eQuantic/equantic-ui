using System;
using System.Collections.Generic;
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

    #endregion

    #region Global HTML Attributes

    public string? AccessKey { get; set; }
    public string? Autocapitalize { get; set; }
    public bool? Autofocus { get; set; }
    public string? ContentEditable { get; set; }
    public string? Dir { get; set; }
    public bool? Draggable { get; set; }
    public string? EnterKeyHint { get; set; }
    public string? InputMode { get; set; }
    public string? Lang { get; set; }
    public string? Popover { get; set; }
    public bool? Spellcheck { get; set; }
    public bool? Translate { get; set; }

    #endregion

    #region ARIA Attributes

    public string? Role { get; set; }
    public string? AriaActiveDescendant { get; set; }
    public bool? AriaAtomic { get; set; }
    public bool? AriaBusy { get; set; }
    public string? AriaChecked { get; set; }
    public int? AriaColCount { get; set; }
    public int? AriaColIndex { get; set; }
    public int? AriaColSpan { get; set; }
    public string? AriaControls { get; set; }
    public string? AriaCurrent { get; set; }
    public string? AriaDescribedBy { get; set; }
    public string? AriaDetails { get; set; }
    public bool? AriaDisabled { get; set; }
    public string? AriaErrorMessage { get; set; }
    public bool? AriaExpanded { get; set; }
    public string? AriaFlowTo { get; set; }
    public string? AriaHasPopup { get; set; }
    public bool? AriaHidden { get; set; }
    public string? AriaInvalid { get; set; }
    public string? AriaKeyShortcuts { get; set; }
    public string? AriaLabel { get; set; }
    public string? AriaLabelledBy { get; set; }
    public int? AriaLevel { get; set; }
    public string? AriaLive { get; set; }
    public bool? AriaModal { get; set; }
    public bool? AriaMultiline { get; set; }
    public bool? AriaMultiSelectable { get; set; }
    public string? AriaOrientation { get; set; }
    public string? AriaOwns { get; set; }
    public string? AriaPlaceholder { get; set; }
    public int? AriaPosInSet { get; set; }
    public string? AriaPressed { get; set; }
    public bool? AriaReadOnly { get; set; }
    public string? AriaRelevant { get; set; }
    public bool? AriaRequired { get; set; }
    public string? AriaRoleDescription { get; set; }
    public int? AriaRowCount { get; set; }
    public int? AriaRowIndex { get; set; }
    public int? AriaRowSpan { get; set; }
    public bool? AriaSelected { get; set; }
    public int? AriaSetSize { get; set; }
    public string? AriaSort { get; set; }
    public double? AriaValueMax { get; set; }
    public double? AriaValueMin { get; set; }
    public double? AriaValueNow { get; set; }
    public string? AriaValueText { get; set; }

    #endregion

    #region Standard HTML Attributes

    public string? Accept { get; set; }
    public string? Action { get; set; }
    public string? Alt { get; set; }
    public bool? Async { get; set; }
    public string? Autocomplete { get; set; }
    public bool? Autoplay { get; set; }
    public bool? Checked { get; set; }
    public string? Cite { get; set; }
    public int? Cols { get; set; }
    public int? ColSpan { get; set; }
    public bool? Controls { get; set; }
    public string? Coords { get; set; }
    public string? Data { get; set; }
    public string? Datetime { get; set; }
    public bool? Default { get; set; }
    public bool? Defer { get; set; }
    public bool? Disabled { get; set; }
    public string? Download { get; set; }
    public string? Enctype { get; set; }
    public string? For { get; set; }
    public string? Form { get; set; }
    public string? FormAction { get; set; }
    public string? Headers { get; set; }
    public string? Height { get; set; }
    public double? High { get; set; }
    public string? Href { get; set; }
    public string? Hreflang { get; set; }
    public string? Integrity { get; set; }
    public bool? IsMap { get; set; }
    public string? Kind { get; set; }
    public string? Label { get; set; }
    public string? List { get; set; }
    public bool? Loop { get; set; }
    public double? Low { get; set; }
    public double? Max { get; set; }
    public int? MaxLength { get; set; }
    public string? Media { get; set; }
    public string? Method { get; set; }
    public double? Min { get; set; }
    public int? MinLength { get; set; }
    public bool? Multiple { get; set; }
    public bool? Muted { get; set; }
    public string? Name { get; set; }
    public bool? NoValidate { get; set; }
    public bool? Open { get; set; }
    public double? Optimum { get; set; }
    public string? Pattern { get; set; }
    public string? Placeholder { get; set; }
    public string? Poster { get; set; }
    public string? Preload { get; set; }
    public bool? ReadOnly { get; set; }
    public string? Rel { get; set; }
    public bool? Required { get; set; }
    public bool? Reversed { get; set; }
    public int? Rows { get; set; }
    public int? RowSpan { get; set; }
    public string? Sandbox { get; set; }
    public string? Scope { get; set; }
    public bool? Selected { get; set; }
    public string? Shape { get; set; }
    public int? Size { get; set; }
    public string? Sizes { get; set; }
    public int? Span { get; set; }
    public string? Src { get; set; }
    public string? Srcdoc { get; set; }
    public string? Srclang { get; set; }
    public string? Srcset { get; set; }
    public int? Start { get; set; }
    public double? Step { get; set; }
    public string? Target { get; set; }
    public string? Type { get; set; }
    public string? Usemap { get; set; }
    public string? Value { get; set; }
    public string? Width { get; set; }
    public string? Wrap { get; set; }
    
    // Performance & Styling
    public string? Loading { get; set; }
    public string? Decoding { get; set; }
    public string? FetchPriority { get; set; }
    
    // Inline JavaScript Events
    public string? OnLoad { get; set; }

    public string? InnerHtml { get; set; }
    public Dictionary<string, string> CustomAttributes { get; set; } = new();

    /// <summary>
    /// Custom JavaScript-style event handlers.
    /// </summary>
    public Dictionary<string, Delegate> CustomEvents { get; set; } = new();

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
    /// Submit event handler
    /// </summary>
    public Action? OnSubmit { get; set; }

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
        
        // Inline styles
        if (Style != null) attrs["style"] = Style.ToCssString();

        // Data attributes
        if (DataAttributes != null)
        {
            foreach (var attr in DataAttributes)
            {
                attrs[$"data-{attr.Key}"] = attr.Value;
            }
        }

        foreach (var attr in CustomAttributes)
        {
            attrs[attr.Key] = attr.Value;
        }

        // Global HTML
        if (AccessKey != null) attrs["accesskey"] = AccessKey;
        if (Autocapitalize != null) attrs["autocapitalize"] = Autocapitalize;
        if (Autofocus == true) attrs["autofocus"] = "true";
        if (ContentEditable != null) attrs["contenteditable"] = ContentEditable;
        if (Dir != null) attrs["dir"] = Dir;
        if (Draggable.HasValue) attrs["draggable"] = Draggable.Value ? "true" : "false";
        if (EnterKeyHint != null) attrs["enterkeyhint"] = EnterKeyHint;
        if (InputMode != null) attrs["inputmode"] = InputMode;
        if (Lang != null) attrs["lang"] = Lang;
        if (Popover != null) attrs["popover"] = Popover;
        if (Spellcheck.HasValue) attrs["spellcheck"] = Spellcheck.Value ? "true" : "false";
        if (Translate.HasValue) attrs["translate"] = Translate.Value ? "yes" : "no";

        // ARIA Role & Properties
        if (Accept != null) attrs["accept"] = Accept;
        if (Action != null) attrs["action"] = Action;
        if (Alt != null) attrs["alt"] = Alt;
        if (Async == true) attrs["async"] = "";
        if (Autocomplete != null) attrs["autocomplete"] = Autocomplete;
        if (Autoplay == true) attrs["autoplay"] = "";
        if (Checked == true) attrs["checked"] = "";
        if (Cite != null) attrs["cite"] = Cite;
        if (Cols.HasValue) attrs["cols"] = Cols.Value.ToString();
        if (ColSpan.HasValue) attrs["colspan"] = ColSpan.Value.ToString();
        if (Controls == true) attrs["controls"] = "";
        if (Coords != null) attrs["coords"] = Coords;
        if (Data != null) attrs["data"] = Data;
        if (Datetime != null) attrs["datetime"] = Datetime;
        if (Default == true) attrs["default"] = "";
        if (Defer == true) attrs["defer"] = "";
        if (Disabled == true) attrs["disabled"] = "";
        if (Download != null) attrs["download"] = Download;
        if (Enctype != null) attrs["enctype"] = Enctype;
        if (For != null) attrs["for"] = For;
        if (Form != null) attrs["form"] = Form;
        if (FormAction != null) attrs["formaction"] = FormAction;
        if (Headers != null) attrs["headers"] = Headers;
        if (Height != null) attrs["height"] = Height;
        if (High.HasValue) attrs["high"] = High.Value.ToString();
        if (Href != null) attrs["href"] = Href;
        if (Hreflang != null) attrs["hreflang"] = Hreflang;
        if (Integrity != null) attrs["integrity"] = Integrity;
        if (IsMap == true) attrs["ismap"] = "";
        if (Kind != null) attrs["kind"] = Kind;
        if (Label != null) attrs["label"] = Label;
        if (List != null) attrs["list"] = List;
        if (Loop == true) attrs["loop"] = "";
        if (Low.HasValue) attrs["low"] = Low.Value.ToString();
        if (Max.HasValue) attrs["max"] = Max.Value.ToString();
        if (MaxLength.HasValue) attrs["maxlength"] = MaxLength.Value.ToString();
        if (Media != null) attrs["media"] = Media;
        if (Method != null) attrs["method"] = Method;
        if (Min.HasValue) attrs["min"] = Min.Value.ToString();
        if (MinLength.HasValue) attrs["minlength"] = MinLength.Value.ToString();
        if (Multiple == true) attrs["multiple"] = "";
        if (Muted == true) attrs["muted"] = "";
        if (Name != null) attrs["name"] = Name;
        if (NoValidate == true) attrs["novalidate"] = "";
        if (Open == true) attrs["open"] = "";
        if (Optimum.HasValue) attrs["optimum"] = Optimum.Value.ToString();
        if (Pattern != null) attrs["pattern"] = Pattern;
        if (Placeholder != null) attrs["placeholder"] = Placeholder;
        if (Poster != null) attrs["poster"] = Poster;
        if (Preload != null) attrs["preload"] = Preload;
        if (ReadOnly == true) attrs["readonly"] = "";
        if (Rel != null) attrs["rel"] = Rel;
        if (Required == true) attrs["required"] = "";
        if (Reversed == true) attrs["reversed"] = "";
        if (Rows.HasValue) attrs["rows"] = Rows.Value.ToString();
        if (RowSpan.HasValue) attrs["rowspan"] = RowSpan.Value.ToString();
        if (Sandbox != null) attrs["sandbox"] = Sandbox;
        if (Scope != null) attrs["scope"] = Scope;
        if (Selected == true) attrs["selected"] = "";
        if (Shape != null) attrs["shape"] = Shape;
        if (Size.HasValue) attrs["size"] = Size.Value.ToString();
        if (Sizes != null) attrs["sizes"] = Sizes;
        if (Span.HasValue) attrs["span"] = Span.Value.ToString();
        if (Src != null) attrs["src"] = Src;
        if (Srcdoc != null) attrs["srcdoc"] = Srcdoc;
        if (Srclang != null) attrs["srclang"] = Srclang;
        if (Srcset != null) attrs["srcset"] = Srcset;
        if (Start.HasValue) attrs["start"] = Start.Value.ToString();
        if (Step.HasValue) attrs["step"] = Step.Value.ToString();
        if (Target != null) attrs["target"] = Target;
        if (Type != null) attrs["type"] = Type;
        if (Usemap != null) attrs["usemap"] = Usemap;
        if (Value != null) attrs["value"] = Value;
        if (Width != null) attrs["width"] = Width;
        if (Wrap != null) attrs["wrap"] = Wrap;
        
        // Performance & Styling
        if (Loading != null) attrs["loading"] = Loading;
        if (Decoding != null) attrs["decoding"] = Decoding;
        if (FetchPriority != null) attrs["fetchpriority"] = FetchPriority;
        if (OnLoad != null) attrs["onload"] = OnLoad;

        // ARIA Role & Properties
        if (Role != null) attrs["role"] = Role;
        if (AriaActiveDescendant != null) attrs["aria-activedescendant"] = AriaActiveDescendant;
        if (AriaAtomic.HasValue) attrs["aria-atomic"] = AriaAtomic.Value ? "true" : "false";
        if (AriaBusy.HasValue) attrs["aria-busy"] = AriaBusy.Value ? "true" : "false";
        if (AriaChecked != null) attrs["aria-checked"] = AriaChecked;
        if (AriaColCount.HasValue) attrs["aria-colcount"] = AriaColCount.Value.ToString();
        if (AriaColIndex.HasValue) attrs["aria-colindex"] = AriaColIndex.Value.ToString();
        if (AriaColSpan.HasValue) attrs["aria-colspan"] = AriaColSpan.Value.ToString();
        if (AriaControls != null) attrs["aria-controls"] = AriaControls;
        if (AriaCurrent != null) attrs["aria-current"] = AriaCurrent;
        if (AriaDescribedBy != null) attrs["aria-describedby"] = AriaDescribedBy;
        if (AriaDetails != null) attrs["aria-details"] = AriaDetails;
        if (AriaDisabled.HasValue) attrs["aria-disabled"] = AriaDisabled.Value ? "true" : "false";
        if (AriaErrorMessage != null) attrs["aria-errormessage"] = AriaErrorMessage;
        if (AriaExpanded.HasValue) attrs["aria-expanded"] = AriaExpanded.Value ? "true" : "false";
        if (AriaFlowTo != null) attrs["aria-flowto"] = AriaFlowTo;
        if (AriaHasPopup != null) attrs["aria-haspopup"] = AriaHasPopup;
        if (AriaHidden.HasValue) attrs["aria-hidden"] = AriaHidden.Value ? "true" : "false";
        if (AriaInvalid != null) attrs["aria-invalid"] = AriaInvalid;
        if (AriaKeyShortcuts != null) attrs["aria-keyshortcuts"] = AriaKeyShortcuts;
        if (AriaLabel != null) attrs["aria-label"] = AriaLabel;
        if (AriaLabelledBy != null) attrs["aria-labelledby"] = AriaLabelledBy;
        if (AriaLevel.HasValue) attrs["aria-level"] = AriaLevel.Value.ToString();
        if (AriaLive != null) attrs["aria-live"] = AriaLive;
        if (AriaModal.HasValue) attrs["aria-modal"] = AriaModal.Value ? "true" : "false";
        if (AriaMultiline.HasValue) attrs["aria-multiline"] = AriaMultiline.Value ? "true" : "false";
        if (AriaMultiSelectable.HasValue) attrs["aria-multiselectable"] = AriaMultiSelectable.Value ? "true" : "false";
        if (AriaOrientation != null) attrs["aria-orientation"] = AriaOrientation;
        if (AriaOwns != null) attrs["aria-owns"] = AriaOwns;
        if (AriaPlaceholder != null) attrs["aria-placeholder"] = AriaPlaceholder;
        if (AriaPosInSet.HasValue) attrs["aria-posinset"] = AriaPosInSet.Value.ToString();
        if (AriaPressed != null) attrs["aria-pressed"] = AriaPressed;
        if (AriaReadOnly.HasValue) attrs["aria-readonly"] = AriaReadOnly.Value ? "true" : "false";
        if (AriaRelevant != null) attrs["aria-relevant"] = AriaRelevant;
        if (AriaRequired.HasValue) attrs["aria-required"] = AriaRequired.Value ? "true" : "false";
        if (AriaRoleDescription != null) attrs["aria-roledescription"] = AriaRoleDescription;
        if (AriaRowCount.HasValue) attrs["aria-rowcount"] = AriaRowCount.Value.ToString();
        if (AriaRowIndex.HasValue) attrs["aria-rowindex"] = AriaRowIndex.Value.ToString();
        if (AriaRowSpan.HasValue) attrs["aria-rowspan"] = AriaRowSpan.Value.ToString();
        if (AriaSelected.HasValue) attrs["aria-selected"] = AriaSelected.Value ? "true" : "false";
        if (AriaSetSize.HasValue) attrs["aria-setsize"] = AriaSetSize.Value.ToString();
        if (AriaSort != null) attrs["aria-sort"] = AriaSort;
        if (AriaValueMax.HasValue) attrs["aria-valuemax"] = AriaValueMax.Value.ToString();
        if (AriaValueMin.HasValue) attrs["aria-valuemin"] = AriaValueMin.Value.ToString();
        if (AriaValueNow.HasValue) attrs["aria-valuenow"] = AriaValueNow.Value.ToString();
        if (AriaValueText != null) attrs["aria-valuetext"] = AriaValueText;

        if (classNames.Count > 0) attrs["class"] = string.Join(" ", classNames);

        return attrs;
    }

    private static readonly Dictionary<string, string> EventNameMap = new()
    {
        ["OnClick"] = "click",
        ["OnDoubleClick"] = "dblclick",
        ["OnFocus"] = "focus",
        ["OnBlur"] = "blur",
        ["OnMouseEnter"] = "mouseenter",
        ["OnMouseLeave"] = "mouseleave",
        ["OnMouseDown"] = "mousedown",
        ["OnMouseUp"] = "mouseup",
        ["OnKeyDown"] = "keydown",
        ["OnKeyUp"] = "keyup",
        ["OnKeyPress"] = "keypress",
        ["OnChange"] = "change",
        ["OnInput"] = "input",
        ["OnSubmit"] = "submit"
    };

    /// <summary>
    /// Build event handlers dictionary using Reflection for discovery
    /// </summary>
    protected Dictionary<string, Delegate> BuildEvents()
    {
        var events = new Dictionary<string, Delegate>();
        var type = GetType();

        foreach (var (propName, domEvent) in EventNameMap)
        {
            var prop = type.GetProperty(propName);
            if (prop != null)
            {
                var value = prop.GetValue(this) as Delegate;
                if (value != null)
                {
                    events[domEvent] = value;
                }
            }
        }

        if (CustomEvents != null)
        {
            foreach (var (key, value) in CustomEvents)
            {
                events[key] = value;
            }
        }

        return events;
    }
}
