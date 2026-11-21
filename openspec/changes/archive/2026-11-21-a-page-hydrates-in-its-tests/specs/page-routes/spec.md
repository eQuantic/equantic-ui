## ADDED Requirements

### Requirement: The browser's router and the server choose the same page

The browser SHALL compare the page the server drew for a URL (the document's configuration, and a client
navigation's payload, which SHALL name it as `page`) with the page its router resolves for that URL, at load and
after each client navigation. In development a different page SHALL be an error in the console naming both,
the path, and the documentation; production SHALL say nothing. A router that resolves no page SHALL disagree
with nothing.

#### Scenario: A host that rewrites a path

- **WHEN** a host rewrites `/promo` to `/catalogue` on the server, the route table gives `/promo` to `PromoPage`,
  and the page is loaded in development
- **THEN** the console says "the server drew CataloguePage, the router chose PromoPage for /promo" with the
  link to the documentation

#### Scenario: The same host in production

- **WHEN** the same page is loaded in production
- **THEN** the console says nothing, and the first load's record still names both pages

#### Scenario: A navigation's payload

- **WHEN** a client navigation asks for `/probe`
- **THEN** its payload names the page the server resolved, by its module's name
