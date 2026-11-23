## ADDED Requirements

### Requirement: A served page is hydrated in a test

`HydrationAssert.PageAsync(factory, path)` SHALL serve the app on a port of the machine, load the page at
`path` as a browser loads it, run the page's scripts against the server's markup, and throw a
`HydrationException` whose message names, the most telling first, each thing that went wrong: an error the page
reported, the browser's router choosing another page than the server drew, a page the browser drew over, a
missing string, and each text, attribute or element the browser drew differently from the server. A page that
hydrates as the server drew it SHALL pass. It SHALL run under the embedded bun the app was built with, which the
package's build targets find from the app the test project references, so a test project installs nothing
else.

#### Scenario: A page that hydrates as the server drew it

- **WHEN** a test asks for the fixture site's home page, a heading and a counter
- **THEN** the report passes, names `HomePage` as the page the server drew and the router chose, and holds no
  difference

#### Scenario: A label the browser draws blank

- **WHEN** a page's prefetch keeps its title in a static field, which does not cross, and the server draws
  "Our cakes"
- **THEN** the test fails, saying the text was 'Our cakes' on the server and '' in the browser

#### Scenario: An icon size the browser refuses

- **WHEN** a page sets an icon's size to 22 once it is mounted, which happens in the browser alone
- **THEN** the test fails, saying the page reported an error that names "Icon size 22"

#### Scenario: A string kept in the first request's language

- **WHEN** a page caches a resource string in a static field on its first build, and the server served the page
  in pt-BR first
- **THEN** a test of the page in English fails, saying the text was 'Olá' on the server and 'Hello' in the
  browser

#### Scenario: A path the router gives another page

- **WHEN** the server rewrites `/promo` to the catalogue, and the route table gives `/promo` to `PromoPage`
- **THEN** the test fails, saying the server drew `CataloguePage` and the browser's router chose `PromoPage` for
  `/promo`
