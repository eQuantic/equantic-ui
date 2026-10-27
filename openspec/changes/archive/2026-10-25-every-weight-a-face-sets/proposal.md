## Why

Closes #1082, a task under #1038 (the document and its faces come from the theme), under the epic #1035.
`FontWeight` names five weights, `Regular` (400) to `ExtraBold` (800), the weights of the family the SDK
first bundled. A provider serves a family from 100 to 900 (#1057), and a theme can neither set text in
`Thin`, `ExtraLight`, `Light` or `Black` nor ask the build for them.

Measured on the way: the weights the vocabulary does name are not all drawn either. The runtime turns a
weight's name into a number through a hand-written switch, so a member it does not list falls to 400.
Photon on Apple and on Android draws only regular or bold, the bold trait at 600 and above, so `Medium`
draws as `Regular` there and `SemiBold` as `Bold`, and a family that ships a file per weight is drawn from
its Regular or its Bold file whatever the weight asked.

## What Changes

For a developer using the SDK, four more weights to name, and every weight drawn as named:

```csharp
TypeStyle.OfSize(48, FontWeight.Light)
[assembly: GoogleFont("Inter", FontWeight.Thin, FontWeight.Black)]
```

- **`FontWeight` names the nine weights OpenType defines**: `Thin` (100), `ExtraLight` (200), `Light`
  (300) and `Black` (900) join the five, appended.
- **The web draws each one**, from the server and from the runtime alike. The runtime reads a weight's
  number from a table generated from the C# enum, in place of the hand-written switch.
- **Photon on Apple asks CoreText for the weight itself**, its weight trait, so the system face draws every
  weight, the italic asked with it. A family that ships files is drawn from the file that sets the weight
  and the slant, opened directly, chosen as a browser chooses among a family's faces, and so is it on
  Android: the match by weight CoreText offers picked a neighbouring file for Thin, Light and Black. A
  variable file is opened at the weight on its axis.
- **Photon on Android asks for the weight** from Android 9 (API 28), where a typeface takes one. Below it
  the shell draws regular and bold, as today. Windows already hands DirectWrite the number.
- **`GoogleFont` takes all nine**, since it takes `FontWeight`. Measured on the way: Google's static Thin and
  ExtraLight of Inter both carry the weight class 250, the value Windows's GDI needs, and the catalog read
  them as one face and stopped the build (EQ4006). It reads a static face's weight from its `STAT` table now,
  where Google's files say 100 and 200, and falls back to the weight class where a `STAT` says nothing.

The parts it reaches: the vocabulary (`FontWeight`), the runtime's TypeScript twins (their generator and
`cssFontWeight`), eqicon's font catalog (its reading of a face's weight), the native framework's shipped
faces (the file a weight is set in), and the Apple and Android shells. The public surface adds four members
to `FontWeight`, appended, so no value moves, and `ShippedFaces.FileFor` to the native framework. The
developer surface does not move. Nothing an app wrote breaks, and one thing an app drew changes on Apple
and on Android: `Medium` and `SemiBold` draw at their own weights, where they drew as `Regular` and `Bold`.
