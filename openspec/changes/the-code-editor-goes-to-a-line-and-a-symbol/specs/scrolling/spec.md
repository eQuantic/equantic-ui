## ADDED Requirements

### Requirement: A scroll view goes where the app asks, once per request

A `ScrollView` whose `Request` carries a version it has not honoured SHALL scroll to the request's offset,
clamped to what its content can scroll, on the web and on Photon, and SHALL report the offset it went to
through `OnScrolled`. A request it has honoured SHALL move nothing, however often the tree is built again,
so a reader's own scrolling after it stays.

#### Scenario: A request after the first frame

- **WHEN** a scroll view over content 2000 high in a viewport 300 high is drawn at offset 0, and built again
  with a request of version 1 for offset 900
- **THEN** it shows the content from 900, and `OnScrolled` reported 900

#### Scenario: A request already honoured

- **WHEN** that view is scrolled by the reader to 400 and built again with the same request
- **THEN** it stays at 400

#### Scenario: Past the end

- **WHEN** it is built with a request of version 2 for offset 5000
- **THEN** it shows the content from 1700, the end
