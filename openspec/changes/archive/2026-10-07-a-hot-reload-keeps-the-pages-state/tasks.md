# Tasks

## 1. The page's fields cross

- [x] 1.1 `capturePageState`: the page's own fields, without the runtime's, each one that comes back
      exactly, written with the hydration spec of its runtime type
- [x] 1.2 `restorePageState`: the fields the reloaded page declares too, each rebuilt from its spec by
      the typed `hydrate`, and kept only where the page declares or initializes that type, before it
      builds
- [x] 1.3 The boot captures with the first and replays with `replayPageState`, which restores and keeps
      the server's payload from writing over a field it decided
- [x] 1.4 Check: `hot-reload-state.spec.ts`, through JSON as sessionStorage carries it, and
      `boot-hot-reload.spec.ts`, through the real boot and a mount

## 2. Against the real thing

- [x] 2.1 The dashboard sample under `dotnet run`: `Count: 3`, a save to `DeclarativeScreen.cs`, and the
      page comes back with the edit and `Count: 3`, then counts on to 4; on the base it came back 0
- [x] 2.2 The wiki's hot reload pages in English and Portuguese, and one `docs/LEDGER.md` line
