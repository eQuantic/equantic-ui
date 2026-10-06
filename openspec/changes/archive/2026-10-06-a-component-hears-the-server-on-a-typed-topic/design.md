# Design

## Context

A Server Action is a `fetch` POST to `/api/_equantic/actions`, dispatched by `ServerActionsMiddleware`
in the request's own DI scope, authorized by the Primitives' `[Authorize]` through ASP.NET Core's
`IAuthorizationService`, and answered as JSON written with `EqJson.Options`; the transpiled stub
revives the result with `$eq.hydrate` and a hydration spec eqc computes from the C# type
(`HydrationSpec.Of`). A capability (`INetworkStatus`, `ICamera`) is an interface in Primitives that a
component takes in its constructor, registered by name in the runtime (`devices/register.ts`), absent
on the server (`AbsentCapabilities`), and realized by each Photon shell. `StatefulComponent` has
`OnMount` and `OnUnmount` on both sides. SignalR is registered and mapped but carries nothing: the hub
is empty, no `IHubContext` is used, the shell fetches a SignalR script from cdnjs that the runtime
never calls, and `SignalRClient.ts` is not bundled.

Flutter's answer is a `Stream` and a `StreamBuilder` (FLUTTER-PARITY's row for `StreamBuilder` notes
the gap: "a stream that arrives while the user watches has no vocabulary here"), fed by a package the
app ships inside itself (`web_socket_channel`, an SSE client, Firebase's listeners), never fetched
at run time. The server side is the app's own. Here the SDK owns both ends, so it supplies the
channel and the server's half, and the frameworks that own both ends (Phoenix, Laravel, Rails,
SignalR) converge on the same four parts: a topic, a server-side authorization per topic, a
publisher, and a pluggable fan-out across instances.

## Goals / Non-Goals

**Goals:**
- A general mechanism for any app: topics, payloads and authorization are the app's; the SDK
  imposes no naming, no topology and no product concept (no "room", no "user channel").
- A seam at every place an app may need its own piece: transport, backplane, authorization,
  lifecycle.
- The .NET mechanisms an ASP.NET Core developer already knows: route templates, authorization
  policies and handlers, options bound from configuration, dependency injection.
- Zero external runtime dependency: no CDN, no npm package, the client inside `runtime.js`.

**Non-Goals:**
- A streaming Server Action (`IAsyncEnumerable<T>`), presence, replay, WebSocket, a Redis package and
  the native realization: each is a later slice on these seams (proposal, "After this slice").
- Client-to-server messages over the connection: a Server Action already is that door, with its own
  authorization and payload allow-list.
- Sharing one connection between tabs.

## Decisions

**Topics, not a method the server calls.** #291 sketched `[ServerEvent] void OnPriceChanged(Quote q)`
on a component. A method fixes the channel per component type: it cannot say "this room" or "this
order", which every real case needs, and it ties a server publish to a component class. A topic is a
value: built at run time from the app's own data, subscribed by any component, published by any
service. The attribute can come back as sugar over a topic, without changing this.

**`ServerTopic<T>` is a typed value, the name inside it a plain string.** The name is the app's (no
convention imposed, any characters), and the type parameter is the contract: `Subscribe` takes an
`Action<T>`, `PublishAsync` takes a `T`, so a mismatch is a compile error rather than a payload that
reads as undefined. An untyped string overload is left out: it would be the one place the contract
could drift.

**A capability, as the device APIs are.** `IServerEvents` is injected (nullable where a target lacks
it), absent on the server, testable with a fake, and realizable by the native shells later without
the component changing. Its shape follows `INetworkStatus`: a current state, and subscriptions that
return `IDisposable`. A base-class API was the alternative; it would put a transport concern on every
component and could not be faked.

**Server-Sent Events first, behind a transport seam.** The need is one way, server to client; the
other way is a Server Action. SSE is HTTP, so it passes proxies and is multiplexed by HTTP/2; the
browser's `EventSource` reconnects by itself and sends the last event's id; ASP.NET Core serves it
with no library. WebSocket was weighed: two-way is not needed, and the client would have to rebuild
reconnection and heartbeats. SignalR was weighed: it needs its JavaScript client (fetched from a CDN
today, which breaks the self-contained principle, or bundled, about 13 KB gzipped) and its protocol
for a one-way stream. Both stay possible: the server's connection registry and fan-out do not know
the transport, and the runtime's client talks to the transport through one interface, so WebSocket
is an implementation to add. The seam is internal until a second transport exists, because a public
knob with one value is a member kept for nothing.

**One stream, subscriptions as requests.** The stream's first event hands the page a connection id;
a bind posts `{topic}` to `/_equantic/events/{connection}/subscribe` and a release to
`…/{connection}/release`. The alternative, the topics
in the stream's query string, would reopen the stream whenever a component mounts, losing what was
published in between, and would authorize every topic again each time. The connection id is an
unguessable token bound to the stream that issued it, and a request naming an id the server did not
issue is refused.

