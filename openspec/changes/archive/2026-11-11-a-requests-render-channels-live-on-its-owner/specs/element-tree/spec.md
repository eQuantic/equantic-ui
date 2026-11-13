## ADDED Requirements

### Requirement: A render's channels are handed to it

The web SHALL hand each render its channels as one `RenderChannels`: the atomic style sink, the
gradient sink, the face sink, the expansion scope, the density and the link policy. The server SHALL
make one for each prefetch round and one for each discovery, and SHALL hand it to `MarkupHost.Lower`
for a write-once page. An escape-hatch page SHALL be rendered with its round's channels armed by
`RenderChannels.BridgeWith`, and a write-once bridge inside it SHALL read them there, the one web
channel read from the async flow. Every `AsyncLocal` the SDK's source declares SHALL be named in a
list with the reason it is one, and the list SHALL fail on a channel it does not name and on a name
the source no longer declares.

#### Scenario: Two requests drawn at once

- **WHEN** two requests at different densities are drawn at the same time, each held in its page's
  build until the other is drawing too, for a write-once page and for an escape-hatch page
- **THEN** each page carries its own density and its own atomic rules, and none of the other's

#### Scenario: A page in a language

- **WHEN** a page lowered through channels whose link policy is a language's links to `/about`
- **THEN** its href is the language's, and the same page lowered outside a render links to `/about`

#### Scenario: A channel nobody named

- **WHEN** the source declares an `AsyncLocal` the list does not name
- **THEN** the list's test fails, naming it
