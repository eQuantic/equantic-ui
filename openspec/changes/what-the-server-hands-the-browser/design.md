## Context

A developer writes `app.UseStaticFiles(); app.UseServerActions(); app.MapUI();`. Nothing there says compression, and the
product principle says nothing should have to.

## How Flutter answers it

`flutter build web` leaves compression to the host. Here the SDK is the host, and it already registered the providers.

## Decisions

- **`IStartupFilter`, the mechanism .NET has for a library to add to a pipeline it does not own.** It stands in front, so
  the runtime's endpoint, the static files and a page all pass through it, in either order the app wires them. The
  precompressed `.br` beside each published module would need the static assets pipeline, which `UseStaticFiles` does not
  read, and would leave the runtime, which is an embedded resource, out. An app that also calls `UseResponseCompression`
  changes nothing: a response that says its encoding is left alone.
- **Every kind of token crosses**, rather than only those that differ from the default, so the browser reads one source and
  `theme-bridge.photon.json` pins it. The fallback stays for a page written by an older shell during a deployment.
- **Two different waits for #997.** The renderer's streams are its answer, so the deadline holds them all. A rebuild that
  succeeded has no answer to read, so hot reload waits only on the failure path, within what is left of its limit.

## Risks / Trade-offs

- Compressing costs CPU on each request of the near-megabyte runtime, at `CompressionLevel.Fastest`, which `AddUI` already
  chose. The address carries a version, so a browser asks once.
