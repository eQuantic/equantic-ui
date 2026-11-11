- **2026-11-09 · Server events take requests from the app's origins, and bound their streams**: the
  stream, a bind and a release now ask the origin rule the Server Actions and the uploads apply; a stream
  lives at most `MaxConnectionLifetime` and binds its topics again, each authorized again, on the next one;
  an instance holds at most `MaxConnections` streams; `ConfigureEndpoints` lets the app attach rate
  limiting or CORS to the three endpoints; and what a backplane delivers is written only when a frame can
  carry it ([#1292](https://github.com/eQuantic/equantic-ui/issues/1292)). Proposed and archived through
  OpenSpec (`openspec/specs/server-events`).
