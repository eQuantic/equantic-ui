## ADDED Requirements

### Requirement: A page ships the strings its modules read

The build SHALL write, beside the catalogues, which catalogue strings each page and the layout read through
the modules they reach (`page-strings.json`), following the factory surface only through the factories a
module calls, and naming the catalogues it ships to every page (the SDK's own). A catalogue SHALL hold only
the strings a module the browser reaches reads. The server SHALL inline, in a document, the strings of its
page and its layout and every catalogue shipped whole, and a navigation's payload SHALL carry the next
page's. A page the index does not name SHALL get the whole catalogue. A key the browser was not handed
SHALL be fetched with the rest of its catalogue before it is reported missing.

#### Scenario: A back office's string on the shop's page

- **WHEN** an app's home page reads `Strings.Cakes` and places a component that reads `Strings.Fresh`, and
  its admin page reads `Strings.Orders`
- **THEN** the index names `Strings/Cakes` and `Strings/Fresh` for the home page and `Strings/Orders` for the
  admin page, and the home page's document carries no `Strings/Orders`

#### Scenario: A component composed through the factory surface

- **WHEN** the home page calls `AppUI.Badge()`, and the surface also holds `AppUI.Report()`, whose component
  reads `Strings.Orders`
- **THEN** the home page's strings are `Strings/Cakes` and `Strings/Fresh`

#### Scenario: A string only server code reads

- **WHEN** a service no component names reads `Strings.Receipt`
- **THEN** no catalogue the browser receives holds `Strings/Receipt`

#### Scenario: A page the index does not know

- **WHEN** the server serves a page the index does not name
- **THEN** its document carries every string of the catalogue
