## ADDED Requirements

### Requirement: A page's styles are inside the policy

The policy SHALL write `style-src` admitting the request's nonce and `style-src-attr 'unsafe-inline'`.
Every style element and every stylesheet link the shell writes SHALL carry the request's nonce, and
every style element the runtime creates SHALL carry the page's nonce before it enters the document.
The runtime SHALL NOT write a style through `setAttribute('style', …)`.

#### Scenario: A stylesheet link

- **WHEN** the app adds a stylesheet with `AddStylesheet`
- **THEN** the page's `<link rel="stylesheet">` carries the request's nonce

#### Scenario: A page the server did not render

- **WHEN** the runtime creates the atomic sheet's style element on a page served with a nonce
- **THEN** the element carries that nonce

#### Scenario: The strict policy

- **WHEN** an app with `UseContentSecurityPolicy()` serves a page
- **THEN** the policy carries `style-src 'nonce-<n>'` and `style-src-attr 'unsafe-inline'`, with the nonce its scripts carry

## MODIFIED Requirements

### Requirement: Every script and style the shell writes carries the request's nonce

The server SHALL make one nonce for each request that serves a page, 128 bits from a
cryptographic random source, and SHALL write it as the `nonce` attribute of every `<script>`, every
`<style>` element and every `<link rel="stylesheet">` the shell writes: the template's own, the
app's CSS, every head tag, and every asset a page declares. Two requests SHALL NOT carry the same
nonce. An element that is not a script, a style or a stylesheet link SHALL NOT carry it. A head
element SHALL be given to the shell as an `HtmlTag`, so the shell writes its attributes.

#### Scenario: A page with GTM, the app's CSS and a head script

- **WHEN** an app with `UseGtm(...).WithConsent()`, `AddStyles`, `AddScript` and an inline head
  tag serves a page, in Production or in Development, rendered on the server or not
- **THEN** every `<script>` and `<style>` start tag and every stylesheet link of the document
  carries one and the same nonce
