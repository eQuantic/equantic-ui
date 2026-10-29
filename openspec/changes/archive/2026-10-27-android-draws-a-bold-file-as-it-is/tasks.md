# Tasks

## 1. The shells

- [x] 1.1 `ShippedFaces.IsDrawnAsItIs` holds the rule #1099 wrote into the Windows shell, and the Windows shell
      reads it in place of its own copy
- [x] 1.2 From API 28 the Android shell asks for a shipped file it draws as it is at the weight the typeface
      reads in the file, and for any other at the weight of the style
- [x] 1.3 Unit test of the rule: a static file asked for its own weight, a lighter one and a heavier one, under
      600 and from 600, and a variable file (`ShippedFileChoiceTests`)
- [x] 1.4 Check on the emulator three ways, with a probe that rasterizes the four rows through
      `AndroidTextService` and compares each with row 1 byte for byte: with the change, rows 2 and 4 equal row 1
      and row 3 differs; with the shell as it was, rows 2 and 4 draw the fake bold of row 3; with the change but
      without the clause for a file weighed 600 or more, row 4 draws it alone. The Windows tests of #1099 hold
      the Windows shell on CI's Windows leg

## 2. Documentation

- [x] 2.1 The wiki's DesignSystem page already says a shell sets text in the file of its weight and slant; it
      holds on Android in this case now, and no page changes
- [x] 2.2 One ledger entry citing #1109, as a file of its own under `docs/ledger/`
