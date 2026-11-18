## ADDED Requirements

### Requirement: A listener hears the raw pointer over any subtree

A `Listener` SHALL report the pointer over the node it wraps to `OnPointerDown`, `OnPointerMove`,
`OnPointerUp`, `OnPointerCancel`, `OnPointerHover` and `OnPointerScroll`, each with a `PointerEvent` whose
`Position` is relative to the listener's top-left corner, whose `Kind` is the device, whose `Buttons` are the
buttons held (none on an up and on a hover), and whose `Modifiers` are the keys held. A pointer that went down
on a listener SHALL be reported to it until its up or its cancel, wherever it moves. Hover SHALL be reported
for a mouse or a pen moving with nothing pressed, and never for a finger. A scroll SHALL carry its
`ScrollDelta` in dp toward the content's end. A listener SHALL take nothing from what it wraps: a control
inside it still presses and a scroll view inside it still scrolls.

#### Scenario: A press, a drag outside and a release

- **WHEN** a mouse presses the primary button 10 px right of a listener's left edge and 6 px below its top,
  moves 200 px to the right, past the listener's edge, and releases
- **THEN** the listener hears a down at (10, 6) with the primary button, a move at (210, 6) with the primary
  button, and an up at (210, 6) with no button

#### Scenario: A button inside a listener

- **WHEN** a `Pressable` inside a listener is clicked
- **THEN** the listener hears the down and the up, and the `Pressable` is pressed

#### Scenario: A finger does not hover

- **WHEN** a finger moves over a listener without touching it first, which no touch screen reports, and a
  mouse then moves over it with nothing pressed
- **THEN** the listener hears one hover, the mouse's

### Requirement: An app chooses how a listener is hit

`Listener.Behavior` SHALL decide where the listener hears a pointer, as Flutter's `HitTestBehavior` does.
`DeferToChild` SHALL hear only where its child is hit: where the pointer lands on something the child draws (a
text, an icon, an image, a vector, a canvas, a box with a fill, a border or a shadow) or on something in it
that answers the pointer (a control, a field, a scroll view, an `Opaque` listener). `Opaque` SHALL hear
anywhere in its box and SHALL hide the listeners behind it. `Translucent` SHALL hear anywhere in its box and
SHALL let the listeners behind it hear as well. Listeners that hear the same pointer SHALL be told innermost
and topmost first. The rule SHALL be the same on the web and on Photon, and both targets SHALL run the same
cases.

#### Scenario: The gap of a column

- **WHEN** a listener wraps a column of two texts with a gap between them, and the pointer goes down in the
  gap
- **THEN** a `DeferToChild` listener hears nothing, and an `Opaque` or a `Translucent` one hears the down

#### Scenario: Two listeners in a stack

- **WHEN** two listeners fill the same stack, the upper one wrapping a box that draws nothing, and the
  pointer goes down on them
- **THEN** with the upper one `Translucent` both hear the down, upper first; with it `Opaque` only the upper
  one hears it; and with it `DeferToChild` only the lower one hears it

### Requirement: Photon hears every phase its shells deliver

The Photon host SHALL report a listener's down, move, up and cancel from the presses, moves and cancels its
shells deliver, its hover from a mouse's moves, its scroll from `ScrollBy`, and a secondary click from
`ContextRequest` as a down and an up with the secondary button, with the same rule and the same positions the
web reports. A frame with no listener SHALL record nothing for it.

#### Scenario: A secondary click on Photon

- **WHEN** a shell reports a secondary click over a listener
- **THEN** the listener hears a down with the secondary button, then an up with no button, at the click's
  point in its box
