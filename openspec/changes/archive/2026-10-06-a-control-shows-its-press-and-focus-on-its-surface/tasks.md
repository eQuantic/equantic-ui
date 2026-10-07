# Tasks

## 1. The vocabulary

- [x] 1.1 Add `Pressed` to `BoxStyle`, its runtime twin and wire shape, and restate `Focus` as the control's focus
- [x] 1.2 Check: `VocabularyMirrorShapeTests` holds the twin to the record

## 2. The web

- [x] 2.1 The focus and pressed rule families in both atomizers, with their specificity, and the realizer and its TypeScript twin writing a box's `Focus` and `Pressed` diffs into them, simulated states over the base
- [x] 2.2 The ring's slot at the head of every shadow list, on both producers, and the generated stylesheet's ring rules, `eq-focused` on a simulated focus
- [x] 2.3 Check: the realizer tests pin the rules, the parity fixture compares both producers on a control that sets every state, and a browser shows a press, a focus with the ring beside the shadows, and their order

## 3. Photon

- [x] 3.1 The press scope's pressed and focused flags for the control's subtree, and the effective style laying focus and pressed over hover
- [x] 3.2 The custom shadows gliding by position, padded with transparent ones
- [x] 3.3 Check: display lists of a pressed and a focused control, their order, a simulated state, and a glide, each failing without the change

## 4. Documentation

- [x] 4.1 The wiki's styling section, English and Portuguese
- [x] 4.2 The `WidgetStateProperty` row of `docs/FLUTTER-PARITY.md`
- [x] 4.3 One `docs/LEDGER.md` line citing the issue
