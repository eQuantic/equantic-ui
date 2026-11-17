## Why

Closes #1564, a user story of #1534 (sortable lists and drag and drop), under the epic #1510. The keyboard's sorting
steps (#1536) are spoken on the web from the SDK's culture catalog, which the browser's controller reads itself, and
Photon does not speak them at all (#1537): the SDK's words live in `eQuantic.UI.Components`, and the Photon realizer
does not reference that library. An app cannot say the steps in its own words on either target. Edgar's decision
(2026-11-15): the words become data on the node.

## What Changes

- **The words are on the node.** A new vocabulary record, `SortAnnouncements`, holds what a reader hears while an
  item is sorted by the keyboard: the instructions, and the steps picked up, moved, moved inside, refused, dropped,
  dropped inside and put back. Each text has numbered holes the realizer fills (`{0}` the item's name, then its
  position, the count and the list's name, or the item it lands inside). `Sortable` takes it as a constructor
  parameter, `Announcements`.
- **The SDK fills it, and an app can replace it.** `UI.Sortable(…)` takes `announcements:`. Left out, it is the SDK's
  words in the reader's culture (`SdkStrings.Sorting`, the eight strings `SdkResources` already has). An app passes
  its own record, in C#, to say the steps its way. A `TreeView` that moves its nodes says the SDK's.
- **The web reads the node's words.** The lowering writes each text on the list's element, and the controller says
  every step, and describes the focused item, from the list the item was picked up from, instead of the catalog.
  A `Sortable` built with no words says nothing.
- **Photon speaks the steps.** The keyboard's carry says each step through the live region of the list it was picked
  up from, the same mechanism a spreadsheet's moves use (#1081): macOS and iOS post it as an announcement, and
  Android marks the list's node live with the words it holds, which TalkBack reads when its content is reported
  changed. An item's name is what a reader hears it called, the labels of what it holds.
- **Not in this change.** On Photon the focused item does not describe the keys yet: a semantic node has no hint
  for the shells to carry, which is its own change (#1596). The look of the lift goes to `SortableTheme` in its own
  slice (#1545).

Parts reached: the vocabulary (`eQuantic.UI.Primitives`), the components (`UI.Sortable`, `SdkStrings`, `TreeView`),
the web realizer (C# and TS lowering), the runtime's sortable controller, and the Photon host's keyboard carry,
emit and semantics walks. The public surface grows (`SortAnnouncements`, `Sortable.Announcements`, the factory's
`announcements`), and `Sortable`'s constructor widens; nothing goes. The developer surface does not move.

Principles followed: 1 (the words are C#, no platform artifact), 4 (written once: the record and its hole filling
are transpiled to the runtime, and both realizers read the same data), 5 (the record names steps, not a target's
announcement API), 13 (the constructor widens, with nothing kept beside it), 15 (a ready-made part takes the
developer's own words, as it takes their content and style).
