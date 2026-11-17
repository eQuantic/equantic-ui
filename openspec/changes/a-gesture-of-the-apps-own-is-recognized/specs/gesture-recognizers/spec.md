## ADDED Requirements

### Requirement: An app writes its own gesture recognizer in C#

An app SHALL write a gesture recognizer as a C# class deriving from `GestureRecognizer`, and SHALL host it over
any node with `RawGestureDetector(child, gestures)`, where each entry is a
`GestureRecognizerFactory<T>(create, initialize)`. The recognizer SHALL be offered every pointer that goes down
where its detector is hit by its `Behavior` (the rule of `HitTestBehavior`), through `IsPointerAllowed` and
`AddAllowedPointer`; it SHALL hear every later move, up and cancel of a pointer it tracks through
`HandleEvent`, with the position in its detector's box, until the pointer ends or it stops tracking it. The
same class SHALL transpile and run in the browser and run on Photon, with no line of JavaScript written by
the app.

#### Scenario: A recognizer hears its pointer past its detector's edge

- **WHEN** a recognizer whose detector is 200 wide hears a mouse press at (10, 6) in the detector's box, a move
  200 px to the right and a release
- **THEN** its `AddAllowedPointer` hears the down at (10, 6), and its `HandleEvent` hears a move at (210, 6)
  and an up at (210, 6), on the web and on Photon

#### Scenario: A pointer the recognizer does not allow

- **WHEN** a recognizer whose `IsPointerAllowed` answers false for the secondary button hears a secondary press
- **THEN** it does not join that pointer's arena, and a `Pressable` under the press presses as if the detector
  were not there

#### Scenario: The recognizer's subclass crosses to the browser

- **WHEN** eqc transpiles an app's recognizer that keeps fields, overrides `IsPointerAllowed`,
  `AddAllowedPointer`, `HandleEvent`, `AcceptGesture`, `RejectGesture` and `DidExceedDeadline`, and calls
  `StartDeadline` and `Resolve`
- **THEN** the build reports no diagnostic, and the module's twin, driven by the same sequence of events as the
  C# class, reports the same calls in the same order

### Requirement: A recognizer outlives a rebuild

A `RawGestureDetector` SHALL keep one recognizer per place in its `gestures` list for as long as its position
lives: `create` SHALL run once for a place, `initialize` SHALL run on the kept recognizer with every build that
hands the position a new detector, and a place whose factory's `T` is not the kept recognizer's type SHALL
create a new one. A recognizer in the middle of a gesture SHALL go on with the callbacks the latest build gave
it. A recognizer SHALL be dropped when its position leaves the tree.

#### Scenario: A rebuild during a long press

- **WHEN** a detector's component rebuilds three times while a finger holds it still, each build handing an
  `initialize` that sets a different `OnHeld`, and the hold then reaches its deadline
- **THEN** `create` ran once, and the `OnHeld` the third build set is the one that runs

#### Scenario: A place that changes its type

- **WHEN** a build replaces the factory at a place with one of another recognizer type
- **THEN** the next press is offered to a newly created recognizer of the new type, and the old one hears
  nothing more

### Requirement: Recognizers compete for a pointer in one arena

Every pointer SHALL have one arena, whose members are the recognizers that took the pointer and the SDK's own
gestures under it, in hit order, innermost first and, between boxes that do not contain each other, topmost
first. The arena SHALL close once the down is dispatched, and no member SHALL join after it closes. A member
that resolves `Accepted` SHALL win and every other member SHALL hear `RejectGesture`; a member that resolves
`Rejected` SHALL leave, and when one member is left it SHALL win; a member alone at the close SHALL win at the
close; at the pointer's up, with nothing decided, the FIRST member SHALL win and the rest SHALL be rejected; a
cancel SHALL reject every member. A member SHALL hear `AcceptGesture` or `RejectGesture` exactly once per
pointer. The arena SHALL be written once and run by both targets, and both SHALL run the same cases.

#### Scenario: The first to accept wins

- **WHEN** two recognizers, an inner and an outer one, join a pointer's arena and the outer one resolves
  `Accepted` on the first move
- **THEN** the outer one hears `AcceptGesture` and the inner one hears `RejectGesture`, once each

#### Scenario: The sweep gives the pointer to the first member

- **WHEN** an inner and an outer recognizer join a pointer's arena and neither resolves before the up
- **THEN** at the up the inner one hears `AcceptGesture` and the outer one `RejectGesture`

#### Scenario: The last one left

- **WHEN** of three members, two resolve `Rejected` before the up
- **THEN** the third hears `AcceptGesture` at the second rejection, before the up

#### Scenario: Both targets run the same cases

