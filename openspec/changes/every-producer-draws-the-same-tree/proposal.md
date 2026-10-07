# Proposal

Five bugs where the same tree is drawn differently by the producers that draw it: the C# realizer
that serves a page, its TypeScript twin that renders it in the browser, and Photon. One family of
the 2026-10-07 bug sweep:

- #495 (under #166, geometry and semantics live in the vocabulary): a Text inside a Box or a
  Flexible is laid out on the parent's 16px line, not its own.
- #506 and #511 (under #504, StyleDiff gains transform and shadow): a pinned header's scrolled style
  is drawn by half its members on the web and not at all on Photon; a draggable's resting offset and
  its box's own transform take the same CSS property.
- #622 (under #194, pointer, gestures and focus): under a pointer, a Pressable whose child draws no
  box keeps that child's content under the hit slop.
- #623 (under #282, the SSR-to-client seam): a server-rendered page shows Comfortable controls under
  a mouse until a component re-renders, and then that component snaps to Compact.

## Why

Each one is a member, a setting or a measurement that one producer honours and another ignores or
draws otherwise. That is the defect class the product principle rules out: the developer writes one
tree, and it has to mean one thing.

## What Changes

- **A Text is a block on the web, wherever it sits.** As an inline span inside a block parent (a Box,
  a Flexible, a Link, a Pressable) it sat on the parent's line box and its body-font strut: a 10/15
  label in a padded pill measured 26.5px where its parts add up to 21. As a block it has its own
  line box, and it takes the width its parent gives, as Flutter's Text does under a tight width.
- **A pinned header's `ScrolledStyle` applies every member, on every target.** The web wrote four
  members of seven and a border alone, and Photon read nothing. It goes through the builder a box's
  states use, over `Pinned.ScrolledBase` (whose border is the hairline along the bottom edge), and
  Photon draws it, gliding under the header's `Transition`. The surface that decides is the one the
  header pins to: the nearest ScrollView, or the page.
- **A state's border follows the edges its box draws**, and a width alone draws in the base's
  colour, on the web as on Photon. A hover border on a box with one edge drew all four.
- **A draggable's offset rides the individual `translate` property**, on both producers and in the
  drag controller, so the box keeps its own `transform` and its hover's. A release hands the surface
  back to the markup, so a swipe that changes nothing glides home instead of staying put.
- **The hit slop's lift reaches through wrappers that draw no box** (an InView, an Adaptive's arms, a
  light and dark Image): the first descendants that draw one carry `eq-lift`.
- **A served page is built at the density the browser asked for.** The runtime leaves the density
  its pointer asks for in a session cookie, the server builds the page at it and says which density
  it used, and hydration lowers at that density and then switches the whole page at once when the
  browser's own differs.

What a developer writes does not change: every one of these is the SDK keeping a promise the tree
already made.

## Parts reached and surfaces moved

- The web realizer (C#) and its TypeScript twin, the drag controller, a new runtime module for the
  scrolled state, the boot, the server's render and its page configuration, and Photon's emit pass.
- Public surface: `HtmlStyle.Translate` (the DOM escape hatch transcribes `translate`),
  `Pinned.ScrolledThreshold` and `Pinned.ScrolledBase`, and a `density` parameter on
  `WebRealizer.Lower` and `VisualNodeComponent`. The developer surface (csproj, appsettings,
  templates) does not move.

## Migration

- A Text is a block on the web: a sentence composed of several Texts inside an `HtmlElement` puts
  each on its own line. A sentence is one Text with `Spans`.
- `WebRealizer.Lower` and `VisualNodeComponent` take a trailing `density` (Comfortable by default).
- The scrolled rules select on the header's `data-eq-scrolled` instead of `html.eq-scrolled`.
