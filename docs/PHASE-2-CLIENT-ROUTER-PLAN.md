# Implementation Plan — Phase 2: Client Router

> Phase 2 of `ROADMAP.md`. Phase 1 (transpiler correctness) is done; the SPA primitives come next, and
> the router is the first. Same shape as `docs/IMPLEMENTATION-PLAN.md`: objective → exit criteria →
> workstreams → milestones.

## Why this phase

Today every navigation is a **full page reload**: the server maps each `[Page]` route to a `MapGet`
endpoint, SSRs the page, and the client `boot()` mounts it **once** — there is no client-side router, no
history management, and `<a>`/`Link` clicks hit the network and re-run the whole boot. That throws away
the SPA value the per-page bundles + reconciler already make possible. Phase 2 wires them together:
navigate by swapping the page bundle and re-rendering into a persistent shell, with the History API.

## Current state (audited)

- **Server routing** — complete. `UIExtensions.MapPages` scans `[Page]` attributes and registers a
  `MapGet(route, …)` per page; `ServeAppShell` SSRs the page and injects `window.__EQ_CONFIG` (`page`,
  `version`, `ssr`). 404/500 handled. `[Page]` already accepts ASP.NET route syntax (`/user/{id:int}`),
  though no sample uses params and the value isn't yet surfaced to the component.
- **Client** — `boot.ts` reads `__EQ_CONFIG.page`, dynamic-imports `/_equantic/{Page}.js`, then hydrates
  the SSR HTML (or mounts). One-time only. **No** `history`/`pushState`/`popstate`/click interception.
- **Bundles** — per-page JS already emitted with code-splitting (`bun build … --splitting`) and loaded by
  dynamic import. Route-based code-splitting is therefore *already there*; the router just reuses it.
- **Link** — `Components/Link.cs` renders a plain `<a href>`; no SPA hook.
- **Layouts** — manual: each page wraps itself in a shell (e.g. `DefaultDashboardShell`). No persistent
  layout across navigations.
- **Tests** — none for routing/navigation.

## Objective

Internal navigation happens **client-side with no full reload**: the URL changes via the History API,
the target page's bundle loads (reusing the existing split bundles), its component renders into the app
root, browser back/forward works, and the initial SSR load still hydrates exactly as today. Unmatched
routes fall through to the server (real 404). Behavior is covered by runtime tests (happy-dom).

### Exit criteria (Phase 2 is "done" when) — ✅ ALL MET
- [x] A **client route table** is available in the browser (generated from `[Page]` attributes, injected
      via `__EQ_CONFIG.routes`), matching paths (incl. `{param}` / `{param:type}`) to page bundles. *(M0)*
- [x] A **`Router`** in the runtime intercepts internal `<a>` clicks, `pushState`s, loads + renders the
      target page into `#app`, and handles `popstate` (back/forward) — no full reload. *(M0)*
- [x] **Route parameters** (`/users/{id}`) and query string are parsed and exposed to the page via
      `RenderContext` (`context.Route.Param("id")` / `context.Route.Query("q")`), matching the server's
      binding (SSR populates it from the HTTP route values + query; client nav from the matched route).
      *(M1; demonstrated by the `Users`/`UserDetail` sample pages in M3.)*
- [x] **Persistent layout**: a shell stays mounted across navigations, with only the routed content
      swapped (no re-mount of the nav/sidebar) — via reconcile-on-navigate. *(M2a)*
- [x] **`Link`** opts into SPA navigation (and external/`target=_blank`/modified clicks fall back to the
      browser); **prefetch** on hover/focus (`data-prefetch`). *(M2b)*
- [x] **Scroll restoration**: `scrollRestoration='manual'`; scroll resets to top on a forward
      navigation and is restored from the History entry on back/forward. *(M1)*
- [x] **Route guards** hook (a `CanActivate`-style async check that can cancel/redirect a navigation).
      *(M2c; demonstrated by the `/admin` auth gate in M3.)*
- [x] Runtime tests (happy-dom) cover: click → SPA nav, back/forward, param/query parsing, guard
      cancel/redirect, fallthrough to server on unmatched. Green in CI. *(250 vitest)*

## Workstreams

- **W1 — Route manifest (build + server).** Emit the route table from `[Page]` attributes. The server
  already enumerates them in `MapPages`; surface the same list to the client (inject `__EQ_CONFIG.routes`
  in `ServeAppShell`, and/or write `/_equantic/routes.json`). Each entry: `{ pattern, page, hasSsr }`.
