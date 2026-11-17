- **2026-11-17 · A sortable list says its steps in the words on its node, on the web and on Photon**: a `Sortable`
  carries `SortAnnouncements`, the instructions and the seven steps of the keyboard's sort, each with numbered holes
  that `SortWords.Fill` fills in one pass on both targets. `UI.Sortable` gives the SDK's words in the reader's culture
  unless the app passes its own (`announcements:`), and a list built with none says nothing. The web reads them from
  the list's element instead of the culture catalog, and Photon now speaks every step through the live region of the
  list the item was picked up from: macOS and iOS post it, and Android reads it from the list's node, marked live
  ([#1564](https://github.com/eQuantic/equantic-ui/issues/1564)). On Photon a focused item does not describe the keys
  yet ([#1596](https://github.com/eQuantic/equantic-ui/issues/1596)). Proposed and archived through OpenSpec
  (`openspec/specs/sortable-lists`).
