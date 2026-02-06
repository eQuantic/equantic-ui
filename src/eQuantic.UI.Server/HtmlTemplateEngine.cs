using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace eQuantic.UI.Server;

/// <summary>
/// Simple and fast HTML template engine using {{variable}} placeholders.
/// Supports conditionals ({{#if}}...{{/if}}) and optional sections ({{?variable}}...{{/variable}}).
/// Optimized for performance with StringBuilder and minimal allocations.
/// </summary>
public class HtmlTemplateEngine
{
    private readonly string _template;
    private static readonly Dictionary<string, string> _templateCache = new();

    // Regex patterns for template constructs
    private static readonly Regex _conditionalRegex = new(@"\{\{#if\s+(\w+)\}\}(.*?)\{\{/if\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex _conditionalElseRegex = new(@"\{\{#if\s+(\w+)\}\}(.*?)\{\{#else\}\}(.*?)\{\{/if\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex _optionalRegex = new(@"\{\{\?(\w+)\}\}(.*?)\{\{/\1\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public HtmlTemplateEngine(string template)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
    }

    /// <summary>
    /// Loads a template from an embedded resource.
    /// Templates are cached for performance.
    /// </summary>
    /// <param name="resourceName">The fully qualified resource name (e.g., "eQuantic.UI.Server.Templates.app-shell.html")</param>
    /// <param name="assembly">The assembly containing the resource. If null, uses calling assembly.</param>
    /// <returns>A new template engine instance.</returns>
    public static HtmlTemplateEngine FromResource(string resourceName, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        // Check cache
        var cacheKey = $"{assembly.FullName}:{resourceName}";
        if (_templateCache.TryGetValue(cacheKey, out var cachedTemplate))
        {
            return new HtmlTemplateEngine(cachedTemplate);
        }

        // Load from embedded resource
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var availableResources = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'. " +
                $"Available resources: {availableResources}");
        }

        using var reader = new StreamReader(stream);
        var template = reader.ReadToEnd();

        // Cache for future use
        _templateCache[cacheKey] = template;

        return new HtmlTemplateEngine(template);
    }

    /// <summary>
    /// Renders the template with the provided variables and conditionals.
    /// Supports:
    /// - {{Variable}} - Simple variable substitution
    /// - {{#if Condition}}...{{/if}} - Conditional block (true/false)
    /// - {{#if Condition}}...{{#else}}...{{/if}} - Conditional with else
    /// - {{?Variable}}...{{/Variable}} - Optional section (rendered if variable has value)
    /// </summary>
    /// <param name="variables">Dictionary of variable names and their values.</param>
    /// <param name="conditions">Dictionary of condition names and their boolean values.</param>
    /// <returns>The rendered HTML string.</returns>
    public string Render(Dictionary<string, string> variables, Dictionary<string, bool>? conditions = null)
    {
        conditions ??= new Dictionary<string, bool>();
        var result = _template;

        // 1. Process conditionals with else: {{#if Condition}}...{{#else}}...{{/if}}
        result = _conditionalElseRegex.Replace(result, match =>
        {
            var conditionName = match.Groups[1].Value;
            var trueContent = match.Groups[2].Value;
            var falseContent = match.Groups[3].Value;

            var conditionValue = conditions.TryGetValue(conditionName, out var value) && value;
            return conditionValue ? trueContent : falseContent;
        });

        // 2. Process conditionals: {{#if Condition}}...{{/if}}
        result = _conditionalRegex.Replace(result, match =>
        {
            var conditionName = match.Groups[1].Value;
            var content = match.Groups[2].Value;

            var conditionValue = conditions.TryGetValue(conditionName, out var value) && value;
            return conditionValue ? content : string.Empty;
        });

        // 3. Process optional sections: {{?Variable}}...{{/Variable}}
        result = _optionalRegex.Replace(result, match =>
        {
            var variableName = match.Groups[1].Value;
            var content = match.Groups[2].Value;

            var hasValue = variables.TryGetValue(variableName, out var varValue) && !string.IsNullOrWhiteSpace(varValue);
            return hasValue ? content : string.Empty;
        });

        // 4. Process simple variable substitutions: {{Variable}}
        var builder = new StringBuilder(result);
        foreach (var kvp in variables)
        {
            var placeholder = $"{{{{{kvp.Key}}}}}";
            builder.Replace(placeholder, kvp.Value ?? string.Empty);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders the template with the provided variables (fluent API).
    /// </summary>
    public string Render(Action<Dictionary<string, string>> configure)
    {
        var variables = new Dictionary<string, string>();
        configure(variables);
        return Render(variables, null);
    }

    /// <summary>
    /// Renders the template with variables and conditions using builder pattern.
    /// </summary>
    public string Render(Action<TemplateContext> configure)
    {
        var context = new TemplateContext();
        configure(context);
        return Render(context.Variables, context.Conditions);
    }

    /// <summary>
    /// Clears the template cache. Useful for development/testing.
    /// </summary>
    public static void ClearCache()
    {
        _templateCache.Clear();
    }
}

/// <summary>
/// Context for building template variables and conditions.
/// </summary>
public class TemplateContext
{
    public Dictionary<string, string> Variables { get; } = new();
    public Dictionary<string, bool> Conditions { get; } = new();

    /// <summary>
    /// Sets a template variable.
    /// </summary>
    public TemplateContext Set(string key, string value)
    {
        Variables[key] = value;
        return this;
    }

    /// <summary>
    /// Sets a template variable only if the condition is true.
    /// </summary>
    public TemplateContext SetIf(bool condition, string key, string value)
    {
        if (condition)
            Variables[key] = value;
        return this;
    }

    /// <summary>
    /// Sets a template variable to empty string if null.
    /// </summary>
    public TemplateContext SetOrEmpty(string key, string? value)
    {
        Variables[key] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets a boolean condition for {{#if}} blocks.
    /// </summary>
    public TemplateContext When(string conditionName, bool value)
    {
        Conditions[conditionName] = value;
        return this;
    }

    /// <summary>
    /// Shorthand for setting a true condition.
    /// </summary>
    public TemplateContext Enable(string conditionName)
    {
        Conditions[conditionName] = true;
        return this;
    }

    /// <summary>
    /// Shorthand for setting a false condition.
    /// </summary>
    public TemplateContext Disable(string conditionName)
    {
        Conditions[conditionName] = false;
        return this;
    }

    /// <summary>
    /// Indexer for direct variable access.
    /// </summary>
    public string this[string key]
    {
        get => Variables[key];
        set => Variables[key] = value;
    }
}

/// <summary>
/// Extension methods for fluent template variable configuration.
/// </summary>
public static class TemplateVariablesExtensions
{
    /// <summary>
    /// Sets a variable value in the dictionary.
    /// </summary>
    public static Dictionary<string, string> Set(this Dictionary<string, string> variables, string key, string value)
    {
        variables[key] = value;
        return variables;
    }

    /// <summary>
    /// Sets a variable value only if the condition is true.
    /// </summary>
    public static Dictionary<string, string> SetIf(this Dictionary<string, string> variables, bool condition, string key, string value)
    {
        if (condition)
            variables[key] = value;
        return variables;
    }

    /// <summary>
    /// Sets a variable to empty string if null, otherwise uses the value.
    /// </summary>
    public static Dictionary<string, string> SetOrEmpty(this Dictionary<string, string> variables, string key, string? value)
    {
        variables[key] = value ?? string.Empty;
        return variables;
    }
}
