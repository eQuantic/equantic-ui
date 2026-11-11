# Design

## How Flutter answers it

`ThemeData` carries a component theme per family (`ButtonThemeData`, `InputDecorationTheme`,
`ChipThemeData`…), every member nullable, and a widget resolves each value from its own argument, then
the nearest component theme, then its defaults. `VisualDensity` shifts sizes by the target. The slots
take that shape, with the shared `Controls` standing in for what `Sizing` held, and `Density` read at
every step.

## Data, never a delegate

A slot crosses to the browser in the theme's JSON, so it holds data only: numbers, flags, enums,
colours, insets, type styles and records of them. A value that moves with the size is a `SizeLadder` (a rung per size), and one that moves with
size and density a `DensityLadder` (a `SizeLadder` per density). Every rung is optional, so a theme
that changes the medium height names that rung alone, and the rest fall through.

## The order lives in one place

`ControlMetrics`, in the components, resolves each number of the ladder: the family's own ladder (a
Button's `ButtonTheme.Controls`), then the theme's `Controls`, then `Sizing`, at `context.Density`.
Every control that read `Sizing` reads it there, so a theme that changes a rung changes every control
of that rung, and a test holds the list of controls measured to the sources that read the height.
The Accordion's row stays on `Sizing`: it is a row of a list, not a control, and joins with the
`List` slot (#1244).

## `With` is an extension

The plan named `With` a member of the theme. A default interface member is reachable only through an
`IAppTheme` reference, so `PhotonTheme.Instance.With(…)` would not compile. `AppThemeExtensions.With`
takes any theme, and returns an `OverriddenTheme` that declares every member of `IAppTheme`, the
defaults included: `With_DeclaresEveryMemberOfTheTheme` fails on a member left to the interface, which
is the loss a hand-written decorator has. Its runtime twin is a static, as `VisualNodeExtensions` is.

## Every member crosses

`EveryMemberOfTheTheme_CrossesToTheBrowser` reads the members of `IAppTheme` by reflection and fails
on one with no key in the JSON. That found the code palette (#936): `ThemeBridge` writes every kind's
colour, and the browser's `code(kind)` answers from it.

Every slot crosses through one writer, `ThemeBridge.SlotJson`, by reflection: each member of
`IAppTheme` whose type is a slot record, each member of it that answers, under its camel-cased name,
and a kind it has no form for throws rather than writing nothing. The generator of the runtime's
Photon theme calls the same writer, so the two producers of a theme the browser reads cannot
disagree, and a family's slot crosses with nothing written in the bridge or the generator: in the
runtime it adds its mirror and a builder registered in `slotBuilders`, which the materializer walks.
`EveryMemberOfEverySlot_CrossesUnderItsName` sets every member of every slot and reads each back.

## Which colours a slot carries

A colour the component chooses itself, a role other than its variant's five or an opacity it applies,
is the slot's: the Button's outline (`BorderStrong`) and the fill its quiet variants take when hovered
or pressed (`SurfaceSubtle`). The colours a variant fills with stay `Colors(variant)`'s, already the
theme's.

## Byte for byte

`PhotonTheme` fills its slots from `Sizing` itself, rung by rung, and with the Button's constants, so
every control resolves the number it drew before. `PhotonsControlsSlot_IsTheLadder` holds it, and the
SSR fixtures, the goldens and the transpiled pins are the proof.

## Material

Material 3 specifies its sizes per component rather than as one ladder of controls, so its `Controls`
stays null, the design system's own. Its Button is the common button: a fully rounded container, the
label in Label large (14, weight 500, tracking 0.1), 24 of padding, an 18 icon and an 8 gap at the
medium size it specifies, a 1 outline, and a text button's 12 of padding for the link variant.
