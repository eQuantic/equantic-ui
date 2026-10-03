## ADDED Requirements

### Requirement: A navigation replaces the head's metadata as a set

Every tag a document's metadata writes SHALL be marked as such, on a full load and in a navigation's
payload, and a client navigation SHALL remove the marked tags the previous page left and write the
next page's, the translation group included. A navigation to a page the server does not render SHALL
still answer the app's and the route's title and metadata.

#### Scenario: A page the server does not render

- **WHEN** a client navigation reaches a page declared `[Page("/client-only", Title = "Client only", Description = "Drawn in the browser", DisableSsr = true)]`
- **THEN** its payload carries the title `Client only` and the description, each tag marked

#### Scenario: The translation group follows the navigation

- **WHEN** the dashboard sample navigates on the client from `/charts` to `/clock`
- **THEN** the head holds the four alternates of `/clock`, and none of `/charts`
