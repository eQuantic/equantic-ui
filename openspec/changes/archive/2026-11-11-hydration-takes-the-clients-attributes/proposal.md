# Proposal

Closes #1302, a Bug.

## Why

The server renders a page with every capability resolved to its absent realization, which reports
itself unavailable. A page that decides an ATTRIBUTE from that answer (a button written
`Disabled = saver is not { IsAvailable: true }`) is served with the server's answer. In the browser
the page builds again with the real capability, and its tree says otherwise, but hydration accepted
the server's element as it was and kept the client's tree as the one the DOM reflects. The next
render diffed the client's tree against itself, found nothing changed, and the attribute the server
wrote stayed for good. The wiki's Capabilities page promised the opposite.

A STRUCTURAL difference never showed it: hydration reports a mismatch and falls back to a full
render. The defect was the case hydration accepts, same structure, other attributes or text.

## What Changes

- Hydration makes every element it adopts carry the client's attributes: one the client names is
  written with the client's value, and one only the server wrote is removed. The element itself is
  kept, so its focus, its scroll and the listeners attached to it survive.
- The framework's `data-eq-*` markers are the one exception: one the server wrote that the client's
  tree does not name stays, as it did.
- A field the person already used before the runtime loaded keeps what they typed or ticked:
  writing the client's `value` or `checked` would set the property and throw it away.
- A text node the server wrote with other text takes the client's.
- The dashboard sample's recovery codes page gates its Download button on `IFileSaver.IsAvailable`,
  so the page shows the case live.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hydration-contract`: hydration adopts the server's elements with the client's attributes and text

## Impact

The web runtime's reconciler (`hydrate`). No API, no setting.
