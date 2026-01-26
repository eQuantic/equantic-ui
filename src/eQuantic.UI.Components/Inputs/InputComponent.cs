using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Base class for all input components
/// </summary>
/// <typeparam name="TValue">The type of the input value</typeparam>
public abstract class InputComponent<TValue> : StatelessComponent
{
    /// <summary>
    /// Current value
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// Change event handler
    /// </summary>
    public Action<TValue>? OnChange { get; set; }

    /// <summary>
    /// Input event handler
    /// </summary>
    public Action<TValue>? OnInput { get; set; }
}
