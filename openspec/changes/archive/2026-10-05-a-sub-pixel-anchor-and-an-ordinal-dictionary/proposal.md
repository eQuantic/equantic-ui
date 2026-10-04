# Proposal

Closes #576, a sub-issue of #208 (the audit continues), and #577, a sub-issue of #565 (the
transpiler's fences hold on every path). Both were met by equantic-web on 0.2.0-preview.60.

## Why

Two things a site meets on 0.2.0-preview.60 answer worse than 0.2.0-preview.59 did. A fragment link
to a page under a floating header lands its target BEHIND the header on a warm load. And a
`Dictionary<string, T>` built with `StringComparer.Ordinal`, the default for a string key, fails the
build with EQ1004.

## What Changes

- **A fragment lands below the pinned chrome when the jump leaves it a fraction of a pixel above the
  top.** The runtime's cold-load correction re-scrolls a fragment target that the browser's jump left
  under the chrome, and it only took a target whose top was in `[0, offset)`. Layout positions are
  fractional (Chrome lays out in 1/64 px) and a scroll offset is whole device pixels, so the jump
  lands the target within a pixel of the top on either side. On equantic-web's `/terms#liability`,
  warm, the target sat at -0.203125 on every frame of the watch and was never corrected. The band
  now starts one device pixel above the top, and never less than one CSS pixel. A reader a whole
  device pixel or more past the target is still left where they are.
- **A dictionary built with a comparer that asks for the default builds again.** #443 made the
  dictionary strategy refuse every constructor with a comparer parameter (EQ1004), whatever the
  argument. That was a second, stricter copy of the fence every creation already passes (EQ2007),
  which accepts `null`, `EqualityComparer<T>.Default`, `Comparer<T>.Default` and
  `StringComparer.Ordinal`. The construction now skips the comparer argument and leaves the verdict
  to the fence. A comparer is never taken as the dictionary's seed, and a sorted dictionary or list
  keeps the order its comparer asks for: code-unit order for `StringComparer.Ordinal`, the key
  type's own for the default. A comparer that changes equality (`StringComparer.OrdinalIgnoreCase`)
  is still refused, now with EQ2007 alone instead of EQ2007 and EQ1004.

For a developer using the SDK: a fragment link lands one header below the top on every load, and
`new Dictionary<string, T>(StringComparer.Ordinal) { [key] = value }` compiles and answers as .NET's.
Nothing is refused that was accepted.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `links`: a fragment link lands its target below the pinned chrome, including when the browser's
  jump leaves it a fraction of a pixel above the top.
- `runtime-dictionaries`: a dictionary built with a comparer that asks for the default builds, and a
  sorted one keeps the order its comparer asks for.

## Impact

- The runtime: `src/shared/sticky-offset.ts` (the cold-load correction's band).
- eqc: `CodeGen/Strategies/Types/DictionaryStrategy.cs` (construction and its factory).
- Not reached: the web realizer's C#, the Photon shells, the SDKs and the templates.
- The public surface and the developer surface do not move.
