using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Utility for constructing CSS class strings conditionally (CVA pattern).
/// </summary>
public class StyleBuilder
{
    private readonly StringBuilder _builder = new();

    private StyleBuilder(string? baseClass)
    {
        if (!string.IsNullOrWhiteSpace(baseClass))
        {
            _builder.Append(baseClass);
        }
    }

    public static StyleBuilder Create(string? baseClass = null)
    {
        return new StyleBuilder(baseClass);
    }

    /// <summary>
    /// Adds a class if the condition is true (or always if no condition).
    /// </summary>
    public StyleBuilder Add(string? className, bool condition = true)
    {
        if (condition && !string.IsNullOrWhiteSpace(className))
        {
            if (_builder.Length > 0) _builder.Append(' ');
            _builder.Append(className);
        }
        return this;
    }

    /// <summary>
    /// Adds a class from a lookup based on a variant/key.
    /// </summary>
    public StyleBuilder AddVariant<TKey>(TKey key, Func<TKey, string?> lookup) where TKey : notnull
    {
        var className = lookup(key);
        return Add(className);
    }

    public string Build()
    {
        return _builder.ToString();
    }
    
    public override string ToString() => Build();
}
