# eQuantic.UI.Material

Material Design 3 as a **write-once theme** for eQuantic.UI — a single `IAppTheme` that themes the
shared component library on **both** targets (web → DOM+CSS, native → Photon pixels). No component
knows Material exists; swapping the theme is the whole change.

This package references `eQuantic.UI.Primitives` only — a theme is target-neutral token data, exactly
like a component.

## The mechanism

Theming eQuantic.UI = providing an `IAppTheme`. Material is one such theme; your brand is another —
the same act:

```csharp
using eQuantic.UI.Material;
using eQuantic.UI.Primitives;

// The M3 baseline theme (dynamic-color seed #6750A4 transcribed into the token contract).
IAppTheme theme = MaterialTheme.Instance;

// It carries the M3 color roles, type scale, shape scale and elevation:
theme.Colors(Variant.Primary).Base;   // #6750A4 / #D0BCFF
theme.Colors(Variant.Tertiary).Base;  // M3 tertiary, a first-class role
theme.Shape(ShapeScale.Large);        // 16dp (the M3 shape ladder)
theme.Type(TypeRole.Title);           // title-large 22/28
```

The web realizer generates the embedded CSS from this theme; the native host renders Photon pixels
from it. A custom brand theme is the same: implement `IAppTheme` (or, later, derive one from a seed
color).

## Mapping (M3 role → eQuantic token)

| M3 | eQuantic |
|---|---|
| primary / secondary / tertiary / error | `Variant.Primary` / `Secondary` / `Tertiary` / `Destructive` (Base/OnBase = role/on-role; Subtle/OnSubtle = container/on-container) |
| surface / surface-container-low / surface-variant | `Background` / `Surface` / `SurfaceSubtle` |
| outline-variant / outline | `Border` / `BorderStrong` |
| on-surface / on-surface-variant | `TextPrimary` / `TextSecondary` |
| M3 type scale | `Type(TypeRole)` |
| M3 shape scale | `Shape(ShapeScale)` |

`Success` / `Warning` / `Info` are eQuantic extensions toned within the M3 language.

## Status

v1: the fixed M3 baseline scheme. Dynamic color from a seed (HCT tonal palettes) and the app-wide
theme-selection wiring (SSR bridge + client boot picking Material) are later slices.
