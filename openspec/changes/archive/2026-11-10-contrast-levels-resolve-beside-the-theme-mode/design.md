# Design

## How Flutter answers it

`MediaQueryData.highContrast` carries the platform's setting, and `CupertinoDynamicColor` holds a
high-contrast colour beside each brightness (`color`, `darkColor`, `highContrastColor`,
`darkHighContrastColor`), resolved against the context. `ColorToken` takes the same shape: a pair
per level, resolved with the mode and the level, and a token that states no high-contrast pair keeps
its standard one, as `CupertinoDynamicColor.withBrightness` does.

## One resolution, two axes

`Resolve(ThemeMode, ContrastLevel)` replaces `Resolve(ThemeMode)` rather than sitting beside it: a
second overload with a default level would let a paint site forget the level and compile, which is
the defect this change exists to prevent. Every call site was fixed; the native realizer threads
the level through `EmitState` beside the mode.

The More legs are stated with `WithMore`, not `init` properties: the configuration binder's source
generator writes a self-assignment for an `init` property it cannot read back (CS1717 in
`eQuantic.UI.Native.Hosting`), and a method reads better in a theme anyway. A More leg equal to its
Standard one is stored as no leg at all, so equality, hashing and the web's variable map read the
token, never how it was written.

## The web switches variables, not classes

The handoff suggested emitting the More values for the same atomic class under the contrast query.
The atomizer never sees a token, only the CSS it lowered to, so it cannot know a class's More value.
Every theme colour already reaches an atomic class through `var(--eq-color-…)`, so the stylesheet
declares those variables again under `@media (prefers-contrast: more)`, and every class follows with
no rule of its own and no growth in the atomic sheet. Two consequences are held by tests:

- The derived hover (`VariantColors.Hover`) was inlined, not a variable, so it would have stayed
  Standard at More. It is now registered as `--eq-color-{variant}-hover` by the stylesheet, the C#
  `ThemeVarMap` and its TypeScript twin, in the same order.
- The map is keyed by a colour's Standard CSS, so two roles sharing a Standard pair read one
  variable. `RolesThatShareAStandardPair_ShareTheirMorePair` fails if the shipped palette ever gives
  them different More pairs.

A token an app builds outside its theme, with More legs, resolves to Standard on the web, since it
reaches no variable. On Photon it steps up. Every colour a component reads comes from the theme.

## What is left for later

- The macOS window reads the mode and the level when it opens, as it read the mode before this
  change; following a change while it runs is #1382. iOS and Android follow both.
- A code block's inverse slab builds its colours from the dark leg alone and loses the More pair,
  which the runtime's `ColorToken` twin would have to carry first: #1383.

## Measured on the strictest ground

`DesignTokenTests` and `DesignTokenApcaTests` run once per level. At More, text is measured on the
strictest of Background, Surface and SurfaceSubtle against 12:1, 9:1 and 7:1, borders on the
strictest of Background and Surface against 3:1 and 4.5:1, labels against 7:1, and the APCA floors
step up to 90, 75 and 30. Link is left where it is by the handoff's More palette and keeps its floor.

## Proof

- `TheHostPaintsTheContrastLevelTests`: the same tree, mode and host paint the More fill at More and
  the Standard fill at Standard, and a level set on a running host repaints.
- `tests/e2e/tests/contrast.spec.ts`: Chromium with `prefers-contrast` emulated paints every muted
  element in its More leg, in light and dark, steps back down, and a screen the runtime builds after
  a navigation follows.
- A/B in disposable worktrees: without the contrast block, four stylesheet tests and all three e2e
  tests fail; a More pair removed from `tokens.json` or drifted by one step fails the pin; Muted left
  at Standard fails the APCA gate and the WCAG targets; a host that ignores its level fails the
  native proof.
