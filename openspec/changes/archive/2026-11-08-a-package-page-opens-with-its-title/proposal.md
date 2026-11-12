## Why

Closes #882, a bug under #880's work on the README. nuget.org shows a readme's HTML as text, and `README.md` opens with
HTML for the head GitHub centres, so the first screen of every package's page was tags.

## What Changes

- A package's readme opens with `assets/package-head.md`, the same head in Markdown: title, tagline, badges that are
  pictures, the link to the playground and the row of links. GitHub keeps its centred head.
- A test holds the two heads to the same links, pictures and words.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `release-publishing`: gains "A package's page draws its head".

## Impact

- **The packages**: every package's `README.md`, written at pack by `WritePackageReadme`.
- **eqc, the runtime, the realizers, the shells, the templates**: none. The public and developer surfaces do not move.
