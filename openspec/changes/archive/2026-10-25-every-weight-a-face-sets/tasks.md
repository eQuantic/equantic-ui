# Tasks

## 1. The vocabulary

- [x] 1.1 `FontWeight` gains `Thin` (100), `ExtraLight` (200), `Light` (300) and `Black` (900), appended
- [x] 1.2 Check: `EnumValueAbiTests` holds every value where it was, and the public surface declares the four

## 2. The web

- [x] 2.1 `EnumUnionsTsGenerator` writes `FontWeightNumbers`, each weight's name and number, from the enum,
      and `cssFontWeight` reads it in place of its switch
- [x] 2.2 Check: the runtime's tests over the nine names, the generated file's pin, and a probe app in a
      browser setting Inter, fetched from Google, in the nine weights, on a page the server renders and on
      one the browser builds

## 3. Photon

- [x] 3.1 CoreText is asked for the weight trait, for a named family and for the system face, the italic
      asked alone after it, the font cache keyed by the weight
- [x] 3.2 Android asks for the weight from API 28, and keeps regular and bold below it
- [x] 3.3 A family that ships files is drawn from the file of its weight and slant, opened directly, chosen
      by one rule the Apple and Android shells call (`ShippedFaces.FileFor`, a browser's), a variable file
      on Apple at the weight on its axis; eqicon's catalog reads a static face's weight from its `STAT`
- [x] 3.4 Check: on macOS, the system face draws the nine weights nine ways, and a family that ships files
      at 300, 400 and 700 measures as the file a browser would choose for each weight; the file choice's
      tests on any machine; the catalog over two faces that share a weight class; a variable file measured at
      six weights on its axis, against a test face whose advances vary with it; a Photon probe drawn with
      no window in the nine weights of Inter, each measuring what a browser measures; the same probe on the
      Android emulator, both ways

## 4. Documentation

- [x] 4.1 The wiki's DesignSystem page, in all five languages: the nine weights, and the line that said
      they were not there yet gone
- [x] 4.2 One ledger entry citing #1082, as a file of its own under `docs/ledger/`
