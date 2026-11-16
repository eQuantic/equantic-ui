# context-requests Specification

## Purpose
How a person asks for the menu of something on every target, by a secondary click, a finger's hold or the
keyboard, which node answers, where the request says it was asked, and the ready-made context menu built on it.

## Requirements

### Requirement: A node asks for its menu

A `ContextTarget` SHALL report a `ContextRequest` to its `OnRequested` when the person asks for the menu of
what it wraps: a secondary click (the right button, or a Control-click on a Mac), a finger held still for
500 ms, or the menu key or Shift+F10 while the focus is inside it. `At` SHALL be the point relative to the
target's top-left corner for a pointer or a finger, and null for the keyboard. When targets are nested, the
innermost one under the pointer or around the focus SHALL answer, and only it. On the web the browser's own
menu SHALL NOT open where a target answers. A disabled target SHALL answer nothing, and a text field inside a
target SHALL keep its own menu.

#### Scenario: A right-click on a tree's row

- **WHEN** a row wrapped in a `ContextTarget` is right-clicked 10 px right of its left edge and 6 px below its
  top
- **THEN** `OnRequested` receives a request whose `At` is (10, 6), and the browser opens no menu of its own

#### Scenario: The keyboard asks

- **WHEN** the focus is on a button inside a `ContextTarget` and the person presses Shift+F10
- **THEN** `OnRequested` receives a request whose `At` is null

#### Scenario: A finger's hold inside a sortable item

- **WHEN** a sortable item inside a `ContextTarget` is held by a finger that does not move
- **THEN** the item is lifted after 250 ms, and no request is reported

### Requirement: Photon receives every way of asking

The Photon host SHALL take a secondary click from its shells (`ContextRequest(x, y, kind)`), count a finger's
hold itself, and take the menu key as `ContextMenu` and Shift+F10, reporting the same requests the web does.
The macOS shell SHALL deliver the right button and a Control-click, the Windows shell the right button and the
Applications key, the Android shell a mouse's secondary button and the menu key, and the iOS shell a pointer's
secondary click.

#### Scenario: A long press on Photon

- **WHEN** a finger presses a row wrapped in a `ContextTarget` and frames go by for 500 ms with no travel
- **THEN** `OnRequested` receives a request at the finger's point, and the row's press handler does not run

### Requirement: A ready-made context menu

`ContextMenu(child, items, onSelect)` SHALL open the `Menu`'s panel of `items` when its child asks for its menu,
at the requested point, or along the child's bottom edge when the keyboard asked; it SHALL take the same keys,
dismissal and selection the `Menu` does.

#### Scenario: A context menu opened by the keyboard

- **WHEN** the focus is inside a `ContextMenu` and the person presses the menu key, then ArrowDown and Enter
- **THEN** the panel opened under the child, and `onSelect` received the first item's index
