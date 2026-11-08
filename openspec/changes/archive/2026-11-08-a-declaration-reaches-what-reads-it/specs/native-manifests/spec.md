## ADDED Requirements

### Requirement: A capability is declared only when an API uses it

The builder SHALL offer no method for a capability no API of the SDK uses, and the generator SHALL refuse
`Use(DeviceCapability.X, …)` for one with `EQ3008` naming it. No such capability SHALL reach a manifest.

#### Scenario: The general form

- **WHEN** an app writes `builder.Capabilities.Use(DeviceCapability.Contacts, "…")`
- **THEN** the build fails with one `EQ3008` naming `Contacts`

#### Scenario: A capability an API uses

- **WHEN** an app declares `UseCamera` or `Use(DeviceCapability.Location, …)`
- **THEN** it builds

### Requirement: A fact stated on the builder reaches the phone that reads it

A fact stated on `builder.Bundle` SHALL be in the iPhone's manifest, through the partial manifest the URL schemes use.
A fact only a Mac reads SHALL NOT be written there, and the build SHALL say so once, by name.

#### Scenario: A key an iPhone reads

- **WHEN** an app states `builder.Bundle.Flag("ITSAppUsesNonExemptEncryption", false)` and is built for iOS
- **THEN** the key is in the built `Info.plist`

#### Scenario: A fact only a Mac reads

- **WHEN** an app states `Agent()` or `MinimumSystemVersion("13.0")` and is built for iOS
- **THEN** neither key is in the manifest, and the build names each once
