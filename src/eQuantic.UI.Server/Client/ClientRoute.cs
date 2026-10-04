using System.Text.Json.Serialization;

namespace eQuantic.UI.Server.Client;

/// <summary>One entry of the client's route table: a pattern, the page it draws, and the title the
/// router gives the document on a navigation, left out when the route declares none.</summary>
internal sealed record ClientRoute(
    string Pattern,
    string Page,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title);
