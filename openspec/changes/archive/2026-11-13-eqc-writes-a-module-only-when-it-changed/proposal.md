# Proposal

Closes #1279, a Task under #900.

## Why

The fifth of the seven slices that bring the web's edit-to-view under a second (#900). A compile
rewrote every module, map and catalogue it produced, the unchanged ones included, so a save that
changed one screen moved the time of every file eqc writes. Anything that goes by a file's time took
that for a change, the incremental bundle of the next slice (#1280) first among them. And the SDK's
prune of eqc's folder read a file older than the compile's start as one the compile did not write,
which holds only while every file is rewritten, and never ran for the eqc a running app keeps (#1276).

## What Changes

- eqc writes a module, its map and a culture's catalogue only when the text differs from the file's.
- eqc removes what it stopped producing itself, after it wrote the rest, by what the compile
  produced: the catalogues by the names it wrote, the bundle's modules, chunks and maps by what bun
  wrote, the rule the maps already followed. The runtime the SDK copies there is never the bundle's.
- The SDK's two prune targets, which went by a file's time, are removed.
- Nothing a developer writes changes, and neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `generated-files`: eqc's output folder is pruned by eqc, by what the compile produced, and a file is
  written only when its text changed.
