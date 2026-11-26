## Purpose

How a component holds the focus as an object and moves it from code, and how one traversal, written once,
decides where Tab, Shift+Tab and a request land on the web and on Photon.

## ADDED Requirements

### Requirement: A component holds a focus node and moves the focus with it

A `FocusNode` held by a component and handed to a `Focus` around any child SHALL stand for that child's
focus on the web and on Photon: `RequestFocus()` SHALL give the keyboard to the child's first traversable
stop, `Unfocus()` SHALL take it away when the node has it, `HasPrimaryFocus` SHALL be true while a stop the
node owns holds the keyboard, and `HasFocus` while any stop under the `Focus` does. A stop is owned by the
innermost `Focus` above it.

#### Scenario: A button asked for the keyboard from code

- **WHEN** a page holds two buttons, the second wrapped in `Focus` with a held node, and a handler calls
  `RequestFocus()` on that node
- **THEN** after the next write the second button has the keyboard, on the web as on Photon, and the node
  answers `HasFocus` and `HasPrimaryFocus` true

#### Scenario: Unfocus

- **WHEN** the node that has the keyboard calls `Unfocus()`
- **THEN** no control has the keyboard, and the node answers `HasFocus` false

#### Scenario: A group around a field and a button

- **WHEN** a `Focus` with a held node wraps a column holding a field and a button, and the button has the
  keyboard
- **THEN** the node answers `HasFocus` true and `HasPrimaryFocus` true, and `RequestFocus()` from elsewhere
  lands on the field, the first of its own stops

### Requirement: A request is applied after the write that follows it

A `RequestFocus()` SHALL take effect after the next write of the tree, so a control the same state change
removes is gone before the keyboard moves. A request made on a node that no `Focus` has shown yet SHALL be
honoured when one first shows it. Of two requests made before one write, the later SHALL win.

#### Scenario: A rename field closes and the tree takes the keyboard

- **WHEN** a field inside a tree has the keyboard, and one handler removes the field and calls
  `RequestFocus()` on the node the tree's `Focus` holds
- **THEN** after the write the tree's one stop has the keyboard and the field's focus-lost callback ran
  before it, on the web as on Photon

#### Scenario: A request before the first build

- **WHEN** a component calls `RequestFocus()` on a node in its constructor, and its first build wraps a
  button in `Focus` with that node
- **THEN** the button has the keyboard after the first write

#### Scenario: Two requests

- **WHEN** a handler calls `RequestFocus()` on node A, then on node B, before any write
- **THEN** B's stop has the keyboard, and A never had it

### Requirement: One traversal decides Tab, Shift+Tab, NextFocus and PreviousFocus

Tab, Shift+Tab, `NextFocus()` and `PreviousFocus()` SHALL move the keyboard by one rule on both targets: the
traversable stops of the innermost focus scope around the focused stop, in each target's tree order (Photon's
paint order, the web's document order), entering at the first stop going forward and at the last going back
when no stop of the scope has the keyboard. Past a scope's last stop (or before its first) its edge behaviour
SHALL decide: `ClosedLoop` wraps inside it, `Stop` leaves the keyboard where it is, `ParentScope` goes on in
the enclosing scope from the scope's place. At the outermost scope Photon SHALL wrap, and the web SHALL let
the browser take the keyboard out of the page. A Tab a focused control consumed (a code editor's indent)
SHALL NOT move the keyboard. On the web, content the manager does not own (an `HtmlElement`'s native
focusables, an `iframe`, a native form control) SHALL stay reachable by Tab in the browser's order.

#### Scenario: NextFocus and Tab agree

- **WHEN** a page holds buttons A, B and C, A has the keyboard, and a handler calls `NextFocus()` on A's node
- **THEN** B has the keyboard, the stop a Tab from A reaches, on the web as on Photon

#### Scenario: A closed-loop scope

- **WHEN** a `FocusScope` (by default `ClosedLoop`) holds buttons B and C between buttons A and D, and Tab is
  pressed from C
- **THEN** B has the keyboard, and D is not reached

#### Scenario: A parent-scope edge

- **WHEN** the same scope says `ParentScope`, and Tab is pressed from C
- **THEN** D has the keyboard, and Shift+Tab from B gives it to A

#### Scenario: A stop edge

- **WHEN** the same scope says `Stop`, and Tab is pressed from C
- **THEN** C keeps the keyboard

#### Scenario: The page's last control

- **WHEN** no scope is declared and Tab is pressed from the page's last control
- **THEN** on Photon the page's first control has the keyboard, and on the web the browser's own Tab takes
  the keyboard out of the page

#### Scenario: Content the manager does not own

- **WHEN** a web page holds a button A, then an `HtmlElement` whose raw markup holds an `input` and a link,
  then an `iframe`, then a native `select`, then a button B, and Tab is pressed from A repeatedly
- **THEN** the keyboard reaches the `input`, the link, the `iframe`'s content, the `select` and B in that
  order, the order the browser's own Tab takes over the same page

#### Scenario: A node that skips traversal

- **WHEN** B is wrapped in `Focus` with `skipTraversal: true`, and Tab is pressed from A
- **THEN** C has the keyboard, and `RequestFocus()` on B's node still gives B the keyboard

#### Scenario: A node that may not take the keyboard

- **WHEN** B is wrapped in `Focus` with `canRequestFocus: false`
- **THEN** Tab from A gives C the keyboard, and `RequestFocus()` on B's node moves nothing

### Requirement: A Focus around no control makes its child a stop

A `Focus` whose child holds no control SHALL make the child a stop of its own on both targets, which Tab
reaches in its tree order and a `FocusScoped` shortcut inside it answers for.

#### Scenario: A focusable panel

- **WHEN** a `Focus` wraps a box holding only text and a `FocusScoped` shortcut for Delete, and Tab reaches it
- **THEN** the box has the keyboard with its focus ring, and Delete calls the shortcut's handler

### Requirement: A focus node tells when its focus changes

Every node whose `HasFocus` or `HasPrimaryFocus` changed SHALL raise `Changed`, and the `onFocusChange` of
its `Focus` or `FocusScope` SHALL be called with `HasFocus`, outermost node first, whatever moved the
keyboard: Tab, a press, a request, the browser, or the focused control leaving the tree. A handler added or
removed while `Changed` is being raised SHALL take effect from the next change, as .NET's events do.

#### Scenario: A press

- **WHEN** a `Focus` with `onFocusChange` wraps a button, and the button is pressed with a pointer
- **THEN** `onFocusChange(true)` is called once, on the web as on Photon

#### Scenario: The focused control leaves

- **WHEN** the button that has the keyboard is no longer built
- **THEN** its node raises `Changed` and answers `HasFocus` false

#### Scenario: A handler that removes itself

- **WHEN** a node's `Changed` has handlers H1 and H2, H1 removes H2 and adds H3, and the focus changes twice
- **THEN** the first change calls H1 and H2, and the second calls H1 and H3, as .NET 10 answers for a C# event

#### Scenario: A handler added twice

- **WHEN** one handler is added to `Changed` twice, the focus changes, the handler is removed once, and the focus
  changes again
- **THEN** the handler is called twice on the first change and once on the second, as .NET 10 answers

### Requirement: Autofocus asks once per position

A `Focus` or `FocusScope` with `autofocus: true` SHALL ask for the keyboard once, when its position is first
built, and SHALL NOT take it when a stop inside its nearest scope already has it.

#### Scenario: A search field's panel opens

- **WHEN** a panel is built with a `Focus(autofocus: true)` around its first button, and is built again
- **THEN** the button has the keyboard after the first write, and a Tab away from it is not undone by the
  second build