- **W2 — Runtime `Router`.** `src/eQuantic.UI.Runtime/src/router/`. Match a URL against the table
  (segment matcher with `{param}`/`{param:type}` + query parse), load the page module (the existing
  dynamic-import path, refactored out of `boot()` into a reusable `mountPage`), render into `#app`,
  and own the History API (`pushState`/`replaceState`/`popstate`) + a global capture-phase click handler
  that intercepts same-origin, unmodified, non-`_blank` `<a>` clicks.
- **W3 — Params + query in `RenderContext`.** Add a route-data accessor to `RenderContext` so a page reads
  `context.Route.Param("id")` / `context.Route.Query("q")`. Server SSR and client nav populate it the same
  way (server from ASP.NET route values; client from the matched pattern).
- **W4 — Persistent layout.** A `Layout`/`RouterOutlet` concept: the shell mounts once; the router swaps
  only the outlet's content. Start minimal (a single root outlet); nested layouts can follow.
- **W5 — `Link` + prefetch + scroll + guards.** `Link` marks itself for SPA interception; hover prefetch
  warms the target bundle. Scroll reset/restore tied to history entries. A guard hook
  (`IRouteGuard`/delegate) runs before activation and may cancel or redirect.
- **W6 — Tests.** Runtime vitest (happy-dom) for the router; server tests for manifest generation; a
  sample (a multi-page app) exercised end-to-end.

## Milestones (sequenced — start at M0)

**M0 — Walking skeleton (smallest end-to-end slice).**
Refactor `boot()`'s page-load into a reusable `mountPage(root, page, config)`. Add a `Router` that, given
a route table, intercepts internal link clicks → `pushState` → `mountPage` (no reload), and handles
`popstate`. Inject `__EQ_CONFIG.routes` from the server. Acceptance: in happy-dom, clicking an internal
`<a>` swaps the page without a reload and updates `history`; back/forward works; an external/unmatched
link is left to the browser.

**M1 — Params + query + scroll. ✅ DONE.** Route-pattern matching with `{param}` (+ inline constraints
`:int`/`:guid`/… mirrored client-side) and query, exposed via `RenderContext.Route` (`Param`/`Query`);
scroll reset on a forward nav / restored on back/forward (`scrollRestoration='manual'`). SSR populates
`RenderContext.Route` from the HTTP route values + query (AsyncLocal, like the service provider) so the
first load matches client nav. 21 router vitest cases (matcher, params/query, constraints, scroll, title,
race-guard, hash, fallthrough). Remaining acceptance detail (end-to-end `/users/{id}` page through a real
build) folds into M3's sample.

**M2 — Persistent layout + `Link` + prefetch + guards. ✅ DONE.**
- *Persistent layout (M2a):* SPA navigation now **reconciles** the new page against the outgoing page's
  tree instead of wiping + remounting the root. When two pages share a layout shell the reconciler keeps
  the shell's DOM (identity, listeners, scroll, focus) and only patches the changed content — persistent
  layout with no explicit layout/outlet concept. Primitives: `RenderManager.adopt`, component
  `mountReconcile`/`getCurrentTree`/`disposeQuietly`/`hydrate`; `boot.ts` tracks `currentComponent` and
  disposes the outgoing page *without* tearing down the reconciled DOM. The "Loading…" flash on nav is gone.
- *`Link` + prefetch (M2b):* the `Link` C# component emits `data-prefetch` (default on; `Prefetch=false`
  opts out, `_blank` excluded). The router adds delegated `pointerover`/`focusin` listeners that warm a
  matched route's bundle once (deduped) via `onPrefetch` → `loadPageModule`, so the click navigates
  instantly. `router.prefetch(href)` is also callable programmatically.
- *Guards (M2c):* `RouterOptions.guards` + `router.addGuard` register `NavigationGuard`s consulted
  **before** a forward navigation commits (so a cancel leaves the URL untouched). A guard returns
  `true`/`undefined` (allow), `false` (cancel) or an href (redirect); first to decide wins; may be async.
  `boot.ts` registers guards from `window.__eqGuards`. The no-guard path stays synchronous up to
  `pushState` (unchanged timing).

Verified live (chrome-devtools, real build): navbar is the **same DOM node** across forward + back nav with
active-link updating and the counter still interactive (`0→2`); hovering a `data-prefetch` link loads the
target bundle before any click; a `window.__eqGuards` guard blocks `/counter` (URL stays `/`) while letting
`/showcase` through — all with zero console errors. Tests: 250 vitest (+15: persistent-layout, prefetch,
guards) + .NET compiler 377 / conformance 492 / server 37.

