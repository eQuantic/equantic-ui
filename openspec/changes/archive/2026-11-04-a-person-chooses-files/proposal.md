## Why

Closes #1213, the first slice of Feature #1212 (an app cannot ask a person for a file and send it to its server), under Epic #190.

Before a file can be sent anywhere, a page needs a way to let a person choose it. The only picker in the SDK was `IFileDialogs`, which is desktop-only on purpose: it answers with a path, and a browser never hands a page one. A web app that needed a file had to drop to the `HtmlElement` escape hatch, or frame a hand-written HTML page, which is the escape hatch offered as the primary path.

## What Changes

- `PickedFile` (vocabulary): a file a person chose, with `Name`, `MimeType`, `Size`, `ReadAllBytesAsync()` and `ReadAllTextAsync()` (UTF-8), and never a path. Abstract: each target realizes it over what its picker answered, and a page never constructs one. It is the currency D15 (#782) realizes on the phones and slice B and C (#1214, #1215) send to the server.
- `IFilePicker` (vocabulary): `IsAvailable`, `PickFileAsync(filters)` answering null on a cancel, and `PickFilesAsync(filters)` answering an empty list. In the browser it is the same file input `IPhotoLibrary` opened, now shared by both (`devices/file-input.ts`), with the filters as the input's `accept`; outside a person's gesture it answers "nothing chosen" at once where the browser says so, rather than waiting for ever. The server render registers an absent picker, so a page that takes one renders there.
- `DropTarget` (vocabulary node) and its `DropTarget(child, onDropped, onDragOverChanged, enabled)` factory: a layout-transparent area that hands over the files dropped on it and says when files are carried over it. Only a drag carrying files is an invitation; a disabled area refuses the drop; the innermost area takes the files. On the web the server writes a `div` marked `data-eq-drop` that declares the four drag events and the client attaches them; once a page has shown a drop area, a file let go outside every area is refused instead of opened over the page.
- `FileFilter` gains its runtime twin, since a write-once component now constructs one.
- Photon: a `DropTarget` draws its child and takes nothing, a door the `EmitVisitor` states and #1216 closes; `IFilePicker` resolves to null on the native shells until #1216 and D15.

## Impact

- Reaches the vocabulary (`eQuantic.UI.Primitives`: `PickedFile`, `IFilePicker`, `DropTarget`, `IVisualNodeVisitor.Visit(DropTarget)`), the components' factory (`UI.DropTarget`), the web realizer (`WebLoweringVisitor`), the runtime (`picked-file.ts`, `devices/file-input.ts`, `devices/file-picker.ts`, `dom/file-drop.ts`, the lowering and the twins), the server's absent capabilities, and Photon's layout, emit and semantics walks and the email walk, which refuses the node.
- The public surface grows (contract): the three types, the visitor method and the factory. `developer-surface.baseline.txt` does not move.
- No break for an app. `IVisualNodeVisitor` gains a method, which an app implementing it would meet; none in this repository's consumers does.
