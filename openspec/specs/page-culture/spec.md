# page-culture Specification

## Purpose
The format culture a page is in: the data the server writes from .NET's `NumberFormatInfo` and
`DateTimeFormatInfo` for the request's culture on every page, how a culture switch fetches it, what
the browser draws every number, date and calendar name from, and the invariant culture a page with no
culture installed is in.

## Requirements

### Requirement: The format culture travels with every page

The server SHALL write the request's format culture on every page it renders
(`window.__EQ_CULTURE__.format`): the number symbols, separators, group sizes, digits and patterns, and
the date patterns, separators, era, first day of the week and names the browser reads, from .NET's own
`NumberFormatInfo` and `DateTimeFormatInfo`, whether or not the app has a string to translate. Boot SHALL
install it before hydration. A culture switch with no reload SHALL fetch the same document for the
culture it switches to from `/_equantic/culture/{name}.json`, and a name .NET does not know SHALL be
not found.

#### Scenario: An app with no string catalog

- **WHEN** a page renders in pt-BR for an app that has no `.resx`
- **THEN** `__EQ_CULTURE__` carries pt-BR's format data, and the hydrated page writes `1,5` where the
  server wrote `1,5`

#### Scenario: A culture switch

- **WHEN** `/_equantic/culture/de-DE.json` and `/_equantic/culture/xx-XX.json` are requested
- **THEN** the first answers de-DE's format document and the second is not found

### Requirement: A page with no culture installed is in the invariant culture

A page or a test that has installed no culture SHALL format in the invariant culture, never in its
host's locale, and a conformance case SHALL run both sides in the culture it names, or in the invariant
one when it names none.

#### Scenario: A host whose locale is pt-BR

- **WHEN** `(1234.5).ToString("N2")` runs in the browser with no culture installed, on a host whose
  default locale is pt-BR
- **THEN** it writes `1,234.50`, the invariant culture's text

### Requirement: A standard number format is laid out from the culture's data

`N`, `F`, `C` and `P` SHALL be laid out from the culture's `NumberFormatInfo` as .NET lays them out: the
value rounded to the precision written or to the culture's own digits for the specifier, its whole part
grouped by the specifier's group sizes (`F` groups nothing), in the culture's pattern for its sign, with
the culture's currency and percent symbols. A culture whose data did not travel SHALL be laid out by
`Intl`.

#### Scenario: A culture whose own digits are not ASCII

- **WHEN** `(-1234.5).ToString("N2")` runs in ar-EG
- **THEN** it writes ASCII digits between the culture's separators, `1٬234٫50` after its negative
  sign, as .NET does, where `Intl` wrote ar-EG's own digits, `١٬٢٣٤٫٥٠`

#### Scenario: A currency's space

- **WHEN** `(-1234.5).ToString("C2")` runs in pt-BR
- **THEN** it writes `-R$ 1.234,50` with the U+0020 space of .NET's pattern, where `Intl` wrote U+00A0

#### Scenario: No precision

- **WHEN** `(1234.5).ToString("N")` runs in en-US on a server whose .NET reads its culture data from ICU
- **THEN** it writes `1,234.500`, the culture's three digits, where the browser wrote two

### Requirement: A calendar says what the culture's data says

A calendar's first day of the week and its day and month names SHALL be the format culture's own
`DateTimeFormatInfo`, the data a date's names come from: one copy, travelling with the format data. With
no culture installed they SHALL be the invariant culture's, and for a culture whose data did not travel,
`Intl`'s.

#### Scenario: A page with no culture installed

- **WHEN** a calendar renders with no culture installed, on a host whose default locale is pt-BR
- **THEN** its first day is Sunday and it names the invariant culture's days and months, as a date's
  text does

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
