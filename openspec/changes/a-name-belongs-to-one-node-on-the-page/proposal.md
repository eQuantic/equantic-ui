# Proposal

Closes #1252, a Task under #160.

## Why

`AdaptiveNode` puts every declared arm in the document and hides the rejected ones with a media
query, so a subtree placed in two arms reaches the page twice. Until #360 the dashboard's
`ConsoleShell` handed the same page to both arms, and every page of the sample was in the document
twice: measured on /charts in September with three bookmarks, six ids, the first copy (the compact
arm, under `display:none`) 0px tall. A browser resolves a fragment to the first match in document
order, so a link to one of them scrolled nowhere.

#360 fixed the shell and `ConsoleShellTests` pins it on the node. Nothing reads the document, which
is where the fault shows: each copy lowers perfectly, so every realizer pin passes over it. And no
sample links to a fragment at all, which is how the router defect fixed in #100 survived a release.

## What Changes

- `DashboardDocumentTests`, in the Server tests, hosts the dashboard from its own `Program` through
  `WebApplicationFactory` (ASP.NET Core 10 declares `Program` public for an app of top-level
  statements, so the sample writes nothing for it) and reads every route the sample declares, taken
  from the same `[Page]` attribute the server's own scan reads. `NoNameIsClaimedTwiceOnAPage` fails
  on a page that draws one id twice, naming the route, the id and the count. It ports the site's
  guard of the same name, which caught 29 duplicates there.
- `NoFragmentPointsAtAPlaceThatIsNotThere` fails on an in-page link whose fragment names nothing on
  the page it points at. A duplicate passes it twice over, which is why both exist.
- /charts gets a "Jump to" row of links to its three cards, each card claiming a `Bookmark`: the
  first fragment any sample links to. The README's charts picture is taken again.
- The Server tests reference the sample, so it builds on every runner the suite runs on, where the
  samples job builds it on macOS alone.
- Nothing a developer using the SDK writes or meets changes. No part of the SDK changes, and neither
  the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the test suites and a sample alone, and no requirement of the product
moves, so the change sets `skip_specs`.)
