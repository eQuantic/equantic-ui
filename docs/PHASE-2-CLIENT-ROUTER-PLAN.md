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

### Exit criteria (Phase 2 is "done" when)
- [ ] A **client route table** is available in the browser (generated from `[Page]` attributes, injected
      via `__EQ_CONFIG.routes`), matching paths (incl. `{param}` / `{param:type}`) to page bundles.
- [ ] A **`Router`** in the runtime intercepts internal `<a>` clicks, `pushState`s, loads + renders the
      target page into `#app`, and handles `popstate` (back/forward) — no full reload.
- [ ] **Route parameters** (`/users/{id}`) and query string are parsed and exposed to the page via
      `RenderContext` (a typed accessor), matching the server's binding.
- [ ] **Persistent layout**: a shell can stay mounted across navigations, with only the routed content
      swapped (no re-mount of the nav/sidebar).
- [ ] **`Link`** opts into SPA navigation (and external/`target=_blank`/modified clicks fall back to the
      browser); optional **prefetch** on hover.
- [ ] **Scroll restoration**: scroll resets to top on push, restores on back/forward.
- [ ] **Route guards** hook (a `CanActivate`-style async check that can cancel/redirect a navigation).
- [ ] Runtime tests (happy-dom) cover: click → SPA nav, back/forward, param/query parsing, guard
      cancel/redirect, fallthrough to server on unmatched. Green in CI.

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

**M1 — Params + query + scroll.** Route-pattern matching with `{param}`/query, exposed via
`RenderContext`; scroll reset on push / restore on pop. Acceptance: a `/users/{id}` page reads its `id`
client-side identically to SSR; conformance/runtime tests green.

**M2 — Persistent layout + `Link` + prefetch + guards.** A shell stays mounted across navigations; `Link`
opts into SPA nav with hover prefetch; a route-guard hook can cancel/redirect. Acceptance: navigating
within a shell does not re-mount the shell; a guard blocks a protected route; tests green.

**M3 — Sample + docs.** A multi-page sample demonstrates SPA nav, params, a guard and a persistent
layout; update the wiki + `DOTNET-COVERAGE-PROGRAM` cross-links and mark Phase 2 ✅ in `ROADMAP.md`.

## Decisions / principles

- **Client nav renders client-side** (load bundle → run `Build` → page fetches its own data via
  `[ServerAction]`), consistent with the existing architecture; the first load stays SSR+hydrate for SEO.
- **Unmatched routes fall through to the server** (real 404 / non-SPA pages keep working).
- **Progressive enhancement**: with JS off (or before boot), `<a href>` still works as a normal link —
  the router only *enhances* navigation.
- **No new user-facing JS**: the route table is generated from `[Page]`; authors keep writing C#.
