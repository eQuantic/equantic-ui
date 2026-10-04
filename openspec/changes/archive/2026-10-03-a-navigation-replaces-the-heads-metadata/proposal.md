# Proposal

Part of #416, under #537's review: the first round found that a navigation to a page the server does
not render answered nothing of its route, and that the client never removed a metadata tag the next
page does not have.

## Why

- `ServePageState` returned `{}` when SSR was off, when the page turned it off, or when its
  preparation failed, so the previous page's description and canonical stayed in the head.
- The client patched the head by name: a tag the next page writes replaced the previous one, and a
  tag it does not write stayed. A navigation's payload left out the translation group for the same
  reason, since alternates share a `rel` and would have collapsed into one.

## What Changes

- A navigation answers the app's and the route's metadata before any gate, and the page's own on top
  when it was prepared.
- Every tag the document's metadata writes carries `data-eq-meta`, on a load and in a payload alike,
  and the client removes the marked set the previous page left and writes the next one's, the
  translation group included.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `document-metadata`: a navigation replaces the head's metadata as a set.

## Impact

- **Server**: `UIExtensions` (the navigation endpoint), the metadata tags.
- **Boot**: `Resources/boot.ts` (the head patcher).
