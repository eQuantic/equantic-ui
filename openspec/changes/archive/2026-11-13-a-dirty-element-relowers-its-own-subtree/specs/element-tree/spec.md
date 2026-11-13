## ADDED Requirements

### Requirement: A dirty element in the browser lowers its own subtree

In the browser, a render that only the page's element owners asked for (a nested component's state change)
SHALL lower only the components that built, each with what it was lowered with and with what its parents did
to its root done again, and SHALL patch only the DOM those components drew. The DOM it leaves SHALL be the DOM
a whole-page lowering leaves. A component whose lowering a parent read or walked into SHALL be lowered with the
nearest component above it that nothing reached into, and the page SHALL lower whole when none is left. The
shortcuts the components lowered declared SHALL replace the ones they declared before, and every other
binding SHALL stay.

#### Scenario: One component changes its state

- **WHEN** a page holds two counters and one of them changes its state
- **THEN** one component is lowered, and the page's DOM is the DOM a whole lowering of the page leaves

#### Scenario: A parent acted on the component's root

- **WHEN** a counter inside a presence changes its state
- **THEN** one component is lowered, its element carries the presence's motion class and exit marker, and
  the DOM is the DOM a whole lowering leaves

#### Scenario: A parent read what the component lowered to

- **WHEN** a counter wrapped by a shortcut changes its state
- **THEN** the page lowers whole, and the DOM is the DOM a whole lowering leaves

#### Scenario: A component and one inside it change their state

- **WHEN** a component and a counter it holds both change their state before the render
- **THEN** the outer component's subtree is lowered once, both show their new state, and the DOM is the DOM
  a whole lowering leaves

#### Scenario: Shortcuts outside the lowered components

- **WHEN** a page declares a shortcut beside a counter, and the counter changes its state
- **THEN** one component is lowered and the shortcut is still live
