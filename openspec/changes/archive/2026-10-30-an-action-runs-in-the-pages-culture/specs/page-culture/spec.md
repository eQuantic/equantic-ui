## ADDED Requirements

### Requirement: A switch asks for no catalog the build did not write

The page SHALL name the catalogs the build wrote, and a culture switch in the browser SHALL walk the exact
culture, its parents and `neutral`, asking only for those. A host with a catalog loader of its own SHALL
answer for itself.

#### Scenario: The app's neutral language

- **WHEN** an app whose build wrote `neutral`, `pt-BR` and `pt-PT` switches to `en`
- **THEN** the browser asks for `neutral.json` and for no `en.json`

#### Scenario: A region whose language has a catalog

- **WHEN** an app whose build wrote `neutral` and `sv` switches to `sv-FI`
- **THEN** the browser asks for `sv.json` alone

#### Scenario: An app with no catalog

- **WHEN** an app with no catalog switches culture
- **THEN** the browser asks for no catalog, and the formats still follow the switch
