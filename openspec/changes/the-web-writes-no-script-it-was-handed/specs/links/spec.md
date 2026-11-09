## ADDED Requirements

### Requirement: A link writes an href only for a scheme it may carry

A `Link`, a `TextRun` with a `Destination` and a Markdown link SHALL lower to an anchor whose `href`
holds the destination only when it has no scheme or its scheme is `http`, `https`, `mailto` or `tel`,
and to an anchor with no `href` for any other scheme, the same on the server and in the browser. The
scheme SHALL be read as a browser's URL parser reads it, with its letter case and the characters that
parser ignores folded away.

#### Scenario: A Markdown link with another scheme

- **WHEN** a `Markdown` holds a link whose target has the `javascript` scheme
- **THEN** the run of that link lowers to an anchor with no `href`, on both producers

#### Scenario: A scheme folded as a browser folds it

- **WHEN** a `Link`'s destination has the `javascript` scheme in mixed letter case, with characters a
  browser's URL parser ignores around and inside it
- **THEN** its anchor carries no `href`

#### Scenario: The schemes a link may carry

- **WHEN** runs lead to `mailto:team@example.com`, `tel:+351210000000`, `HTTPS://example.com`, `#top`
  and `docs/next?page=2`
- **THEN** each lowers to an anchor whose `href` is its destination as written
