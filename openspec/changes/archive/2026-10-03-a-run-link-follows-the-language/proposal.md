# Proposal

Closes #505.

## Why

A `TextRun` with a `Destination` lowered to an `<a>` whose `href` was the destination as written, on
both producers, while a `Link` goes through the language resolver (`RenderContext.ResolveDestination`
in C#, `localizeDestination` in TypeScript). With language prefixes on, a run on `/pt-BR/login` that
links to `/terms` led to the English page, and the client router took the click, so the navigation
was a client one, to the wrong language. `Markdown` lowers its links as runs, so every internal link of
a translated Markdown page had the same defect. A run link carried no `data-prefetch` either, so an
app-internal one was not warmed on hover.

Measured on the way: a `Link` to a protocol-relative URL (`//cdn.example.com/x`) was marked for
prefetch, since the test was a leading slash, while the resolver already treats such a URL as somebody
else's.

## What Changes

- A run that links lowers as a link does, on both producers: the active language on an app-internal
  href, and `data-prefetch` on it.
- One rule decides what warms on hover, for a `Link` and a run alike: a rooted path that is not
  protocol-relative (`WarmsOnHover` in C#, `warmsOnHover` in TypeScript).
- The component parity fixture lowers a `Link` and a run under a language on both sides and compares
  them.

## Capabilities

### New Capabilities

- `links`: where a link leads and when it is warmed, for a `Link` and a run that links.

### Modified Capabilities

(none)

## Impact

- **Web realizer**: `WebLoweringVisitor` (the run branch and `LowerLink`).
- **Runtime**: `shared/lowering.ts`.
- **Tests**: the parity case `links-in-portuguese` on both sides.
