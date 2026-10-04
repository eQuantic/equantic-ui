# Design

## Context

See proposal.md for why. Two existing mechanisms decide each half, and both fixes stay inside them:
the runtime's cold-load correction in `sticky-offset.ts`, a watch of frames that re-scrolls a fragment
target found under the chrome, and the fence every creation passes in eqc,
`CollectionComparerExtensions`, which judges a comparer handed to a collection's constructor.

## Goals / Non-Goals

**Goals:**

- The correction recognises the state it exists for whichever way the layout's fraction rounds.
- A comparer is judged in one place, the fence, and a strategy only lowers what the fence passed.

**Non-Goals:**

- Translating a comparer that changes equality (`OrdinalIgnoreCase`, a culture). The fence keeps
  refusing it. A key normaliser in the runtime's dictionary is its own change.
- `HEAD` on a page route (#575), met on the same proof, and a separate defect.

## Decisions

**How Flutter answers the anchor.** It has nothing to correct. `Scrollable.ensureVisible` computes the
destination from the target's render object and the viewport's own insets, so a fixed header is part
of the arithmetic from the start. The web has a step Flutter does not: the BROWSER performs the
fragment jump, before the runtime has measured the chrome, so a correction after the fact is the only
door. `docs/FLUTTER-PARITY.md` has no row for it, and the correction is web-only.

**A band one pixel above the top, not rounding the measurement.** Rounding `top` before the test
would also take -0.203125. But it would take +0.5 as 1 and -0.5 as -1 (or 0, by the rounding mode),
which moves the question to another fraction. The jump's error is bounded: a whole-pixel scroll
offset against a layout position, so less than one CSS pixel on any device-pixel ratio of 1 or
more. The band says that bound directly, as a named constant whose comment carries the measurement.

**The fence decides, and the strategy skips.** #443 refused any constructor with a comparer
parameter inside `DictionaryStrategy`. Removing that alone would break the other way: the
construction's argument loop took every argument that is not a capacity as the seed, so the comparer
would become the dictionary's source, the defect the fence's documentation records. The construction
now reads the bound operation, which says which parameter each argument fills. A comparer is skipped,
and for a sorted dictionary or list it supplies the ordering through the fence's own
`OrderingAskedFor`, the shape `SortedSet`'s construction already has (#522). Copying the fence's test
into the strategy again was the alternative, and it is the shape that broke here.

## Risks / Trade-offs

- A reader who scrolled up by a FRACTION of a pixel within the watch's window would be re-scrolled by
  under a pixel. Nobody can see that, and the watch retires after twenty still frames.
- Without a semantic model (a strategy-rewritten node), a creation still reads a number as a capacity
  and any other argument as the seed. That path is unchanged, and the fence does not run there either.
