- **2026-11-11 · A code surface is read as a field holding its text**: on the web a code surface's door
  was an empty `textarea` and the text it drew was read as lines of their own, and on Photon the code
  field carried no value, so its text was not read at all. The vocabulary now says what a reader hears a
  code surface hold (`ICodeSurfaceModel.Reading`, a `TextReading`): the code engine answers the lines
  around the caret, ten on each side, with the selection in them, and the formula bar and the name box
  what they show. The web writes it as the door's content, keeps the door's value and selection in step
  after each render (never during a composition) and hides what the surface draws, and Photon gives the
  field the text as its value. The spreadsheet's door holds what the grid says now, as Photon's field
  does ([#1333](https://github.com/eQuantic/equantic-ui/issues/1333)). Proposed and archived through
  OpenSpec (`openspec/specs/screen-reader-parity`).
