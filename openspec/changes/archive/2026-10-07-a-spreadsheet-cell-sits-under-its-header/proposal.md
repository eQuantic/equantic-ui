# Proposal

#613 (under #207, handoff fidelity): on the web the spreadsheet drew every cell one column left of its
header, over the row numbers.

## Why

The row number strip was a Column that hugged its row headers, and a row header is a Stack whose
layers may not grow past it (`min-width: 0`), so the strip's own minimum was zero. Beside a grid wider
than the window the browser shrank it to nothing, measured in Chromium at 0px: A1 sat over row 1's
number, and column B under the "A". Photon never shrinks a rigid child, so there the cells started
after the strip all along.

## What Changes

- **The row number strip is as wide as its header, and says so**: a Fixed width, which the web
  realizer lowers with `flex-shrink: 0` and Photon measures as it always did. The cells start after the
  numbers on both targets.
- The general case behind it, a hugging Stack that a flex line can squeeze to nothing on the web, is
  #698.

## Parts reached and surfaces moved

The shared `Spreadsheet` component and its transpiled twin. No public or developer surface moves.
