## Why

Closes #1292, a Bug the security review (#1265) found in server events, low in each of its parts:

- the stream, the bind and the release did not consult the origins the app allows, as the Server
  Actions and the uploads do;
- a topic was authorized once, when it was bound, and stayed bound until the stream dropped, through a
  sign-out or a role the app took away;
- an instance had no cap on its streams, and the endpoint builders were discarded, so an app could not
  attach ASP.NET Core's rate limiting or CORS to them;
- an envelope a backplane delivered was written to the page without the check the publisher applies.

## What Changes

- The stream, the bind and the release ask `AllowedRequestOrigins`, the rule of the Server Actions and
  the uploads: the app's own host, or an origin in `EQuantic:ServerActions:AllowedOrigins`.
- A stream lives at most `MaxConnectionLifetime` (thirty minutes by default). The page connects again and
  binds its topics again, each authorized as the request that binds it is then.
- An instance holds at most `MaxConnections` streams (10 000 by default); one more is answered 503 with
  `Retry-After`, before any of the app's handlers runs, and the page's client tries again after a growing
  wait, as after any stream that failed.
- The three endpoints are one route group, and `ServerEventsBuilder.ConfigureEndpoints` applies what the
  app attaches to all of them. A cap per client is a concurrency limiter partitioned by client, through
  `RequireRateLimiting`: one keyed on the address inside the SDK would refuse every page behind a proxy.
- An envelope from the backplane is written only when its id is one a frame's id line can hold and its
  payload is one JSON value, the check the publisher applies to what it sends, now shared.
- New public members: `ServerEventsOptions.MaxConnections`, `ServerEventsOptions.MaxConnectionLifetime`
  and `ServerEventsBuilder.ConfigureEndpoints`, and their two configuration keys.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `server-events`: the limits name the streams and their lifetime, and requests come from the app's
  origins.
