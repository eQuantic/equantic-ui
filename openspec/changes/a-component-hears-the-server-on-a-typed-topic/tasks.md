# Tasks

## 1. The contract in Primitives

- [ ] 1.1 Add `ServerTopic<T>`, `IServerEvents` (`Connection`, `Subscribe(topic, onEvent, onRefused)`, `OnConnectionChanged`), `ServerConnection` and `ServerConnectionState` (append-only), and the refusal the component receives; declare them in `PublicAPI.Unshipped.txt`; verified by the build's public API analyzer
- [ ] 1.2 Add the absent capability to `AbsentCapabilities` and register it with `TryAddSingleton`, so server rendering subscribes to nothing; verified by a Server test rendering a component that subscribes in `OnMount`

## 2. The server's half

- [ ] 2.1 `ServerEventOptions` bound from `EQuantic:ServerEvents` (heartbeat, topics per connection, payload size, events queued per connection), with `UIOptions.UseServerEvents(…)` overriding them; the keys added to `developer-surface.baseline.txt`; verified by `DeveloperSurfaceContractTests`
- [ ] 2.2 The connection registry and the SSE endpoint `MapUI` maps: the first event hands the connection id, a heartbeat keeps it open, `RequestAborted` releases its topics; `text/event-stream` stays out of response compression; verified by a Server test that reads the stream through `TestServer`
- [ ] 2.3 The subscription requests (bind and release a topic on one's own connection), refusing a connection id the server did not issue; verified by Server tests for both
- [ ] 2.4 Topic authorization: templates in route syntax with a policy (the topic as `ServerTopicResource`), `AllowAnonymous()` or a delegate, refusing what no template matches; verified by a Server test per spec scenario
- [ ] 2.5 `IServerEventPublisher` through `IServerEventBackplane`, in memory by default, the payload written with `EqJson.Options` and refused past its size limit; verified by Server tests, two instances sharing one backplane among them
- [ ] 2.6 `IServerEventHandler`: opened, bound, released, closed, with the `HttpContext` and the template's values, a throwing handler logged; verified by a Server test
- [ ] 2.7 Remove `ServerActionHub`, its route, `AddSignalR`, the shell's CDN script tag and `SignalRClient.ts`; verified by a Server test that a rendered page loads no script from another origin

## 3. The topic's type in eqc

- [ ] 3.1 A `ServerTopic<T>` built in a component carries the hydration spec of `T` in its twin, the spec `HydrationSpec.Of` computes; verified by an emission test for a record, an enum, a decimal and a list
- [ ] 3.2 `IServerEvents`' members transpile to the runtime capability's; verified by an emission test and by the conformance of a revived record payload

## 4. The client in the runtime

- [ ] 4.1 `server-events.ts`: one `EventSource` per page, opened by the first subscription and closed by the last, the connection id from its first event, a request per bind and release, each payload revived with the topic's spec, refusals reported; verified by vitest specs with a fake `EventSource` and `fetch`
- [ ] 4.2 Reconnection: the state's changes with the last event id, every live topic bound again; verified by a vitest spec that drops and restores the stream
- [ ] 4.3 Registered as `IServerEvents` in `devices/register.ts`; the served runtime's budget measured on main and on the branch with the Server suite's own test, the bytes named in the commit

## 5. The real thing

- [ ] 5.1 A page in `samples/DefaultUIDashboard` subscribes to a topic a hosted service publishes to; in a browser: one event stream in the network panel, events arrive, the page reconnects after the server restarts, and every script comes from the app's origin

## 6. Documentation

- [ ] 6.1 A wiki page for server events, in English and Portuguese, on the wiki branch named like the pull request's: topics, the capability, the publisher, authorization, the backplane and handlers seams, the limits, HTTP/2
- [ ] 6.2 The hub's removal as a migration line for the release notes, and one `docs/LEDGER.md` line citing #291
