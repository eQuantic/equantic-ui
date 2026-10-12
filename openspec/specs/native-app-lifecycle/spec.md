# native-app-lifecycle Specification

## Purpose
How a native app keeps its screen through what the platform does to its window: the surface taken
away and handed back, a rotation, a configuration change, and a swapchain rebuilt.

## Requirements

### Requirement: An Android app keeps its screen while it is away

The Android shell SHALL keep the app's host, and with it every component's state, for as long as the
activity lives. When Android takes the surface away (Home, another app in front, the screen turning
off), the shell SHALL release only what draws, and SHALL draw the same tree into the surface handed
back.

#### Scenario: Home and back

- **WHEN** the Wallet sample is on its Cards tab, Home is pressed and the app is opened again
- **THEN** it shows the Cards tab

#### Scenario: The screen turns off

- **WHEN** the Wallet sample is on its Cards tab and the screen is turned off and on again
- **THEN** it shows the Cards tab

### Requirement: A change the activity handles reaches the host

The generated launcher activity SHALL handle a rotation, a resize (screen size, smallest screen size,
screen layout), a hardware keyboard (keyboard, keyboard hidden), the system's dark mode and a new
density itself, so Android does not rebuild it. A dark-mode or density change SHALL reach the host, as
light or dark (unless the app fixed one) and as the scale it rasters at.

#### Scenario: Dark mode while away

- **WHEN** the Wallet sample is on its Cards tab, goes to the background, the system switches to dark
  mode and the app is opened again
- **THEN** it shows the Cards tab, in the dark theme

#### Scenario: A rotation

- **WHEN** the Wallet sample is on its Cards tab and the device turns to landscape and back
- **THEN** it shows the Cards tab

### Requirement: A rotated window is drawn upright at its own size

The Vulkan swapchain SHALL be made at the size the window reports, inside the range the surface
accepts, and SHALL declare its images upright, so the compositor turns them to the panel. A present
that answers `VK_SUBOPTIMAL_KHR` SHALL count as a frame shown; one that answers
`VK_ERROR_OUT_OF_DATE_KHR` SHALL rebuild the swapchain; any other failure SHALL be an error.

#### Scenario: Turning with the app in front

- **WHEN** the Wallet sample is in front and the device turns to landscape and back
- **THEN** the app stays alive, drawn upright and filling the window in each orientation

#### Scenario: Launched sideways

- **WHEN** the Wallet sample starts while the device is already in landscape
- **THEN** it is drawn upright and fills the window
