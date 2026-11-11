# Proposal

Closes #1403, the first half of #1094 (E4), under #937, the element tree.

## Why

The server drew a write-once page as a tree drawn once, every prefetch round. Each round built the whole page anew, so the component that had loaded was gone by the next round, and the fields it loaded were written onto the fresh instance by name (`ComponentExpansionScope.Restore`). That works for a field and loses anything that is not one, and it makes the server the one host that still builds every component on every pass: Photon and the component tester build through an element tree that keeps an instance per position (E1, E2, #1245). E5b (#1395), which makes the theme, the route and `InFlow` inherited on the web and the server, needs the server to build an element per position too.

The web's frame lowering (#1272) was left with a provisional public form, `WebRealizer.LowerFrame`, and without hydration's unmeasured mark, which says a component built on widths nobody measured. In a frame the owner builds every component before any lowers, so the count around a component's lowering cannot say which one asked. Drawing a page through a frame without that mark would have changed the markup the server writes.

## What Changes

- **A write-once page is drawn through an element tree the request owns.** The server keeps a structural owner for the request, and each prefetch round is a frame of that one tree. A component that loaded has its position marked, and only that subtree builds again, with the instance that loaded. A stateless component below another that loads in the same round waits for the next one, since its parent's build hands its position a fresh instance. These are the rounds the component tester already ran (#1245), now one implementation for both, `ServerDataRounds`.
- **An escape-hatch page is unchanged.** It makes its write-once bridges anew on every render, so they have no position to keep an owner at: each is a tree drawn once, and what loaded is put back by name, as before.
- **The web's host of an owner, `MarkupHost`**, replaces `WebRealizer.LowerFrame`. A frame is its `Build`, `Lower` and `EndFrame`. Its components build through the error boundary with a measurer that has no font, and it notes which build asked that measurer, so the lowering writes the unmeasured mark on the same elements a tree drawn once does. `MarkupHost.Structural` is the server's; the constructor makes a live one, the tester's.
- `ComponentBoundary.BuildAtDepth`, what `Owner()` and `StructuralOwner()` hand the engine, is public, so a host composes its own build around it.
- **The markup and the hydration payload do not change.** Components are named `Type#ordinal` in lowering order, as the browser names them until E3b. The payload carries every component that loads data and every one holding a value from the container, read off the instances that drew.

An app writes nothing new. The public surface moves only where the frame lowering was provisional: `WebRealizer.LowerFrame` goes, `MarkupHost` and `ServerDataRounds` arrive, and `ComponentBoundary.BuildAtDepth` becomes public. No developer surface moves. The migration line, for a test that called `LowerFrame`: make a `MarkupHost` and call `Build`, `Lower` and `EndFrame` on it.

## Impact

The web realizer, the server's rendering service, the component tester and the error boundary. The proof is the server's SSR fixtures and the hydration contract byte for byte, the component parity fixture's first frame compared whole with the mark, and a server test that fails when a round draws a fresh instance.
