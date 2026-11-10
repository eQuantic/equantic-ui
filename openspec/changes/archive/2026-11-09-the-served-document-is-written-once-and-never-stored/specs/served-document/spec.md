## ADDED Requirements

### Requirement: A value the document writes is never read as template

The server SHALL fill each placeholder of the document's template once, from the template alone: text a
variable wrote, the server-rendered markup and what a visitor typed in it included, SHALL be written as
it came, and a `{{Name}}` inside it SHALL NOT be replaced. A placeholder of the template with no
variable SHALL stay as it is.

#### Scenario: A placeholder in a page's own text

- **WHEN** a page's server data holds text that reads like one of the template's placeholders, drawn in a
  `TextEntry`
- **THEN** the served field's `value` is that text as it came, and the page's state is written in its
  script and nowhere else

#### Scenario: A value holding other placeholders

- **WHEN** a template `<p>{{A}}</p><i title="{{B}}"></i>{{C}}{{Missing}}` is rendered with `A` =
  `{{B}}{{C}}`, `B` = `{{A}}` and `C` = `c`
- **THEN** it writes `<p>{{B}}{{C}}</p><i title="{{A}}"></i>c{{Missing}}`

### Requirement: A page's answer is never stored

Every answer of a page's route SHALL be sent with `Cache-Control: no-store`: the document, whether the
page is rendered on the server or not, the not-found and the error documents, and the page's state a
client navigation asks for.

#### Scenario: A page rendered on the server and one that is not

- **WHEN** a page is requested from an app with server rendering on, and from one with it off
- **THEN** both documents are sent `no-store`, where they were sent with no `Cache-Control`

#### Scenario: A path that matches nothing

- **WHEN** a path no page declares is requested
- **THEN** the not-found document is sent `no-store`

#### Scenario: A navigation's state

- **WHEN** a page's route is asked for its state by a client navigation
- **THEN** the JSON is sent `no-store`
