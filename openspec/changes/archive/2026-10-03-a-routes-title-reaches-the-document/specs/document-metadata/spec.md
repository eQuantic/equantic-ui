## ADDED Requirements

### Requirement: A document's title and description follow the page, its route, then the app

A served document's title and description SHALL be the page's own (`IHandleMetadata`) where it
gives them, then its route's (`[Page(Route, Title, Description)]` or `MapPage<T>(route, title)`),
then the app's (`SetTitle`, the shell's default metadata). A full load and a client navigation to the
same route SHALL answer the same title and description, and a page declared at two routes SHALL be
titled by the route it was reached at.

#### Scenario: A route's title on a full load and a navigation

- **WHEN** the app's title is `The App` and a page is declared `[Page("/titled", Title = "Parity")]`
  with no `IHandleMetadata`
- **THEN** `/titled` serves `<title>Parity</title>`, and a client navigation to it answers the title
  `Parity`

#### Scenario: The page's own words win

- **WHEN** a page declared `[Page("/spoken", Title = "Static")]` writes `seo.Title("Dynamic")`
- **THEN** both doors answer `Dynamic`

#### Scenario: Two routes, two titles

- **WHEN** one page is declared at `/orders` with `Title = "Orders"` and at `/orders/admin` with
  `Title = "Orders\nAdmin"`
- **THEN** each route serves its own title

### Requirement: The client's configuration is JSON

The configuration a document hands the client (`window.__EQ_CONFIG`) SHALL be written by a JSON
serializer that escapes every code unit a script element cannot carry raw, and it SHALL carry every
string as written.

#### Scenario: A title that would break the script

- **WHEN** a route's title holds a line break, and another's is `</script><b>x`
- **THEN** the served configuration parses as JSON, each route's title reads back as declared, and
  the document holds no `</script><b>x` outside an escape
