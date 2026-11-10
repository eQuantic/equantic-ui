## Why

Closes #1296 and #1297, two Bugs the security review (#1265) found at the edges of the server's routes,
low each:

- #1296: the `/_equantic/{name}.js` fallback set 404 and then served a module it found in the local
  directory, which a browser refuses to run as a module; its 404 outside Development wrote the route value
  back in a JavaScript comment; and the redirect to a prefixed language built `Location` from the decoded
  path, which a header cannot carry when the path holds a non-ASCII character, so the request answered 500.
- #1297: hot reload was on under `dotnet watch` in every environment, so a Production container started
  under `dotnet watch` mapped the anonymous relay and served every module `no-cache`.

## What Changes

- A module the fallback finds in the local directory is served as one under the web root: 200, its type,
  its cache rule.
- A missing module is answered 404 in plain text; only Development says where it looked, and no other
  environment writes back what the request named.
- The redirect to a prefixed language writes the path as a URL writes it (`PathString.ToUriComponent`).
- Under `dotnet watch` hot reload is on in any environment but Production, which serves the relay only
  when the app says `HotReload = true`. A developer who runs `dotnet watch` with no launch profile sets
  `ASPNETCORE_ENVIRONMENT=Development` or says `HotReload = true`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `page-routes`: the component module route's two edges, and the encoded culture redirect.
- `hot-reload`: Production does not stream rebuilds under `dotnet watch` unless the app says so.
