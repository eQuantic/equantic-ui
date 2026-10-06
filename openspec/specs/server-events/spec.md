# server-events Specification

## Purpose
A component hears what the server publishes: a topic the app names and types, a subscription the
server authorizes, one connection per page that the SDK keeps, and a seam wherever an app needs its
own transport, backplane, authorization or lifecycle code.

## Requirements

### Requirement: A topic names what it carries, and the payload arrives typed

A `ServerTopic<T>` SHALL name a topic and the type of its payload. A payload the server publishes to
a topic SHALL reach a subscribed component as a value of `T`, revived as a Server Action's result is,
so a record's members, an enum, a decimal, a long and a nested record read as they read in C#.

#### Scenario: A record arrives as the record

- **WHEN** a component subscribes to `new ServerTopic<Quote>("prices")` and the server publishes
  `new Quote("EQ", 10.50m, QuoteTrend.Up)`
- **THEN** the component's handler receives a `Quote` whose `Price` is the decimal 10.50 and whose
  `Trend` is `QuoteTrend.Up`, and a method the record declares runs on it

#### Scenario: The same object names the topic on both sides

- **WHEN** an app declares `static ServerTopic<RoomEvent> Room(string id) => new($"room:{id}")`, a
  component subscribes to `Room("a")` and a service publishes to `Room("a")`
- **THEN** the component receives the event, and a publish to `Room("b")` does not reach it

### Requirement: One connection per page carries every topic

A page SHALL hold at most one server-events connection, opened when its first subscription starts
and closed when its last subscription ends, and every topic the page subscribes to SHALL travel over
it.

#### Scenario: Three topics, one connection

- **WHEN** components on one page subscribe to three topics
- **THEN** the browser holds one event stream to the server, and events published to each of the
  three topics arrive

#### Scenario: The last subscription closes the connection

- **WHEN** every subscription on the page is disposed
- **THEN** the event stream is closed

### Requirement: The server authorizes every subscription by its topic

Every subscription SHALL be authorized by the server before the topic is bound to the connection.
A topic SHALL be matched against templates in ASP.NET Core's route syntax, each with a policy,
anonymous access or a delegate that receives the `HttpContext` and the template's values. A topic no
template matches SHALL be refused. A refused subscription SHALL be reported to the component that
asked, and SHALL receive nothing.

#### Scenario: A policy the user meets

- **WHEN** `room:{roomId}` requires the policy `RoomMember`, and a user the policy accepts for room
  `a` subscribes to `room:a`
- **THEN** the subscription is accepted and events published to `room:a` reach it

#### Scenario: A policy the user does not meet

- **WHEN** a user the policy refuses subscribes to `room:a`
- **THEN** the component's refusal handler is called with the topic, and an event published to
  `room:a` does not reach that page

#### Scenario: A delegate reads the template's values

- **WHEN** `room:{roomId}:participant:{participantId}` is authorized by a delegate, and a page
  subscribes to `room:a:participant:7`
- **THEN** the delegate receives the request's `HttpContext` with `roomId` "a" and `participantId`
  "7", and its answer decides

#### Scenario: A topic no template matches

- **WHEN** a page subscribes to a topic that matches no configured template
- **THEN** the subscription is refused

#### Scenario: A fallback policy that requires a user

- **WHEN** the app's fallback authorization policy requires a signed-in user, and an anonymous page
  subscribes to a topic the app lets anyone hear and to one that requires a user
- **THEN** the first is bound and the second is refused: the events endpoints are open, and each
  topic's own rule decides

#### Scenario: A topic authorized after its stream ended

- **WHEN** a subscription is still being authorized when its page's stream ends
- **THEN** the topic is bound to nothing, and no handler hears it subscribed

### Requirement: A client only listens

No request a client sends SHALL publish to a topic. The requests a client sends SHALL only bind or
release topics on a connection the server issued, named by the unguessable id the stream handed to
its page.

#### Scenario: A connection the server never issued

- **WHEN** a request names a connection id the server never issued
- **THEN** the request is refused and no connection changes

### Requirement: Ending a subscription stops delivery

Disposing the `IDisposable` a subscription returned SHALL stop delivery to its handler and release
the topic on the server when no other subscription on the page holds it.

#### Scenario: Disposed in OnUnmount

- **WHEN** a component disposes its subscription in `OnUnmount` and is unmounted, and the server then
  publishes to the topic
- **THEN** the component's handler is not called

### Requirement: The connection's state is observable, and a reconnection subscribes again

The capability SHALL expose the connection's state (connecting, connected, reconnecting,
disconnected) and SHALL report each change to the components that watch it, the change carrying the
id of the last event the page received. When the connection drops, the runtime SHALL reconnect by
itself and SHALL subscribe every live topic again, authorized again, and SHALL report connected once
those topics are bound.

#### Scenario: The server restarts

- **WHEN** a page holds two subscriptions, the connection drops and the server comes back
- **THEN** the page is told reconnecting and then connected, the change names the id of the last
  event it received, and events published after it to both topics arrive

#### Scenario: The app stops

- **WHEN** the app stops gracefully while a page holds a stream
- **THEN** the stream ends rather than holding the shutdown, and the page reconnects

### Requirement: An event published on one instance reaches subscribers on every instance

Publishing SHALL go through a backplane, in memory by default and replaceable by the app, and every
server instance sharing the backplane SHALL deliver an event to its own subscribers of the topic.

#### Scenario: Two instances

- **WHEN** two server instances share a backplane, a page is connected to the second, and a service
  on the first publishes to a topic the page subscribes to
- **THEN** the page receives the event

### Requirement: The app hears the connection's lifecycle on the server

An app SHALL be able to register handlers that hear a connection open and close and a topic
subscribed and released, with the `HttpContext` of the request and the topic's template values.

#### Scenario: Presence from the hooks

- **WHEN** a page subscribes to `room:a` and then closes
- **THEN** the handler hears the subscription with `roomId` "a", and then the topic released and the
  connection closed

### Requirement: Server rendering subscribes to nothing

During server rendering the capability SHALL be absent: a subscription SHALL do nothing and SHALL
open nothing, and the browser SHALL subscribe once the page runs there.

#### Scenario: A page rendered on the server

- **WHEN** a component that subscribes in `OnMount` is rendered on the server and then hydrated
- **THEN** the server opens no connection for it, and the browser subscribes after hydration

### Requirement: Limits come from configuration and fail loudly

The heartbeat interval, the number of topics one connection may hold and the size of a payload SHALL
be read from the `EQuantic:ServerEvents` configuration section. A subscription past the topic limit
SHALL be refused, and publishing a payload past the size limit SHALL throw.

#### Scenario: Too many topics

- **WHEN** `MaxTopicsPerConnection` is 2 and a page subscribes to a third topic
- **THEN** the third subscription is refused

#### Scenario: Requests sent at once

- **WHEN** `MaxTopicsPerConnection` is 2 and a page sends six subscriptions at once
- **THEN** two are bound and four are refused

#### Scenario: A payload too large

- **WHEN** a service publishes a payload larger than `MaxPayloadBytes`
- **THEN** `PublishAsync` throws, naming the topic and the limit

### Requirement: No script from another origin

The page SHALL load no script from another origin for server events: the client SHALL be part of
the runtime the server serves.

#### Scenario: A page that uses Server Actions and server events

- **WHEN** a page whose components call Server Actions and subscribe to topics loads
- **THEN** every script it loads comes from the app's own origin
