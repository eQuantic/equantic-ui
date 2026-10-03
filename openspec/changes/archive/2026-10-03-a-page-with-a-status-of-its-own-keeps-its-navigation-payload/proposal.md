# Proposal

Part of #416, found in the second review round of the pull request that closes it, where a
navigation's payload started to carry the document's title and head.

## Why

A page can answer with a status of its own, a 404 through `IHandleStatus` for content that does not
exist, and a navigation's payload travels with that status. The client dropped every payload that was
not a 2xx, so on a client navigation to such a page its title, its head and its state were thrown
away, and the previous page's head stayed, while a full load of the same page showed its own.

## What Changes

- The server marks the answer to a navigation with the header the request carried, whatever its
  status.
- The client reads a marked payload whatever its status, and still drops anything else that fails
  (a proxy's error page).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `document-metadata`: a page with a status of its own still hands a navigation its payload.

## Impact

- **Server**: `ServePageState` sets the navigation header on its answer.
- **Client**: `fetchPageState` in `boot.ts` reads the marker.
