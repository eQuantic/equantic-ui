## ADDED Requirements

### Requirement: The streams take requests only from the app's origins

The stream, a bind and a release SHALL be refused unless the request comes from the app's own host or
from an origin the app allows in `EQuantic:ServerActions:AllowedOrigins`, by the rule the Server Actions
apply.

#### Scenario: A bind from another site

- **WHEN** a bind or a release names an origin the app neither is nor allows
- **THEN** it is refused with 403 and nothing is bound or released

#### Scenario: An origin the app allows

- **WHEN** a bind names an origin listed in `EQuantic:ServerActions:AllowedOrigins`
- **THEN** the topic is bound

### Requirement: The app attaches its conventions to the streams' endpoints

`ServerEventsBuilder.ConfigureEndpoints` SHALL apply what the app attaches to every endpoint of the
streams, as it would to an endpoint of its own.

#### Scenario: Rate limiting

- **WHEN** an app attaches a convention with `ConfigureEndpoints`
- **THEN** the stream, the bind and the release endpoints each carry it

### Requirement: What a backplane delivers is checked before it is written

An envelope delivered by a backplane SHALL be written to no page unless its id has no line break or NUL
and its payload is one JSON value.

#### Scenario: An envelope no publisher of the app could send

- **WHEN** a backplane delivers an envelope whose id holds a line break, or whose payload is not one JSON
  value
- **THEN** no page receives it, and the next envelope it delivers arrives as before

## MODIFIED Requirements

### Requirement: Limits come from configuration and fail loudly

The heartbeat interval, the number of topics one connection may hold, the size of a payload, the
number of streams an instance holds and how long a stream lives SHALL be read from the
`EQuantic:ServerEvents` configuration section. A subscription past the topic limit SHALL be refused,
publishing a payload past the size limit SHALL throw, a stream past the instance's limit SHALL be
answered 503 before any of the app's handlers runs, and a stream SHALL end when its lifetime runs out. A
limit the connections cannot run with SHALL stop the app from starting.

#### Scenario: Too many topics

- **WHEN** `MaxTopicsPerConnection` is 2 and a page subscribes to a third topic
- **THEN** the third subscription is refused

#### Scenario: Requests sent at once

- **WHEN** `MaxTopicsPerConnection` is 2 and a page sends six subscriptions at once
- **THEN** two are bound and four are refused

#### Scenario: A payload too large

- **WHEN** a service publishes a payload larger than `MaxPayloadBytes`
- **THEN** `PublishAsync` throws, naming the topic and the limit

#### Scenario: A page that stops reading

- **WHEN** a page stops reading its stream until more than `MaxQueuedEventsPerConnection` events wait
- **THEN** the stream ends at once, even while it is blocked writing, and its topics are released

#### Scenario: A limit the connections cannot run with

- **WHEN** `HeartbeatInterval` is zero or negative, or a limit is below one
- **THEN** the app does not start, and the error names the setting and its value

#### Scenario: Too many streams

- **WHEN** `MaxConnections` is 1 and a second page opens a stream
- **THEN** the second is answered 503 with `Retry-After`

#### Scenario: A stream's lifetime

- **WHEN** a stream has been open for `MaxConnectionLifetime`
- **THEN** the server ends it and releases its topics, and the page binds them again on its next stream,
  each authorized as it stands then
