## Context

`UseCultureRoutes` puts `RouteDataRequestCultureProvider` first, which is right for a page and answers
nothing for the actions endpoint, which has no culture segment. The template and every sample wire
negotiation with the lambda overload, `app.UseRequestLocalization(options => ...)`.

## How Flutter answers it

The app's `Locale` is state the app holds, and a call that needs it is handed it: the transport never
infers a language. Here the runtime holds the page's culture and the call carries it.

## Decisions

- **The query keys ASP.NET already reads, not a header of ours.** `QueryStringRequestCultureProvider` is
  the first of the default providers, ahead of the cookie and `Accept-Language`. A provider of the SDK's
  reading a header would need registering, and the lambda overload builds its options inline and sees
  nothing registered through DI: the template and the samples would have been left out, or the middleware
  would have had to apply a culture itself with no list of supported cultures to check it against. With
  the query keys the app's own middleware applies the culture and checks it, for every overload, and the
  SDK adds no transport. `UseCultureRoutes` keeps its route provider first, which answers nothing here.
- **The active culture, not `<html lang>` or the URL.** The runtime's culture store is the page's truth
  after a switch with no reload, and it holds the format culture apart from the language, as .NET does.
- **A name in a URL is text anyone can write.** That is why it goes through the middleware: an unsupported
  culture is refused there and the next provider decides, as for a query string on a page.
- **The page lists the catalogs rather than the server answering for a missing one.** Answering `en.json`
  with the neutral catalog would keep the request and hide what the build wrote. The list costs one
  folder listing a render and ends the walk's requests that could not succeed. It speaks for the build's
  files, so a host with its own loader is not held to it.

## Risks / Trade-offs

- An app that removed `QueryStringRequestCultureProvider` from its providers keeps today's order, cookie
  and browser. That is the app's stated choice.
