# Proposal

Closes #416 and #526.

## Why

- `[Page(Title = …)]` never reached the document. The shell started the page's metadata with the
  app's title and applied the route's only when the metadata had none, which never happened; and a
  client navigation answered with the app's title over the one the router had just set from its
  route table. Measured on the dashboard sample: `/charts`, declared `Title = "Charts — eQuantic
  Console"`, served `<title>eQuantic Console</title>`, and a navigation to `/clock` ended on
  `eQuantic Console` too.
- The client's configuration (`window.__EQ_CONFIG`) quoted its strings by hand, escaping the
  backslash and the quote and nothing else. A route title holding a line break was a syntax error
  in a script every page carries, which stopped the client of the whole app, a `</script>` closed
  the element, and the theme cookie's name went in raw.

## What Changes

- Each route hands its own declaration (`[Page(Route, Title, Description)]`, or
  `MapPage<T>(route, title)`) to the shell and to the navigation endpoint, and one builder writes a
  document's metadata on both: the app's defaults, then the route's title and description, then
  the page's `IHandleMetadata`, each over the one before by key.
- The client's configuration is serialized by System.Text.Json, whose default encoder escapes every
  code unit a script element cannot carry raw.

## Capabilities

### New Capabilities

- `document-metadata`: what a served document says about its page, and the configuration it hands
  the client.

### Modified Capabilities

(none)

## Impact

- **Server**: `UIExtensions` (the shell, the navigation endpoint, the route mapping) and the
  client configuration's records.
- **Break**: a page whose route declares a title is titled by it, where the app's title showed; a
  navigation's head carries the app's default tags and the route's description, so a description
  the previous page left is replaced.
