## ADDED Requirements

### Requirement: Hydration adopts the server's element with the client's attributes and text

When hydration finds the server's markup with the structure of the client's tree, it SHALL keep each
element the server wrote and SHALL make it carry the client's attributes: an attribute the client's
tree names SHALL have the client's value, and one the client's tree does not name SHALL be removed,
except a framework marker (`data-eq-*`), which SHALL stay, and except a field's `value` or
`checked` the person already changed before the runtime loaded, which SHALL stay as the person left
it. A text node SHALL carry the client's text. The first render after hydration SHALL then find nothing the DOM does not already say.

#### Scenario: A button the server drew disabled for a capability it does not have

- **WHEN** the server renders a button disabled because the capability it gates on is unavailable
  there, and the browser's tree, built with the real capability, says it is enabled
- **THEN** after hydration the same button element is enabled, and its listeners are attached

#### Scenario: An attribute only the client knows

- **WHEN** the client's tree names an attribute the server did not write, a link's `href` or an
  `aria-current`
- **THEN** after hydration the element carries it with the client's value

#### Scenario: A text the client writes otherwise

- **WHEN** a text node the server wrote holds other text than the client's tree
- **THEN** after hydration it holds the client's

#### Scenario: A framework marker the client does not repeat

- **WHEN** the server wrote a `data-eq-*` marker on an element and the client's tree does not name it
- **THEN** the marker is still on the element after hydration

#### Scenario: A field the person used before the runtime loaded

- **WHEN** a person types in a served field, or ticks a served box, before hydration, and the
  client's tree gives that field another value
- **THEN** after hydration the field holds what the person typed or ticked, while a field nobody
  used takes the client's value
