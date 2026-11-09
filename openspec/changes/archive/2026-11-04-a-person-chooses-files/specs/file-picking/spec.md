## ADDED Requirements

### Requirement: A person chooses files, answered as PickedFiles

`IFilePicker.PickFilesAsync` SHALL answer with a `PickedFile` for each file the person chose, carrying its name, its MIME type (`application/octet-stream` when the target names none), its size, and content that `ReadAllBytesAsync` and `ReadAllTextAsync` read whole. `PickFileAsync` SHALL answer with one file. Neither SHALL expose a path.

#### Scenario: Two models chosen in the browser

- **WHEN** a page calls `PickFilesAsync()` from a press and the person chooses `bracket.stl` (3 bytes) and `lid.stl` (bytes 4, 5)
- **THEN** the answer is two `PickedFile`s named `bracket.stl` and `lid.stl`, the first of size 3, and reading the second's bytes gives 4, 5

#### Scenario: A file the browser cannot type

- **WHEN** the person chooses `part.step` and the browser reports no type for it
- **THEN** its `MimeType` is `application/octet-stream`

### Requirement: Choosing nothing is an ordinary answer

When the person cancels the picker, `PickFileAsync` SHALL answer null and `PickFilesAsync` SHALL answer an empty list. A cancelled token SHALL answer the same and remove the input. Where the browser reports that no gesture is live, the picker SHALL answer nothing chosen without opening, since the browser would open nothing and report nothing.

#### Scenario: The person closes the picker

- **WHEN** the browser reports a cancel on the input
- **THEN** `PickFileAsync` answers null, `PickFilesAsync` answers an empty list, and no input is left in the document

#### Scenario: A call outside a gesture

- **WHEN** `navigator.userActivation.isActive` is false and a page calls `PickFilesAsync()`
- **THEN** it answers an empty list and no input is created

### Requirement: The filters are what the picker offers

The picker SHALL offer the extensions of every filter, each dotted once and lower-cased, and SHALL offer anything when there is no filter or a filter names no extension.

#### Scenario: Two filters

- **WHEN** the filters are `Images` (`png`, `JPG`) and `Models` (`.stl`)
- **THEN** the input's `accept` is `.png,.jpg,.stl`

### Requirement: A drop area hands over the files dropped on it

A `DropTarget` SHALL call `OnDropped` with a `PickedFile` for each file dropped on it, and `OnDragOverChanged` with true once when files are carried over it and with false once when they leave or land, however many of its children the drag crosses and whatever a re-render removes under the pointer. A drag that carries no files SHALL be ignored. A disabled area SHALL refuse the drop and call no `OnDropped`. Of nested areas, only the innermost SHALL take the files.

#### Scenario: Crossing the children

- **WHEN** files are dragged into an area, across its label and its button, and out again
- **THEN** `OnDragOverChanged` is called with true and then with false, once each

#### Scenario: A re-render removes the element under the pointer

- **WHEN** files are over an area's label, the label is removed, and the drag enters and leaves the button
- **THEN** `OnDragOverChanged` is called with true and then with false, once each

#### Scenario: Dropping two models

- **WHEN** `bracket.stl` and `lid.stl` are dropped on an enabled area
- **THEN** `OnDragOverChanged` reports false and `OnDropped` receives the two files in order

#### Scenario: A disabled area

- **WHEN** a file is dragged over and dropped on an area with `Enabled = false`
- **THEN** the drag is answered with the drop effect `none` and `OnDropped` is not called

### Requirement: A file that misses every drop area is refused

Once a page has shown a drop area, a file let go outside every area SHALL be refused rather than opened by the browser over the page. Text and links dragged across the page SHALL be left to the browser.

#### Scenario: A drop that misses

- **WHEN** a page has lowered a `DropTarget` and a file is dropped on the page outside it
- **THEN** the drop's default is prevented and the drag's drop effect is `none`

### Requirement: The web realizes a drop area as a transparent element on both producers

The server render and the client lowering SHALL write the same element for a `DropTarget`: a `div` that passes its child's fill through, marked `data-eq-drop` `on` or `off`, declaring `dragenter`, `dragover`, `dragleave` and `drop`. On Photon a `DropTarget` SHALL draw its child.

#### Scenario: Both producers agree

- **WHEN** the component parity fixture lowers a `DropTarget` over a box that fills across, and a disabled one
- **THEN** the server's tree and the client's tree are identical
