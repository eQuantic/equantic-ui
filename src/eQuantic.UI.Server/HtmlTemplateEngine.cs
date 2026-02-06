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
    /// Now properly supports nested conditionals.
    /// </summary>
    /// <param name="variables">Dictionary of variable names and their values.</param>
    /// <param name="conditions">Dictionary of condition names and their boolean values.</param>
    /// <returns>The rendered HTML string.</returns>
    public string Render(Dictionary<string, string> variables, Dictionary<string, bool>? conditions = null)
    {
        conditions ??= new Dictionary<string, bool>();
        var result = _template;

        // 1. Process conditionals recursively (handles nested {{#if}}...{{/if}})
        result = ProcessConditionalsRecursive(result, conditions);

        // 2. Process optional sections: {{?Variable}}...{{/Variable}}
        result = _optionalRegex.Replace(result, match =>
        {
            var variableName = match.Groups[1].Value;
            var content = match.Groups[2].Value;

            var hasValue = variables.TryGetValue(variableName, out var varValue) && !string.IsNullOrWhiteSpace(varValue);
            return hasValue ? content : string.Empty;
        });

        // 3. Process simple variable substitutions: {{Variable}}
        var builder = new StringBuilder(result);
        foreach (var kvp in variables)
        {
            var placeholder = $"{{{{{kvp.Key}}}}}";
            builder.Replace(placeholder, kvp.Value ?? string.Empty);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Recursively processes nested {{#if}} conditionals from innermost to outermost.
    /// </summary>
    private string ProcessConditionalsRecursive(string input, Dictionary<string, bool> conditions)
    {
        // Keep processing until no more conditionals are found
        string result = input;
        bool foundConditional;

        do
        {
            foundConditional = false;
            
            // Find the innermost {{#if}} (one without nested {{#if}} inside)
            var ifMatch = FindInnermostConditional(result);
            if (ifMatch != null)
            {
                foundConditional = true;
                result = ProcessSingleConditional(result, ifMatch, conditions);
            }
        } while (foundConditional);

        return result;
    }

    /// <summary>
    /// Finds the innermost conditional (one that doesn't contain another {{#if}} inside).
    /// </summary>
    private static ConditionalMatch? FindInnermostConditional(string input)
    {
        const string ifPattern = "{{#if ";
        const string elsePattern = "{{#else}}";
        const string endifPattern = "{{/if}}";

        int searchStart = 0;
        while (true)
        {
            // Find the next {{#if
            int ifStart = input.IndexOf(ifPattern, searchStart, StringComparison.Ordinal);
            if (ifStart == -1) return null;

            // Find the condition name (ends with }})
            int conditionStart = ifStart + ifPattern.Length;
            int conditionEnd = input.IndexOf("}}", conditionStart, StringComparison.Ordinal);
            if (conditionEnd == -1) return null;

            string conditionName = input.Substring(conditionStart, conditionEnd - conditionStart).Trim();
            int contentStart = conditionEnd + 2;

            // Find the matching {{/if}} - we need to track nesting
            int depth = 1;
            int pos = contentStart;
            int? elsePos = null;
            int? endifPos = null;

            while (pos < input.Length && depth > 0)
            {
                int nextIf = input.IndexOf(ifPattern, pos, StringComparison.Ordinal);
                int nextElse = input.IndexOf(elsePattern, pos, StringComparison.Ordinal);
                int nextEndif = input.IndexOf(endifPattern, pos, StringComparison.Ordinal);

                // Find the next relevant tag
                int[] positions = new[] { nextIf, nextElse, nextEndif }
                    .Where(p => p >= 0)
                    .DefaultIfEmpty(int.MaxValue)
                    .ToArray();
                int nextPos = positions.Min();

                if (nextPos == int.MaxValue) break;

                if (nextPos == nextIf)
                {
                    depth++;
                    pos = nextPos + ifPattern.Length;
                }
                else if (nextPos == nextEndif)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endifPos = nextPos;
                    }
                    pos = nextPos + endifPattern.Length;
                }
                else if (nextPos == nextElse && depth == 1)
                {
                    // Only track {{#else}} at our current nesting level
                    elsePos = nextPos;
                    pos = nextPos + elsePattern.Length;
                }
                else
                {
                    pos = nextPos + 1;
                }
            }

            if (endifPos.HasValue)
            {
                // Check if this conditional has nested {{#if}} inside
                string contentBefore = elsePos.HasValue 
                    ? input.Substring(contentStart, elsePos.Value - contentStart)
                    : input.Substring(contentStart, endifPos.Value - contentStart);
                
                string contentAfter = elsePos.HasValue 
                    ? input.Substring(elsePos.Value + elsePattern.Length, endifPos.Value - (elsePos.Value + elsePattern.Length))
                    : "";

                bool hasNestedIf = contentBefore.Contains(ifPattern) || contentAfter.Contains(ifPattern);

                if (!hasNestedIf)
                {
                    // This is an innermost conditional
                    return new ConditionalMatch
                    {
                        Start = ifStart,
                        End = endifPos.Value + endifPattern.Length,
                        ConditionName = conditionName,
                        TrueContent = contentBefore,
                        FalseContent = contentAfter,
                        HasElse = elsePos.HasValue
                    };
                }
            }

            // Move past this {{#if}} and look for the next one
            searchStart = ifStart + 1;
        }

        return null;
    }

    /// <summary>
    /// Processes a single conditional match and returns the result.
    /// </summary>
    private static string ProcessSingleConditional(string input, ConditionalMatch match, Dictionary<string, bool> conditions)
    {
        var conditionValue = conditions.TryGetValue(match.ConditionName, out var value) && value;
        var replacement = conditionValue ? match.TrueContent : (match.HasElse ? match.FalseContent : string.Empty);

        return string.Concat(
            input.AsSpan(0, match.Start),
            replacement,
            input.AsSpan(match.End)
        );
    }

    /// <summary>
    /// Represents a parsed conditional match.
    /// </summary>
    private class ConditionalMatch
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string ConditionName { get; set; } = "";
        public string TrueContent { get; set; } = "";
        public string FalseContent { get; set; } = "";
        public bool HasElse { get; set; }
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
