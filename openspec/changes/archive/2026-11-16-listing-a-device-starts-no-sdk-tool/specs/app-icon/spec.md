## ADDED Requirements

### Requirement: Listing a device or deploying starts no tool of the SDK's

The native SDK SHALL write the Android icon only on a path that collects Android resources, and only
when the icon is out of date, so the Android workload's device listing starts no tool and builds no
project of the SDK's, a deploy after a build starts none either, and both work on a source tree.

#### Scenario: A device listing

- **WHEN** `dotnet run --list-devices` runs for an Android app with an icon
- **THEN** no eqicon is built or started, and on a source tree the listing works with no
  `DOTNET_HOST_PATH`

#### Scenario: A deploy after a build

- **WHEN** `dotnet run --device <id>` deploys an app whose icon has not changed since its build
- **THEN** the icon target is skipped as up to date and no eqicon is started

#### Scenario: A cold build

- **WHEN** an Android app with an icon builds from no generated assets
- **THEN** the five mipmaps exist before Android collects its resources, with no XA2001, and the
  package carries them
