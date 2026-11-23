## ADDED Requirements

### Requirement: An iOS screen follows the system's mode and contrast on every supported version

From iOS 17, the iOS shell's screen SHALL register for the light or dark mode and the contrast traits and
re-render when either changes, unless the app fixed the mode. On iOS 15 and 16 it SHALL follow them through
the trait-change callback, and from 17 that callback SHALL NOT answer as well.

#### Scenario: The appearance flips on iOS 17 or later

- **WHEN** an app runs on an iOS 27 simulator and the simulator's appearance goes from light to dark
- **THEN** the screen re-renders dark, and back to light when the appearance returns

#### Scenario: The contrast is increased

- **WHEN** the simulator's Increase Contrast setting is turned on
- **THEN** the screen re-renders with the theme's higher contrast, and back when it is turned off

#### Scenario: The registration is the path from iOS 17

- **WHEN** a build of the shell without the registration runs on the iOS 27 simulator and the appearance flips
- **THEN** the screen does not re-render
