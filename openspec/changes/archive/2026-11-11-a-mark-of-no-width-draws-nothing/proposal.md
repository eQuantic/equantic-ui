# Proposal

Closes #1367, a Bug.

## Why

A problem's wave is as wide as its range, counted in columns, and the server, which has no font,
measures every column as 0 wide. The wave was then a drawing of no width, which `Drawing` refuses,
so the server's render of a code block holding a problem failed and was contained: the dashboard
sample's `/code` arrived without the block's server markup on every request. A range of characters
that take no cell (a combining mark) met the same refusal in the browser.

## What Changes

- A mark of no width draws nothing: the wave, the wash, the outline, the strike and the underline.
  The hint's three dots keep their own size, as before.
- The server renders the block with its text, marked unmeasured, and the browser draws it again
  with its waves, as it already did for every other part of its geometry.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `code-diagnostics`: a problem with no width to be drawn draws nothing, and the block still renders

## Impact

`CodeBlock.Marks`, write-once, so both realizers. No API, no setting.
