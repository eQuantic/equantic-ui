namespace eQuantic.UI.Server;

/// <summary>
/// What a route says about its page where it is declared, <c>[Page(Route, Title = …, Description = …)]</c>
/// or <c>MapPage&lt;T&gt;(route, title)</c>: the document's title and description unless the page says
/// otherwise in <c>IHandleMetadata</c>, and over the app's defaults. One per ROUTE rather than per page
/// type, since a page declared at two routes may give each its own title, which the client's route
/// table already carried while the server read the type's first attribute.
/// </summary>
internal sealed record DeclaredPage(string? Title, string? Description);
