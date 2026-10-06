# Tasks

## 1. The contract in Primitives

- [x] 1.1 Add `ServerTopic<T>`, `IServerEvents` (`Connection`, `Subscribe(topic, onEvent, onRefused)`, `OnConnectionChanged`), `ServerConnection` and `ServerConnectionState` (append-only), and the refusal the component receives; declare them in `PublicAPI.Unshipped.txt`; verified by the build's public API analyzer
- [x] 1.2 Add the absent capability to `AbsentCapabilities` and register it with `TryAddSingleton`, so server rendering subscribes to nothing; verified by a Server test resolving the capability on the server and subscribing through it

## 2. The server's half

- [x] 2.1 `ServerEventsOptions` bound from `EQuantic:ServerEvents` (heartbeat, topics per connection, payload size, events queued per connection), with `UIOptions.UseServerEvents(…)` overriding them; the section added to `developer-surface.baseline.txt`; verified by `DeveloperSurfaceContractTests`
- [x] 2.2 The connection registry and the SSE endpoint `MapUI` maps: the first event hands the connection id, a heartbeat keeps it open, `RequestAborted` releases its topics; `text/event-stream` stays out of response compression; verified by a Server test that reads the stream through `TestServer`
- [x] 2.3 The subscription requests (bind and release a topic on a connection the server issued), refusing a connection id the server never issued; verified by Server tests for both
- [x] 2.4 Topic authorization: templates in route syntax with a policy (the topic as `ServerTopicContext`), `AllowAnonymous()` or a delegate, refusing what no template matches; verified by a Server test per spec scenario
- [x] 2.5 `IServerEventPublisher` through `IServerEventBackplane`, in memory by default, the payload written with `EqJson.Options` and refused past its size limit; verified by Server tests, two instances sharing one backplane among them
- [x] 2.6 `IServerEventHandler`: opened, bound, released, closed, with the `HttpContext` and the template's values, a throwing handler logged; verified by a Server test
- [x] 2.7 Remove `ServerActionHub`, its route, `AddSignalR`, the shell's CDN script tag and `SignalRClient.ts`; verified by a Server test that a rendered page of an app with server actions loads no script from another origin, in Development and in Production, which fails on the old shell
- [x] 2.8 The three endpoints admit anonymous requests, so an app's fallback policy leaves each topic to its rule; verified by a Server test under a fallback policy that requires a user, which fails without it
- [x] 2.9 A bind is decided under the lock the stream's end takes: the topic limit read again there, and a connection whose stream ended binds nothing; verified by a Server test of a bind authorized after its stream ended and one of six binds sent at once, each failing without its half
- [x] 2.10 A payload is split into data lines only at CR, LF and CRLF, so U+2028, U+2029 and NEL stay inside a JSON string; verified by a Server test publishing such an envelope through the backplane, which fails with `ReplaceLineEndings`
- [x] 2.11 A stream ends with its request or with `ApplicationStopping`; verified by a Server test that stops an app with a stream open, which fails without it

## 3. The topic's type in eqc

- [x] 3.1 A `ServerTopic<T>` built in a component carries the hydration spec of `T` in its twin, the spec `HydrationSpec.Of` computes; verified by an emission test for a record, a decimal, a string and a list, target-typed included
- [x] 3.2 `IServerEvents`' members transpile to the runtime capability's; verified by an emission test, and by the conformance of the twin's name, text, equality, hash and refusal of an empty name

## 4. The client in the runtime

- [x] 4.1 `server-events.ts` and `server-event-stream.ts`: one `EventSource` per page, opened by the first subscription and closed by the last at the end of the task, the connection id from its first event, a request per bind and release and one at a time per topic, each payload revived with the topic's spec, refusals reported; verified by vitest specs with a fake `EventSource` and `fetch`, each behaviour proved failing without its code
- [x] 4.2 Reconnection: the state's changes with the last event id, every live topic bound again before connected is reported, and a stream the browser gave up on opened again with a growing wait; verified by vitest specs that drop and restore the stream
- [x] 4.3 Registered as `IServerEvents` in `devices/register.ts`; the served runtime's budget measured on main and on the branch with the Server suite's own test, the bytes named in the commit
- [x] 4.4 Every answer is read whole, so the network panel does not show a subscription that worked as aborted; verified by a vitest spec, and in a browser

## 5. The real thing

- [x] 5.1 A page in `samples/DefaultUIDashboard` subscribes to a topic a hosted service publishes to; in a browser: one event stream in the network panel, events arrive, the page reconnects after the server restarts, and every script comes from the app's origin

## 6. Documentation

- [x] 6.1 A wiki page for server events, in English and Portuguese, on the wiki branch named like the pull request's: topics, the capability, the publisher, authorization, the backplane and handlers seams, the limits, HTTP/2, session affinity; its snippets compiled by `WikiClaimsCompile` and `WikiServerEventsClaims`
- [x] 6.2 The hub's removal as a migration line for the release notes, and one `docs/LEDGER.md` line citing #291
