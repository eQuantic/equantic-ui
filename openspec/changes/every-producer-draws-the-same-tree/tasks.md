# Tasks

## 1. A Text's own line box (#495)

- [x] 1.1 Both producers lower every Text as a block, the multi-line clamp keeping its box
- [x] 1.2 Pinned by `TextBlockFlowTests` and the parity fixture; measured in Chromium: the pill is 21px

## 2. The scrolled header (#506)

- [x] 2.1 The scrolled diff goes through the state builder over `Pinned.ScrolledBase` on both producers
- [x] 2.2 The state builder writes a border along the base's edges, and a width alone in its colour
- [x] 2.3 The runtime sets `data-eq-scrolled` from the header's surface; the rules select on it
- [x] 2.4 Photon draws the scrolled chrome from the nearest scroll view, gliding under the Transition
- [x] 2.5 Pinned by `PinnedScrolledRealizerTests`, `PinnedScrolledNativeTests`, `scrolled-pinned.spec.ts`,
  the marker pin and the parity fixture; measured in Chromium on the page and in a panel

## 3. A draggable's offset (#511)

- [x] 3.1 Both producers write the offset as `translate`, and its glide joined to the box's list
- [x] 3.2 The controller drags by `translate` and hands the surface back after the release
- [x] 3.3 Pinned by `DraggableRealizerTests`, `draggable.spec.ts` and the parity fixture; measured in
  Chromium under the pointer, served and client-rendered

## 4. The lift through wrappers (#622)

- [x] 4.1 Both producers mark the first descendants that draw a box with `eq-lift`; the slop lifts them
- [x] 4.2 Pinned by `HitSlopTests` and the parity fixture; measured in Chromium with elementFromPoint

## 5. The density handoff (#623)

- [x] 5.1 The runtime leaves the pointer's density in a session cookie
- [x] 5.2 The server builds the page at it and says so in the page's configuration
- [x] 5.3 The boot hydrates at the served density and switches the whole page at once
- [x] 5.4 Pinned by `DensityHandoffTests` and `density-handoff.spec.ts`; measured in Chromium across a reload

## 6. Documentation

- [x] 6.1 The wiki in English and Portuguese, and one `docs/LEDGER.md` line citing the issues
