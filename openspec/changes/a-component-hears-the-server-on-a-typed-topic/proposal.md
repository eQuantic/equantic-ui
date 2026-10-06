# Proposal

Serves #291 (Real-time server push, a Feature under #288, The roadmap ahead). This is the feature's
first slice, the one every later slice stands on; the slices after it are listed at the end, each
to be filed as a sub-issue of #291.

## Why

The only door from the browser to the server is a Server Action, which is request and response. A
component cannot hear the server: a chat cannot show a message someone else sent, a dashboard cannot
show a price that moved, a page cannot show that a job finished. The app has no way around it, by
design: a component is C# transpiled to JavaScript, EQ2103 refuses `System.Net.Http` and
`System.Net.Sockets` in a component, and an app writes no JavaScript. The SDK owns the door, as it
owns the camera's and the network status's. What exists today is dead weight: an empty SignalR hub
nobody publishes through, a SignalR script the shell fetches from a CDN that nothing in the runtime
calls, and a `SignalRClient.ts` that is not even bundled.

This is a tool for every kind of app, not for one: a chat, a live dashboard, a notification bell, a
document two people edit, a progress bar. So it is built as the frameworks that solved it converged
(Phoenix's channels, Laravel's broadcasting, Rails' Action Cable, ASP.NET's SignalR): a topic the
server publishes to, a subscription the server authorizes, a connection the client keeps, and a seam
at every place an app may need its own piece.

## What Changes

- **A typed topic.** `ServerTopic<T>` names a topic and the type of what it carries. An app declares
  its topics once, in its own C#, and the component that subscribes and the service that publishes
  use the same object, so the name and the payload's type cannot drift apart. The SDK imposes no
  naming convention: `new ServerTopic<Quote>("prices")`, `Topics.Room(id)`, anything the app writes.
- **A capability a component injects, `IServerEvents`**, as it injects `INetworkStatus`:
  `Subscribe(topic, onEvent)` hands each payload over typed, as a Server Action's result is, and
  returns the `IDisposable` that ends it; `OnUnmount` disposes it. The connection's state
  (connecting, connected, reconnecting, disconnected) is observable, and a change carries the id of
  the last event the page saw, so any app can show "reconnecting…" and resynchronize when it comes
  back. A subscription the server refuses is reported to the component. During server rendering the
  capability is absent, as the camera is, and nothing subscribes.
- **A publisher any server code injects, `IServerEventPublisher`**: `PublishAsync(topic, payload)`,
  from a Server Action, a background service, a message handler. An optional id per event travels as
  the event's id, so a page that reconnects can say where it was.
- **Authorization in ASP.NET Core's own terms.** A topic is authorized by a template in the route
  syntax ASP.NET Core already uses (`room:{roomId}`), with a policy (`RequireAuthorization("…")`),
  `AllowAnonymous()`, or a delegate that receives the `HttpContext` and the template's values. A
  policy's handlers receive the topic as their resource, so an app writes an ordinary
  `AuthorizationHandler`. A topic no template matches is refused: subscribing fails closed. A client
  can only listen: nothing a client sends publishes.
- **One connection per page for every topic.** The runtime opens one stream when the first
  subscription starts, multiplexes every topic over it, and keeps it alive with a heartbeat. Each
  subscription is a short request that the server authorizes before it binds the topic to the
  connection. When the stream drops, the runtime reconnects by itself and subscribes every live
  topic again.
- **Server-Sent Events as the first transport**, served by ASP.NET Core with no library and read by
  the browser's own `EventSource`: no script from a CDN, no npm dependency, the client inside the
  runtime. The transport is a seam on both sides, so a second one (WebSocket) is an implementation to
  add, not a rewrite.
- **A backplane seam for more than one server.** Publishing goes through `IServerEventBackplane`,
  in memory by default; an app running several instances plugs its own (Redis pub/sub, a service
  bus) without the publisher or the components changing.
- **Lifecycle hooks on the server.** An `IServerEventHandler` the app registers hears a connection
  open and close and a topic subscribed and released, with the `HttpContext` and the topic's values:
  presence, occupancy and audit are the app's own code on top of it, not something the SDK guesses.
- **Limits from configuration**: the heartbeat, the topics one connection may hold and the size of a
  payload, bound from `EQuantic:ServerEvents` in `appsettings.json` like any other setting.
- **The dead SignalR pieces go**: the empty `ServerActionHub` and its route, `AddSignalR` in
  `AddUI`, the shell's CDN script tag and `SignalRClient.ts`. **BREAKING** for an app that injected
  `IHubContext<ServerActionHub>`, which nothing documented: it publishes through
  `IServerEventPublisher` instead.

Every part the change reaches: Primitives (the topic, the capability, the connection's state), the
Server (the publisher, the endpoints, the authorization, the backplane, the handlers, the options,
the absent capability, the shell), the runtime (the client, its registration as a capability), eqc
(a typed topic carries its payload's hydration spec into the browser), the wiki and the templates'
`Program.cs` comments. The public surface grows by the new types, and the SignalR hub leaves it; the
developer surface gains the `EQuantic:ServerEvents` section.

## Capabilities

### New Capabilities

- `server-events`: a component hears what the server publishes to a typed topic, over one connection
  per page, authorized per topic by the server.

### Modified Capabilities

None.

## After this slice

Each a sub-issue of #291, on the same seams:

- A Redis backplane as an opt-in package, the way an icon pack is one.
- A Server Action that returns `IAsyncEnumerable<T>`, consumed with `await foreach`: progress, a
  model's tokens, a live query, on the same transport.
- Presence: who is on a topic, built on the lifecycle hooks.
- Replay: an optional history store that sends what a page missed since the id it last saw.
- WebSocket as a second transport.
- The native track: `IServerEvents` realized by the Photon shells against the app's server.
