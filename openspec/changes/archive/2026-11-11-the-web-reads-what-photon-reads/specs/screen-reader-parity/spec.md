## ADDED Requirements

### Requirement: A closed tooltip is out of the reading order

A tooltip that is not showing SHALL be hidden from assistive technology on every target, and SHALL stay
its trigger's description. It SHALL be exposed while it shows, on hover and on keyboard focus.

#### Scenario: A tooltip at rest on the web

- **WHEN** a page holds a `Tooltip` over a button, neither hovered nor focused
- **THEN** a reader walking the page meets the button and not the tooltip's text as a line of its own,
  and hears the tooltip's text as the button's description

#### Scenario: A tooltip on Photon

- **WHEN** the same tooltip is realized on Photon
- **THEN** a reader hears nothing of it until it shows, as on the web

### Requirement: A field's message is announced once and read once

A text field's helper or error line SHALL be a live region on every target, present from the first frame,
so a message that appears in it is announced once. A reader walking the page SHALL meet the message once,
and the field SHALL still be described by it.

#### Scenario: An error appears

- **WHEN** a `TextInput` that held no message is given `error: "Required"`
- **THEN** its message line, a live region on the web and on Photon, announces "Required" once

#### Scenario: A field with a message on the web

- **WHEN** a page holds a `TextInput` with `error: "Required"`
- **THEN** a reader walking the page meets "Required" once, in the message line, and the field is
  described by it