**M3 — Sample + docs. ✅ DONE.** `samples/DefaultUIDashboard` now demonstrates the whole phase end-to-end:
a persistent shell (navbar) across SPA navigation; route params via `/users` → `/users/{id:int}` reading
`context.Route.Param("id")` (+ `?role=` query); and a route guard gating `/admin` (redirect to `/login`
when signed out, allowed after sign-in) registered through `window.__eqGuards` and driven entirely by C#
`Link`s. Verified live (chrome-devtools): shell DOM persists through param + guard navigations, params
render on SSR + SPA + direct load, the guard cancels/redirects/allows — zero console errors.

Bringing the sample up surfaced and fixed three real compiler/build gaps (a developer can author pages many
ways; all must work):
- **Static / `const` fields on components.** `private static readonly List<Person> People = new() {…}` was
  dropped by the parser, emitted nowhere, referenced as `this.people` (undefined), and `People.Count`
  mistranslated to the bogus enum literal `'count'`. Now: component fields are parsed (static/instance),
  emitted as `static`/instance class members with transpiled initializers, referenced as `ClassName.field`
  (`IdentifierStrategy`), and types used only in a field initializer are reactively imported. The
  `EnumStrategy` PascalCase heuristic no longer fires when the semantic model resolved a non-enum symbol
  (it was turning `Items.Count` into `'count'`). (Compiler test `StaticFieldRepro`.)
- **bun `--splitting` nested page entries.** Without `--root`, bun inferred the repo root as the base and
  wrote entries to `wwwroot/_equantic/samples/…/ts/Dashboard.js`, which the boot's flat `/_equantic/<Page>.js`
  404'd on (stale flat entries had masked it). Fixed with `--root <intermediate-ts-dir>` so entries are flat.

## Decisions / principles

- **Client nav renders client-side** (load bundle → run `Build` → page fetches its own data via
  `[ServerAction]`), consistent with the existing architecture; the first load stays SSR+hydrate for SEO.
- **Unmatched routes fall through to the server** (real 404 / non-SPA pages keep working).
- **Progressive enhancement**: with JS off (or before boot), `<a href>` still works as a normal link —
  the router only *enhances* navigation.
- **No new user-facing JS**: the route table is generated from `[Page]`; authors keep writing C#.

## Build-infra fixes that unblocked the real-build sample (toward M2/M3)

Bringing a real multi-page sample (`samples/DefaultUIDashboard`) up end-to-end surfaced three latent
defects — invisible in unit/conformance tests, fatal in a real `dotnet build` + browser run. All three are
fixed with regression coverage:

- **eqc resolved no project references → mis-transpilation.** The eqc semantic model gathered references
  from a hardcoded `bin/Debug/net8.0` path; the sample targets `net10.0`, so the directory never existed
  and *zero* eQuantic assemblies were loaded. With `HtmlElement`/`IList<IComponent>` unresolved, member
  calls degraded to naive camel-casing — `Children.Add(x)` emitted `.children.add(x)` (no such array
  method), crashing SPA boot. Fix: the SDK now writes the exact `@(ReferencePathWithRefAssemblies)` set
  (resolved via `ResolveReferences`) to `obj/.../equantic.refs.txt` and passes it to eqc with `--refs`;
  eqc uses that complete set verbatim. The transpiler's type view now matches the real `csc` build,
  independent of TFM/config/NuGet-vs-ProjectReference. (`eQuantic.Build/Program.cs`,
  `ProjectCompilationHelper`, `Sdk.targets`; compiler regression `IListAddRepro`.)
- **Runtime `buildEvents()` dropped forwarded handlers.** The C# `HtmlElement.BuildEvents()` merges
  `CustomEvents`, but the hand-written TS runtime equivalent only discovered own `on*` props. Composite
  components (e.g. `Button`) forward their resolved handler set to a child element via `customEvents`, so
  the child silently lost every handler — buttons rendered but did nothing. Fix: the TS `buildEvents()`
  now merges `customEvents` too, mirroring C#. (`runtime/src/core/types.ts`; 4 vitest cases in
  `build-events.spec.ts`.)
- **`npm run build` was broken** (`tsc` errored before `vite` ran): a spec unused-import, a callable+statics
  cast needing `as unknown as`, strict-null inference in a spec, and unexported public factory interfaces
  (`TimeSpanFactory` &c.) blocking declaration emit for `$eq`. All fixed; the documented runtime build is
  green again.

Verified live (chrome-devtools against the real build): SSR 200 → hydrate with no console errors → router
installs → internal-link click is a *soft* nav (sentinel survives, no reload) → the counter is interactive
after SPA nav (`0→3→2→0`) → the navigated page's `.js.map` serves 200 **with `.cs` sources** (C# breakpoints
work after navigation).
