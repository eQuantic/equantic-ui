# page-routes Specification

## Purpose
Which page a route serves: the one its `[Page]` attribute or its `MapPage<T>` declaration names, on
the server and in what a client navigation receives.

## Requirements

### Requirement: A route renders the page declared for it

A route declared by `[Page]` or by `MapPage<T>` SHALL render, on the server and in the state a client
navigation receives, the page it was declared for, identified by its type, so that two pages with one
simple name in two namespaces each answer at their own route.

#### Scenario: Two pages named Dashboard

- **WHEN** `Admin.Dashboard` is routed at `/same-named/admin` and `Shop.Dashboard` at
  `/same-named/shop`, with SSR on, and each route is requested
- **THEN** each answers with its own page's markup and not the other's

### Requirement: A page answers HEAD as it answers GET

A page route, declared by `[Page]` or by `MapPage<T>`, and the modules the server serves under
`/_equantic/` SHALL answer a HEAD request with the status and the headers they answer a GET with,
and SHALL send no body. A HEAD for a path nothing serves SHALL be answered as the GET is, by the
fallback.

#### Scenario: A page route

- **WHEN** a HEAD request asks for a route a `[Page]` declares, on Kestrel
- **THEN** it is answered 200 with the GET's content type and no body

#### Scenario: A route MapPage declares

- **WHEN** a HEAD request asks for a route `MapPage<T>` declares
- **THEN** it is answered 200 with the GET's content type and no body

#### Scenario: A module

- **WHEN** a HEAD request asks for `/_equantic/runtime.js`, or for an app's module under `/_equantic/`
- **THEN** it is answered 200 with the GET's content type and cache headers, and no body

#### Scenario: No page

- **WHEN** a HEAD request asks for a path no page declares
- **THEN** it is answered 404

### Requirement: A page's authorization guards its route

A route declared by `[Page]` or by `MapPage<T>` SHALL carry the page's `[Authorize]` and
`[AllowAnonymous]` as endpoint metadata, so the app's authorization decides before the page is built:
an anonymous full load SHALL be challenged by the default scheme, a signed-in visitor without the
policy SHALL get 403, and the page's `IServerPrefetch` SHALL NOT run for a refused request.

#### Scenario: An anonymous full load

- **WHEN** an anonymous visitor loads a page marked `[Authorize(Policy = "Backoffice")]`
- **THEN** the default scheme challenges (a redirect to sign in), and the page's prefetched fields are
  not in the response

#### Scenario: A visitor without the policy

- **WHEN** a signed-in visitor without the policy loads it
- **THEN** the answer is 403

#### Scenario: A route declared in Program.cs

- **WHEN** the same page is routed with `MapPage<T>`
- **THEN** that route carries the same requirement

### Requirement: A refused client navigation is a 401 or a 403

A client navigation (`X-EQ-Navigate`) into a page the visitor may not open SHALL be answered 401 when
the visitor is not signed in and 403 when they lack the policy, marked with `X-EQ-Navigate`, and the
router SHALL load the route in full instead of rendering the page.

#### Scenario: An anonymous navigation

- **WHEN** an anonymous visitor follows a link into a page that requires authorization
- **THEN** the navigation's request is answered 401 with the header, and the router loads the route in
  full, which the server challenges

### Requirement: The SDK's own code serves under a fallback policy

The runtime and the page modules SHALL allow anonymous access, so a page marked `[AllowAnonymous]`
serves and comes alive under an app-wide `FallbackPolicy`.

#### Scenario: A sign-in page under a fallback policy

- **WHEN** an app requires an authenticated user by fallback policy and an anonymous visitor loads a
  page marked `[AllowAnonymous]`
- **THEN** the page and `/_equantic/runtime.js` are answered 200

#### Scenario: A name that is not a file name

- **WHEN** an asset route is asked for a name with a separator in it, such as `..\..\secret`
- **THEN** it answers 404 and serves nothing outside its own directory

### Requirement: A registered error page is asked as its own route asks

The 404 and 500 pages an app registers SHALL be drawn in place of a route only for a visitor their own
route would serve: a page that names no requirement SHALL be under the app's `FallbackPolicy`, the
request SHALL be the policy's resource, and a page the visitor may not see SHALL NOT be drawn, nor its
`IServerPrefetch` run.

#### Scenario: A plain 500 page under a fallback policy

- **WHEN** an app requires an authenticated user by fallback policy, and a page marked
  `[AllowAnonymous]` fails for an anonymous visitor
- **THEN** the app's 500 page, which names no requirement, is not drawn, and its prefetched fields are
  not in the response

#### Scenario: A 404 page whose policy reads the request

- **WHEN** the app's 404 page requires a policy that reads the request, and a visitor that policy
  allows asks for a URL that matches nothing
- **THEN** the answer is 404, with the page drawn
