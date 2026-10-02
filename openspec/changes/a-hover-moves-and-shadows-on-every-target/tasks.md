# Tasks

## 1. The vocabulary

- [x] 1.1 Add `Transform` and `Shadows` to `StyleDiff`, and to its runtime twin and wire shape
- [x] 1.2 Check: `VocabularyMirrorShapeTests` holds the twin to the record

## 2. The web

- [x] 2.1 Write a state's transform, backdrop blur and composed shadow list in the C# realizer and its TypeScript twin, the same strings on both
- [x] 2.2 Check: the realizer tests pin the declarations, and the twin's spec writes the same ones

## 3. Photon

- [x] 3.1 Compute a hovered box's effective style once and paint every member from it, gliding under the box's `Transition`
- [x] 3.2 Check: the display list of a hovered box carries its transform, its elevation, its custom shadows, its opacity layer, its gradient and its backdrop blur, each failing without the change

## 4. Documentation

- [ ] 4.1 The wiki's styling section, English and Portuguese
- [ ] 4.2 `docs/FLUTTER-PARITY.md` rows for `Transform` and state-resolved styles, with their probes
- [ ] 4.3 One `docs/LEDGER.md` line citing the issue
