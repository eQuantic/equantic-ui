# content-security Specification

## Purpose
What the SDK writes so that a Content-Security-Policy can admit the SDK's own scripts and styles and
refuse everything else: the request's nonce on every element the shell writes, and the policy
itself, stated in C# and completed by the SDK with what its own features need (#306).

## Requirements

### Requirement: Every script and style the shell writes carries the request's nonce

The server SHALL make one nonce for each request that serves a page, 128 bits from a
cryptographic random source, and SHALL write it as the `nonce` attribute of every `<script>` and
every `<style>` element the shell writes: the template's own, the app's CSS, every head tag, and
every asset a page declares. Two requests SHALL NOT carry the same nonce. An element that is not a
script or a style SHALL NOT carry it. A head element SHALL be given to the shell as an `HtmlTag`,
so the shell writes its attributes.

#### Scenario: A page with GTM, the app's CSS and a head script

- **WHEN** an app with `UseGtm(...).WithConsent()`, `AddStyles`, `AddScript` and an inline head
  tag serves a page, in Production or in Development, rendered on the server or not
- **THEN** every `<script>` and `<style>` start tag of the document carries one and the same nonce

#### Scenario: A page's own assets

- **WHEN** a page declares an inline script and an inline style through `IRequireAssets`
- **THEN** both are written with the request's nonce

#### Scenario: Two requests

- **WHEN** the same page is requested twice
- **THEN** the two documents carry different nonces, each 16 bytes in base64