- **WHEN** the cases of `gesture-arena.fixture.json` run against the C# arena and against its transpiled twin
- **THEN** every case gives each member the same outcome, at the same step, on both

### Requirement: The SDK's gestures are members of the arena

A `Pressable`'s press, a `Draggable`, a `DragDismiss`, a `Sortable`'s pick-up, a `ContextTarget`'s finger hold,
a scroll view's pan and a table viewport's pan SHALL each join the arena of a pointer they arm for. Each SHALL
keep its own arming: a press SHALL never accept before the up and SHALL win only as the first member at the
sweep; a drag, a sort and a pan SHALL accept when they arm, a context hold when its menu is asked. A built-in
that is rejected SHALL drop what it armed, and a press that lost SHALL press nothing. Where no recognizer takes
a pointer, every built-in SHALL resolve as it did before this requirement. A field, a code surface and a sheet
surface SHALL NOT join the arena: they keep the press they take.

#### Scenario: A hold takes a button's press

- **WHEN** a finger holds a `Button` inside a detector whose recognizer accepts at 500 ms, and lifts after
  700 ms without moving
- **THEN** the recognizer hears `AcceptGesture`, and the button's `OnPressed` does not run, on the web and on
  Photon

#### Scenario: A drag that arms first takes the pointer from a recognizer

- **WHEN** a `Draggable` inside a detector whose recognizer waits 500 ms is dragged 40 px along its axis at once
- **THEN** the draggable moves and reports, and the recognizer hears `RejectGesture` before its deadline,
  whose `DidExceedDeadline` never runs

#### Scenario: Nothing changes without a recognizer

- **WHEN** a `Pressable` inside a scroll view, with no detector anywhere, is tapped, and then swiped 80 px
  along the scroll's axis
- **THEN** the tap presses it and the swipe scrolls the view and presses nothing, as before this change

### Requirement: On Photon, a scroll waits for the arena

On Photon a scroll view's pan SHALL be a member like any other: once a recognizer under the same pointer
accepts, the pan SHALL be rejected and the view SHALL not move for the rest of that pointer; and a pan that
travels past the slop first SHALL accept, rejecting the recognizers. A recognizer's deadline SHALL be counted by
the host's frame clock, and the host SHALL ask for a frame when the next deadline is due.

#### Scenario: A hold then a drag inside a scroll view, on Photon

- **WHEN** a finger presses a card inside a vertical `ScrollView` whose detector holds the sample's
  `HoldThenDrag`, the host renders frames for 600 ms without a move, and the finger then moves 80 px down and
  60 px right and lifts
- **THEN** `HoldThenDrag` reports its hold, moves with an offset of (60, 80) and a release there, and the
  scroll view's offset is where it was before the press

#### Scenario: A swipe at once scrolls, on Photon

- **WHEN** the same finger moves 80 px up within 100 ms of its press
- **THEN** the scroll view's offset moves by the travel past the slop, `HoldThenDrag` hears `RejectGesture`,
  and its hold is never reported

#### Scenario: An idle shell wakes for the deadline

- **WHEN** a finger holds a detector whose recognizer started a 500 ms deadline, and nothing else asks for a
  frame
- **THEN** the host answers that a frame is due 500 ms after the first frame that saw the press

### Requirement: On the web, the browser's scroll is a member that cannot be held

On the web the browser's own scroll SHALL join a pointer's arena as the member that accepts when the browser
starts a pan, which it announces with `pointercancel`: every other member SHALL then be rejected. Once a
recognizer has won a pointer, the runtime SHALL prevent the browser's pan for the rest of that pointer, and
SHALL prevent the text selection and the context menu the drag would start. A press that lost SHALL have the
`click` the browser fires after its up swallowed. A recognizer's deadline SHALL be counted by the document's
clock.

#### Scenario: A hold then a drag inside a scroll view, in the browser

- **WHEN** on the dashboard's gestures screen, in Chromium with touch, a finger presses a card inside the
  scrolling list, holds still for 600 ms, then travels 80 px down and 60 px right and lifts
- **THEN** the card is reported moved by (60, 80), and the list's `scrollTop` is what it was before the press

#### Scenario: A swipe at once scrolls, in the browser

- **WHEN** the same finger travels 80 px up within 100 ms of its press
- **THEN** the list scrolls, the browser fires `pointercancel`, and the card is not moved

#### Scenario: A mouse's hold then drag selects nothing

- **WHEN** a mouse presses a card's text, holds still for 600 ms, then drags 100 px right and releases
- **THEN** the card is reported moved by 100 px, and `getSelection()` holds no text

#### Scenario: A tap still presses

- **WHEN** a card's button is tapped and released within 100 ms
- **THEN** its `OnPressed` runs once, and the recognizer hears `RejectGesture`
