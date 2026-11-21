## ADDED Requirements

### Requirement: The browser holds the arm its window resolves once a page has hydrated

A served page SHALL carry every arm of an adaptive node, each behind its media gate, and SHALL hydrate so.
Once it has hydrated, the browser SHALL hold the one arm the window's width resolves at the node's
thresholds, as Photon does: the other arms SHALL leave the document and their components SHALL unmount, and
a window crossing a threshold SHALL render the page again with the arm it then shows. A page drawn in the
browser from the start SHALL hold one arm from its first render. The width SHALL be read on the side of each
threshold that the threshold's media query says. The arm's box SHALL be keyed by its index, so the render
after hydration keeps the DOM of the arm on screen.

#### Scenario: One control per arm after hydration

- **WHEN** a page whose adaptive node holds a button in its compact arm and one in its expanded arm hydrates
  in a window 1200 px wide
- **THEN** the document holds the expanded arm's button alone, the same DOM element hydration adopted

#### Scenario: A window crossing the threshold

- **WHEN** the window then narrows to 400 px
- **THEN** the page renders again and the document holds the compact arm alone

#### Scenario: A zoomed window at a threshold

- **WHEN** a window whose media query for 840 px does not match reports an `innerWidth` of 840
- **THEN** the arm held is the one below 840

### Requirement: An arm stands at its own slot on every target

The element tree SHALL hold an adaptive node's compact arm at slot 0, its medium arm at slot 1 and its
expanded arm at slot 2, whether it holds every arm or one, on every target. Where it holds one arm, an arm of
the same type and key as the one it showed SHALL take that position when the window crosses a threshold.

#### Scenario: The expanded arm on Photon

- **WHEN** Photon builds a node with a compact and an expanded arm in a window 914 dp wide
- **THEN** the node's position holds nothing at slot 0 and the expanded arm at slot 2

#### Scenario: Two arms of one type

- **WHEN** both arms are the same stateful component, pressed once at 400 dp, and the window widens to 914 dp
- **THEN** the expanded arm's position is the one that was pressed, and its count is 1
