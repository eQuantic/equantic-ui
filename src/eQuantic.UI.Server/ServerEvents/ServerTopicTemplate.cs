using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;

namespace eQuantic.UI.Server;

/// <summary>
/// One configured topic template, parsed by ASP.NET Core's own route machinery: <c>room:{roomId}</c>
/// is a route template whose one segment is a literal and a parameter, matched as a request's path
/// would be. A parameter takes no constraint, which the route matcher would not evaluate here: the
/// template's rule validates the values instead, and a constraint written anyway fails at startup
/// rather than reading as a check that never runs.
/// </summary>
internal sealed class ServerTopicTemplate
{
    private readonly TemplateMatcher _matcher;

    public ServerTopicTemplate(ServerTopicRule rule)
    {
        Rule = rule;
        RoutePattern pattern;
        try
        {
            pattern = RoutePatternFactory.Parse("/" + rule.Template);
        }
        catch (RoutePatternException exception)
        {
            throw new ArgumentException(
                $"'{rule.Template}' is not a topic template: write it in ASP.NET Core's route syntax, as in 'room:{{roomId}}'.",
                exception);
        }

        var constrained = pattern.Parameters.FirstOrDefault(parameter => parameter.ParameterPolicies.Count > 0);
        if (constrained is not null)
            throw new ArgumentException(
                $"The topic template '{rule.Template}' constrains '{constrained.Name}', and a topic template's parameter takes no constraint: validate the value in the template's authorization, which receives it.");

        _matcher = new TemplateMatcher(new RouteTemplate(pattern), new RouteValueDictionary());
        Specificity = pattern.PathSegments
            .SelectMany(segment => segment.Parts)
            .OfType<RoutePatternLiteralPart>()
            .Sum(part => part.Content.Length);
    }

    public ServerTopicRule Rule { get; }

    /// <summary>How much of a topic the template's literals fix: of two templates that both match,
    /// the one that fixes more is the one the app meant (<c>room:{id}:participant:{p}</c> over
    /// <c>room:{id}</c>, whose parameter would take the rest).</summary>
    public int Specificity { get; }

    public bool TryMatch(string topic, out IReadOnlyDictionary<string, string?> values)
    {
        var matched = new RouteValueDictionary();
        if (!_matcher.TryMatch(new Microsoft.AspNetCore.Http.PathString("/" + topic), matched))
        {
            values = new Dictionary<string, string?>();
            return false;
        }

        values = matched.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
        return true;
    }
}
