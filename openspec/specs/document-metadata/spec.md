# document-metadata Specification

## Purpose
What a document the server sends says about its page (its title and description, written once for a
full load and a client navigation alike), and the configuration it hands the client's boot.

## Requirements

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

### Requirement: A navigation replaces the head's metadata as a set

Every tag a document's metadata writes SHALL be marked as such, on a full load and in a navigation's
payload, and a client navigation SHALL remove the marked tags the previous page left and write the
next page's, the translation group included. A navigation to a page the server does not render SHALL
still answer the app's and the route's title and metadata. A page that answers with a status of its
own SHALL still hand a navigation its payload, marked as the answer to one, and the client SHALL apply
a marked payload whatever its status.

#### Scenario: A page the server does not render

- **WHEN** a client navigation reaches a page declared `[Page("/client-only", Title = "Client only", Description = "Drawn in the browser", DisableSsr = true)]`
- **THEN** its payload carries the title `Client only` and the description, each tag marked

#### Scenario: The translation group follows the navigation

- **WHEN** the dashboard sample navigates on the client from `/charts` to `/clock`
- **THEN** the head holds the four alternates of `/clock`, and none of `/charts`

#### Scenario: A page that answers 404

- **WHEN** a client navigation reaches a page whose `IHandleStatus` answers `404` and whose metadata
  titles it `Nothing Here`
- **THEN** the answer's status is `404`, it carries the navigation header, and its payload's title is
  `Nothing Here`
