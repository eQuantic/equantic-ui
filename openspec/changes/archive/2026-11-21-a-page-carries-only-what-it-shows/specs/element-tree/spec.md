## MODIFIED Requirements

### Requirement: Every arm of an adaptive node is a position on the web

An owner the server makes, and an owner of the browser's until the page it draws has hydrated, SHALL hold
every arm an adaptive node declares, compact at slot 0, medium at slot 1 and expanded at slot 2, rather than
the arm a window resolves, since the served page draws every arm behind its own media gate. An owner that
draws one arm (Photon's, and the browser's once the page has hydrated) SHALL resolve it, and hold it at its
own slot. An owner that lays a tree out once and draws it no more SHALL hold every arm.

#### Scenario: The arm on screen is held at any width

- **WHEN** a page with an adaptive node holding a stateful component in each arm is drawn with every arm,
  and the window crosses the node's threshold
- **THEN** the component in the arm now shown keeps its instance and answers a press without a build

## ADDED Requirements

### Requirement: A global key carries its position across parents

A node that carries a `GlobalKey` and is placed, in one build, under another parent than the one its
position stood under SHALL take that position along, with every stateful instance below it, whether the
build reaches the new place before or after the old one; no component below it SHALL unmount or mount for
the move. A component moved under another inherited value SHALL read the value above its new place and
SHALL hear that its dependencies changed. A position of a key that no node takes in the build SHALL leave
at the frame's end. Where one arm is held, a key placed at two positions in one frame SHALL fill both,
SHALL start one of them again, and SHALL tell the developer; where every arm is held, each arm's position of
the key SHALL be its own.

#### Scenario: A screen in both arms of the drawer template

- **WHEN** the drawer shell of `dotnet new equantic-native`, its activity screen keyed, counts to 2 at 400 dp,
  and the window widens to 1000 dp and narrows back to 400 dp
- **THEN** the activity screen is the same instance at each width, and its count is 2

#### Scenario: A key moving to a branch the build reaches first

- **WHEN** a keyed counter pressed once moves from the second of two columns in a row to the first
- **THEN** it keeps its instance and its count, it neither unmounts nor mounts, and the second column holds
  nothing

#### Scenario: A key placed twice

- **WHEN** one frame places two nodes of one key in a row where one arm is held
- **THEN** both slots hold a position, each with its own instance, and the developer is told the key is
  placed at two positions in one frame

#### Scenario: The same on the browser's engine

- **WHEN** the screen case runs on the transpiled engine the served runtime exports
- **THEN** its trace equals the .NET engine's
