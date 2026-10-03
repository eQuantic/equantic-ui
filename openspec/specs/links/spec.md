# links Specification

## Purpose
Where a link leads and when it is warmed: a `Link` and a run of text that links, lowered the same
way on the server and in the browser.

## Requirements

### Requirement: A link carries the active language

A `Link` and a `TextRun` with a `Destination` SHALL lower to an anchor whose `href` carries the active
language when the app serves languages as a path prefix and the destination is an app-internal path,
the same on the server and in the browser. A destination that is somebody else's (an absolute URL, a
protocol-relative one, a fragment, a `mailto:`) SHALL be left as written.

#### Scenario: A run under Portuguese

- **WHEN** the app declares the prefixes `en` and `pt-BR`, the reader is in `pt-BR`, and a Text holds
  a run linking to `/terms` and one linking to `https://example.com/docs`
- **THEN** the first lowers to `href="/pt-BR/terms"` and the second to `href="https://example.com/docs"`,
  on both producers

### Requirement: An app-internal link warms on hover

A `Link` and a run that links SHALL carry `data-prefetch` exactly when their destination is an
app-internal path: rooted, and not protocol-relative.

#### Scenario: Three destinations

- **WHEN** a Link leads to `/`, another to `//cdn.example.com/x`, and a run to `/terms`
- **THEN** the first and the run carry `data-prefetch` and the protocol-relative one does not
