## ADDED Requirements

### Requirement: VoiceOver walks into a Photon window on the Mac and hears where the keyboard is

The macOS bridge SHALL keep one accessibility element per semantics node for as long as the node lives, found
by the id of the node's position at every read and told what the node says now, taking away a value, a pick
or an open list the node no longer has. It SHALL release an element only when its node leaves the tree, and
SHALL give a position that holds another kind of node a new element. The content view SHALL be an `AXGroup`
element that the window lists and that its elements name as their parent. Its focused element SHALL be the
element of the node that holds the keyboard, or the view itself when none does, and the bridge SHALL post
`NSAccessibilityFocusedUIElementChangedNotification` on it when the keyboard moves, once an assistive app has
read the tree.

#### Scenario: An element an assistive app holds

- **WHEN** an assistive app reads the window's group, holds one of its elements, and reads the group again
- **THEN** the element it holds still answers, and is the element the second read lists

#### Scenario: A node that changes, leaves, or becomes another kind

- **WHEN** a field's text changes, a button leaves the tree, and a button's position comes to hold a checkbox
- **THEN** the field keeps its element, which holds the new text, the button's element is gone, and the
  checkbox has an element of its own

#### Scenario: What a node stops saying

- **WHEN** a tab that was picked, open and valued is read again with none of the three
- **THEN** its element has no value, is not selected and is not expanded

#### Scenario: The window's tree and its focus

- **WHEN** an assistive app reads the PhotonDesktop window, and a press moves the keyboard to a field
- **THEN** the window lists the content's group beside its title bar's buttons, each element names the group
  as its parent, the application's focused element was the group and is now the field's element, and an
  observer of the focused element hears the change
