# Proposal

Closes #1398, a Task under #938 (the theme slots).

## Why

`PhotonOptions` is bound from configuration by the configuration binder's source generator, and it held
the app's theme. Configuration never reaches a theme, but the generator walks every type the bound
options reach anyway, the theme's slots included. A slot record with a `TypeStyle` member made it
write `Fluid = Fluid;`, which does not compile in `eQuantic.UI.Native.Hosting`, so no slot could
carry a type style, and E6b and E6c split their label styles into numbers to stay buildable.

## What Changes

- `PhotonOptions.Theme` goes. A Photon app chooses its theme with `builder.UseTheme(theme)`, which
  it already could, and `PhotonApplication.Theme` carries it to the shells. Photon's own theme stays
  the default.
- The bound options reach no theme, so the binder walks nothing the theme holds, and the project no
  longer suppresses the binder's warnings (SYSLIB1100, SYSLIB1101).
- The samples and the `equantic-native` template call `UseTheme`.

A break: `builder.Configure(photon => photon.Theme = theme)` becomes `builder.UseTheme(theme)`. The
parts reached are the Photon host and its four shells. The developer surface does not move.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `design-system`: a Photon app's theme is chosen in C# alone.