**Authorization: route templates, policies, a delegate, and a closed default.** A template uses
ASP.NET Core's route syntax (`room:{roomId}`), parsed and matched by its own `RoutePattern` machinery,
so a developer reads it as a route. A template takes a policy name (the topic reaches the policy's
handlers as a `ServerTopicContext`: its name, its values, the connection's id, the `HttpContext`), `AllowAnonymous()`, or
a delegate for the case a policy would be heavy. No match refuses. The Primitives' `[Authorize]`
attribute, which Server Actions use, was the alternative; a topic is not a member to put it on.

**The payload's type reaches the browser with the topic.** Generic arguments are erased in
JavaScript, so eqc gives `ServerTopic<T>`'s twin the hydration spec of `T` where the topic is built,
the spec `HydrationSpec.Of` already computes for a Server Action's result, and the runtime revives
each payload with `$eq.hydrate` before the handler sees it. The server writes payloads with
`EqJson.Options`, the format the revival reads, so the serializer is not a seam: it is the wire
contract.

**A backplane seam, separate from the local fan-out.** `IServerEventBackplane` publishes an envelope
(topic, payload already serialized, optional id) and delivers the envelopes it receives to the
instance's registry, which writes them to the connections holding the topic. In memory by default.
An app with several instances registers its own; a Redis package is a later slice.

**Lifecycle hooks as handlers the app registers.** `IServerEventHandler` hears a connection open and
close and a topic bound and released, with the `HttpContext` and the template's values. Presence,
occupancy and audit are built on it by the app. A handler that throws is logged and does not break
the connection or the subscription.

**Limits are options, bound from configuration.** `ServerEventsOptions` binds `EQuantic:ServerEvents`
(heartbeat, topics per connection, payload size, events queued per connection) and the fluent
builder can override them. A connection whose queue fills is closed, and the page reconnects and
resynchronizes: a slow reader never grows the server's memory.

**Connected means bound.** After a connection opens, the runtime binds every live topic and reports
`Connected` once they are bound, with the last event's id. A page that resynchronizes through a
Server Action on that change asks when nothing more can be missed in between; reported when the
stream opened, an event published between the page's read and the binds would be lost.

**The endpoints admit anonymous requests; each topic decides.** A stream carries nothing until a
topic is bound to it, and every bind is authorized by the topic's rule. Left to an app's fallback
authorization policy, a policy that requires a user would refuse the stream itself, and with it
every topic the app allowed anyone to hear.

**A stream ends with its request or with the app.** A graceful shutdown waits for every request in
flight; a stream that ended only when its page went away held each instance until the host's timeout,
and in a rolling deploy kept its pages on the instance that was leaving. Linked to
`ApplicationStopping`, the stream ends, and the page reconnects as after any drop.

**A bind is decided under the lock the stream's end takes.** The topic limit is read again where the
topic is bound, and a connection whose stream ended binds nothing: a bind authorized after the end
would hold a topic no one ever releases, and a presence handler would count a member who left.

**The SignalR pieces are removed, not repurposed.** Nothing publishes through the hub, nothing in the
runtime reads the script, and `SignalRClient.ts` is not bundled. Keeping them would leave a second,
dead door beside the real one.

## Risks / Trade-offs

- [Six HTTP/1.1 connections per origin: each tab holds one stream] → HTTP/2, Kestrel's default over
  TLS, multiplexes them; the documentation says so. Sharing one stream across tabs is a later option.
- [A proxy that buffers the stream] → the response is `no-cache`, sends `X-Accel-Buffering: no`, and
  `text/event-stream` stays out of response compression; the heartbeat keeps idle proxies from
  closing it.
- [Delivery is at most once] → the connection's change reports the last event id, so the app
  resynchronizes through a Server Action; replay is a later slice on that id.
- [Ordering across instances depends on the backplane] → the in-memory one keeps publish order per
  topic; a replacement documents its own.
- [Several instances behind a load balancer] → a bind names the connection, which only the instance
  holding the stream knows, so a page's requests need session affinity, as SignalR's do; a bind that
  reaches another instance is reported as `Failed`, and the browser's console names the cause.
  Forwarding binds through the backplane is the alternative to weigh when an app cannot pin.
- [The connection's id is a bearer token] → anyone who learns it can bind to that page's stream the
  topics they themselves are authorized for, or release its topics; it travels in the request's
  path, so access logs hold it. Binding the connection to the identity that opened it is a hardening
  slice.
- [A bind is authorized as the request that makes it] → so a request another site makes the
  visitor's browser send, cookies included, would bind the visitor's topics to a stream that site
  opened. A bind and a release take only a JSON body: a form or a `no-cors` fetch cannot send one,
  and a cross-origin `fetch` that does needs a preflight the app's CORS policy refuses.
- [A handler awaits its own work] → a connection's transitions are heard in order, so a handler
  still awaiting a join holds that connection's close until it returns. A handler that never
  returns holds its connection open, which is the handler's defect to fix, not the order's.
- [A bind whose stream has just ended is answered as unknown] → the server cannot tell that page
  from one whose requests reach the wrong instance, so the browser asks again, four times over three
  and a half seconds, binding on whatever connection the page then has, before it reports the
  topic refused.
