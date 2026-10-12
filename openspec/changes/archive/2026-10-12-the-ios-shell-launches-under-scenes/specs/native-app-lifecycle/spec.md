## ADDED Requirements

### Requirement: An iOS app runs under the scene life cycle

The iOS shell SHALL make its window in a `UIWindowSceneDelegate`, on the `UIWindowScene` the system
connects, and the app delegate SHALL own no window. The SDK SHALL declare the scene manifest, with one
scene, in the Info.plist of every iOS app it builds, so that an app built with the iOS 27 SDK launches.

#### Scenario: The manifest is in the app

- **WHEN** the Wallet sample is built for the iOS simulator
- **THEN** its Info.plist has `UIApplicationSceneManifest` with `UIApplicationSupportsMultipleScenes`
  false

#### Scenario: Built on Xcode 27, launched on iOS 27

- **WHEN** the Wallet sample is built on Xcode 27 and launched on an iOS 27 simulator with
  `Photon__MaxFrames` 60
- **THEN** it presents 60 frames

#### Scenario: No configuration error at launch

- **WHEN** the Wallet sample launches on an iOS simulator
- **THEN** UIKit logs no error about a missing scene configuration

### Requirement: An iOS app keeps its screen while it is away

The iOS shell SHALL keep the app's screen, and with it every component's state, for the life of the
process, across scenes the system disconnects and connects again. In the background the shell SHALL
present no frame, and coming back SHALL present the same tree again.

#### Scenario: Home and back

- **WHEN** the Wallet sample has its Income filter selected, Home is pressed and the app is opened
  again
- **THEN** it shows the Income filter selected, and a touch on Bills selects Bills
