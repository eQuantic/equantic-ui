using System;

namespace eQuantic.UI.Core.Events;

/// <summary>
/// Mouse event arguments
/// </summary>
public class MouseEventArgs : EventArgs
{
    /// <summary>
    /// X coordinate relative to the viewport
    /// </summary>
    public double ClientX { get; init; }
    
    /// <summary>
    /// Y coordinate relative to the viewport
    /// </summary>
    public double ClientY { get; init; }
    
    /// <summary>
    /// X coordinate relative to the page
    /// </summary>
    public double PageX { get; init; }
    
    /// <summary>
    /// Y coordinate relative to the page
    /// </summary>
    public double PageY { get; init; }
    
    /// <summary>
    /// Which mouse button was pressed (0=left, 1=middle, 2=right)
    /// </summary>
    public int Button { get; init; }
    
    /// <summary>
    /// Whether Alt key was pressed
    /// </summary>
    public bool AltKey { get; init; }
    
    /// <summary>
    /// Whether Ctrl key was pressed
    /// </summary>
    public bool CtrlKey { get; init; }
    
    /// <summary>
    /// Whether Shift key was pressed
    /// </summary>
    public bool ShiftKey { get; init; }
    
    /// <summary>
    /// Whether Meta key was pressed
    /// </summary>
    public bool MetaKey { get; init; }
}

/// <summary>
/// Keyboard event arguments
/// </summary>
public class KeyboardEventArgs : EventArgs
{
    /// <summary>
    /// The key value
    /// </summary>
    public string Key { get; init; } = string.Empty;
    
    /// <summary>
    /// The key code
    /// </summary>
    public string Code { get; init; } = string.Empty;
    
    /// <summary>
    /// Whether Alt key was pressed
    /// </summary>
    public bool AltKey { get; init; }
    
    /// <summary>
    /// Whether Ctrl key was pressed
    /// </summary>
    public bool CtrlKey { get; init; }
    
    /// <summary>
    /// Whether Shift key was pressed
    /// </summary>
    public bool ShiftKey { get; init; }
    
    /// <summary>
    /// Whether Meta key was pressed
    /// </summary>
    public bool MetaKey { get; init; }
    
    /// <summary>
    /// Whether this is a repeat event
    /// </summary>
    public bool Repeat { get; init; }
}

/// <summary>
/// Input change event arguments
/// </summary>
public class ChangeEventArgs : EventArgs
{
    /// <summary>
    /// The new value
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
