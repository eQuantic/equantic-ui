# Proposal

Part of #1259, a Task under #306 (the security hardening review).

## Why

Dependabot alert #90 (GHSA-6pq8-jhm2-2vcw, medium) reaches `qs` 6.15.3 in
`extensions/vscode/package-lock.json`, through `@vscode/vsce` → `typed-rest-client`, which asks
for `qs` `^6.9.1`. The patched 6.16.1 is inside that range.

The other alert #1259 names, #94 (GHSA-g3hv-75wg-m249, medium, `minimatch` below 11.0.1), has no
patched version that a released `@vscode/vsce` accepts: 3.9.2 asks for `minimatch` `^10.2.2`, the
newest major 4.1.0 asks for `^10.2.6`, and no 10.x release carries the fix. It waits on the issue
for vsce to accept 11, rather than an `overrides` entry over vsce's declared range.

None of these packages reaches a developer. The extension declares no `dependencies`, and its
`.vscodeignore` leaves `node_modules/**` out of the `.vsix`.

## What Changes

- The lockfile records `qs` 6.17.1, the newest release in `typed-rest-client`'s range, with the
  same dependencies as 6.15.3. No other package moves.
- Nothing a developer using the SDK or the extension writes or meets changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the extension's build tooling alone, and no requirement of the product
moves, so the change sets `skip_specs`.)

## Impact

`extensions/vscode/package-lock.json`, three lines.
