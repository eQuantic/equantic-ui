- **2026-11-04 · A person chooses files on the web, by a picker or by dropping them**: a task under #1212
  ([#1213](https://github.com/eQuantic/equantic-ui/issues/1213)), the first slice of uploading files. The vocabulary
  gains `PickedFile`, a file a person chose with its name, MIME type, size and content and never a path, which a
  browser does not give and a phone answers otherwise; `IFilePicker`, the portable way to ask a person for files,
  realized in the browser through the file input the photo library already opened, which both now share; and
  `DropTarget`, a layout-transparent area that hands over the files dropped on it and says when files are carried
  over it. The web realizer declares the four drag events and the runtime answers them, keeping the set of
  elements a drag entered on the area's element, because a counter drifted when the "over" report re-rendered
  the area; once a page has shown a drop area, a file let go outside every area is refused instead of opened over
  the page. `FileFilter` gained its runtime twin. Photon renders a drop area's child and takes nothing, a door
  #1216 closes for the desktop shells, and D15 (#782) for the phones. Proposed and archived through OpenSpec
  (`file-picking`).
