# Localization Plan — Track L

**The directive (Edgar, 2026-08-01):** multi-language in eQuantic.UI must work **without any
third-party library**, using only what .NET itself offers. The developer writes C# and .NET —
they never see JavaScript, never touch a JS i18n catalog, never learn a framework-specific DSL.

This document is the design. It has three laws:

1. **Canonical .NET authoring.** Localization is `.resx` + the strongly-typed accessor, exactly as
   a .NET developer already knows it. No new DSL, no string-keyed `T("...")` in app code, no JSON
   catalogs hand-edited by humans. If a developer can localize an ASP.NET Core app today, they can
   localize an eQuantic.UI app with the same knowledge and the same IDE tooling.
2. **One tree, three targets.** The SAME component tree renders localized text on the server (SSR),
   in the browser (after hydration, on every re-render) and on Photon (native, AOT). A string that
   resolves differently across targets is a bug of the same severity as a layout divergence.
3. **BCL only.** `System.Resources` + `System.Globalization` — in-box, AOT-safe, no package.
   `Microsoft.Extensions.Localization` may be used as OPTIONAL server-side interop; it never
   becomes a dependency of Primitives, the runtime, or the native stack.

---

## 0. Why this is not "just ship a dictionary"

The naive approach — a JSON catalog and a `t("key")` helper — fails all three laws: it invents a
non-.NET authoring surface, it has no compile-time key safety, and it forces the developer to think
about a client-side runtime concept. It is also exactly the shape every JS framework already has,
which is the thing this framework exists not to be.

The real constraint is mechanical: **`ResourceManager.GetString()` cannot run in the browser.** It
reads assembly resources. So the compiler must bridge that gap at build time — otherwise
JavaScript leaks into the developer's world, which violates the founding promise.

Everything below follows from that one sentence.

---

## 1. The mechanisms we already have (this plan invents almost nothing)

| Need | Existing machinery | Where |
|---|---|---|
| Resolve `Strings.Hero_Title` to a resource key at build time | eqc's Roslyn semantic model, fed the EXACT assemblies csc uses (`--refs`) | `Sdk.targets` :226, `SemanticModelProvider` |
| Route a C# type to runtime-provided behavior instead of emitting it | `RuntimeProvidedTypeScanner` + `[RuntimeProvided]` | `Compiler/Services/` |
| Ship server-selected data to the client BEFORE hydration | `ThemeBridge.SerializeJson` → `window.__EQ_THEME__` → boot applies pre-hydration | `Web/ThemeBridge.cs`, `UIExtensions.cs` :505, `boot.ts` :102 |
| Swap a global at runtime and re-render | `setPhotonTheme` | `runtime/shared` |
| Compute what a page actually depends on | `ComponentDependencyResolver` | `Compiler/Services/` |
| Pin C# ↔ TS agreement in CI | the theme-bridge shared fixture (C# pins, vitest round-trips) | `theme-bridge.photon.json` |

Localization is the **theme bridge again, for strings**. That symmetry is the plan's core bet: the
theme is server-selected, serialized, applied before hydration, swappable at runtime, and identical
on native. Culture is the same shape of problem with the same shape of answer.

---

## 2. Authoring — what the developer writes

```csharp
// Strings.resx           → "Hero_Title" = "Build products, not plumbing."
// Strings.pt-BR.resx     → "Hero_Title" = "Construa produtos, não encanamento."

public override VisualNode Build(ComponentContext context)
{
    var column = new Column(gap: Space.S3);
    column.Add(new Text(Strings.Hero_Title, TypeRole.Display));
    column.Add(new Text(Strings.Hero_Subtitle, TypeRole.BodyL));
    return column;
}
```

That is the whole authoring surface. It is ordinary .NET: the IDE's resx editor, IntelliSense over
keys, a typo is a **compile error** (string-keyed catalogs cannot offer this), and `git diff` shows
translation changes as data, not code.

---

## 3. Key decisions

- **D1 — `.resx` is the single source of truth; the generated accessor is the API.** No parallel
  catalog format. Translators receive `.resx` (every TMS on earth reads it) or the XLIFF the .NET
  tooling already produces. The framework never asks a human to write JSON.

- **D2 — eqc REWRITES resource accessors; it must never inline them.** This is the sharpest rule in
  the plan and the easiest to get wrong. eqc already inlines cross-assembly constants (icon glyphs
  prove it). A resource accessor looks constant-ish and is NOT: it depends on
  `CultureInfo.CurrentUICulture` at call time. Inlining it would bake the build machine's culture
  into the bundle — a silent-wrong-code bug of the worst kind (English strings on a Portuguese
  page, discovered in production). The accessor lowers to a runtime lookup:
  `Strings.Hero_Title` → `$eq.str("Strings", "Hero_Title")`.
  Detection is SEMANTIC, never a name list (per the project's design rule): a generated resource
  class is recognized by its ResXFileCodeGenerator SHAPE — an internal/public static class exposing
  `ResourceManager` of type `System.Resources.ResourceManager` and a `Culture` of type
  `CultureInfo`. A compiler test pins that a resource accessor is never emitted as a literal.

- **D3 — Catalogs are emitted per culture, at build, from the keys the app actually uses.** eqc
  collects `(resourceClass, key)` pairs from the reachable tree and emits
  `wwwroot/_equantic/strings/{culture}.json`. Unused keys never ship. Because the only API is the
  typed accessor (D1), there are no dynamic keys — reachability is complete **by construction**,
  not by heuristic; no string-keyed catalog can promise that. v1 emits ONE catalog per culture for
  the whole app; per-page splitting rides the existing chunk splitter later (§7).

- **D4 — The culture bridge mirrors the theme bridge, slot for slot.** The server emits
  `window.__EQ_CULTURE__ = { name: "pt-BR", strings: { … } }` in the same shell slot as
  `__EQ_THEME__`; boot installs it BEFORE hydration. This is not a convenience — it is what makes
  hydration parity structural: the client resolves the same strings the server rendered, so the
  SSR-identity contract the e2e already enforces cannot silently break on a translated page.
  Two transports, one catalog: the shell inlines the ACTIVE culture's catalog (first paint needs
  no extra fetch); the static `strings/{culture}.json` files exist for `setCulture` (D6) to load
  alternate cultures on demand. Consequence, stated out loud in §7: the rendered shell is now
  per-culture, and any SSR output caching must key on culture.

- **D5 — Culture selection is ASP.NET Core's job, not ours.** `RequestLocalizationMiddleware`
  (first-party) negotiates from route/cookie/`Accept-Language`; the SDK only reads
  `CultureInfo.CurrentUICulture`. We ship no negotiation logic, no cookie format, no custom
  middleware. On native, the platform locale sets `CurrentUICulture` and nothing else changes.

- **D6 — Switching culture re-renders; it never reloads.** `setCulture(name)` fetches the catalog
  if absent, swaps it, invalidates the tree — the exact `setPhotonTheme` shape. The developer
  reaches it through C# (a `CultureSwitcher` component / `context.Culture`), never through JS.

- **D7 — Formatting is a MAPPED SUBSET, never a reimplementation.** This is the honest hard part.
  `CultureInfo`'s formatting is far richer than the browser's `Intl`. Reimplementing .NET's
  formatting in TypeScript is a multi-year trap; pretending `Intl` matches is a silent-divergence
  trap. So v1 defines a **closed, tested subset** — integers, fixed-decimal (`N0`–`N4`), currency
  (`C`), percent (`P`), short/long date, and the invariant round-trip — mapped `CultureInfo` →
  `Intl` and CROSS-PINNED by a shared fixture the C# tests and vitest both assert (the theme-bridge
  pattern). A format string outside the subset is a **build-time diagnostic (EQ2100)**, not a
  runtime surprise. The developer is told at compile time, in C#, that a format will not survive
  the trip.

- **D8 — Plurals are explicit keys in v1.** .NET ships ICU but exposes no public plural-rule
  selection API, so there is nothing "in-box" to lean on. v1: `Plural(count, Strings.Items_One,
  Strings.Items_Other)` — correct for the 2-form languages (en/pt/es/fr/de/it). Languages with 3+
  forms (ru, pl, ar, cs) are a **documented v1 fence**, not a silent wrong answer: the API rejects
  them with a diagnostic rather than pluralizing incorrectly. Full ICU MessageFormat is a v2
  decision that must be weighed against law 3 (it would mean a dependency or a hand-written rule
  table).

- **D9 — RTL is a LAYOUT concern and is explicitly out of this track.** Mirroring (`Row` direction,
  padding start/end, icon flipping) belongs to the layout vocabulary and is tracked with Track S,
  not here. Stated so nobody assumes "we did i18n" means "we support Arabic".

- **D10 — Native needs no bridge at all.** Photon runs the same C# with no transpilation:
  `ResourceManager` + satellite assemblies work directly. The plan's only native work is
  VERIFICATION (satellite assemblies survive trimming/NativeAOT) — not implementation. This is the
  write-once payoff and the reason this design beats a JS-catalog approach outright.

- **D11 — Composite format is the second authoring surface and gets the same guard as D2.** The
  resx accessor has no parameters, so the canonical .NET pattern for placeholders is
  `string.Format(Strings.Greeting, userName)` with `"Olá, {0}!"` living in the catalog. It lowers
  to `$eq.format($eq.str("Strings","Greeting"), userName)` — a runtime implementation of .NET
  composite formatting whose specifiers are exactly the D7 subset (`{0}` plain substitution,
  `{0:C}`, `{0:N2}`, …); alignment (`{0,10}`) is outside v1 → EQ2100. Two guards, both pinned:
  (a) the CompileTimeEvaluator already recognizes `string.Format` as an evaluatable pattern (the
  TW helpers prove it) — it must NEVER fire when the template is a resource accessor, because the
  template is per-culture data resolved at call time. This is D2's rule surfacing a second way,
  and it gets its own never-evaluate test.
  (b) at build, eqc validates the templates of EVERY culture's resx: a malformed specifier or an
  argument-arity mismatch against the neutral culture (`pt-BR` says `{2}`, neutral has only
  `{0}`/`{1}`) is **EQ2101** — caught on the build machine, not when a Brazilian visits the page.

- **D12 — The fallback chain is flattened at build time; the runtime never implements it.** .NET
  resolves pt-BR → pt → neutral. Reproducing that cascade in TypeScript would be a
  reimplementation of .NET resolution semantics — exactly what law 3 forbids in spirit. Instead,
  W2 emits each `{culture}.json` with the chain already collapsed (a key present only in the
  neutral resx appears in every culture's catalog), and the runtime does a flat O(1) lookup with
  no fallback logic at all. A conformance test pins the chain: a neutral-only key resolves
  identically in .NET and in JS.

- **D13 — The client has ONE current-culture atom, and it is runtime state, not a parameter.**
  `$eq.str`, `$eq.format` and every D7 formatter resolve against `$eq.culture` — set by boot from
  `__EQ_CULTURE__.name` before hydration, swapped by `setCulture`. It is the client mirror of
  `CultureInfo.CurrentUICulture`: `DateTime.Now.ToString("d")` with no explicit culture formats
  against it, exactly as the same C# line does on the server. Naming this atom prevents the drift
  where each formatter grows its own culture plumbing.

---

## 4. Architecture

```
  Strings.resx / Strings.pt-BR.resx        ← the developer's only localization artifact
        │
        ├── csc → satellite assemblies ────────────────► SERVER (SSR)   ResourceManager, CurrentUICulture
        │                                                └────────────► NATIVE (Photon)  identical, no bridge
        │
        └── eqc (Roslyn semantic model)
              ├── rewrites  Strings.Hero_Title → $eq.str("Strings","Hero_Title")     (D2)
              ├── rewrites  string.Format(Strings.X, a) → $eq.format($eq.str(…), a)  (D11)
              └── emits     wwwroot/_equantic/strings/{culture}.json
                            (used keys only, fallback chain flattened)               (D3, D12)
                                  │
   server shell: window.__EQ_CULTURE__ = { name, strings }   (D4, the __EQ_THEME__ slot)
                                  │
                       boot installs BEFORE hydration → runtime catalog store
                                  │
                    setCulture(name) → swap + re-render (D6, the setPhotonTheme shape)
```

---

## 5. Workstreams

- **W1 — Semantic discovery + rewrite (compiler).** Shape-based resource-class detection, accessor
  → `$eq.str` lowering, composite-format → `$eq.format` lowering (D11), the never-inline guard
  test (extended to `string.Format`-with-accessor), EQ2100 format diagnostic, EQ2101 cross-culture
  template validation. **Prerequisite that is cheap to do and expensive to forget:** the generated
  `*.Designer.cs` files must be part of the source set eqc compiles — shape detection lives in the
  semantic model, and if the Designer files are absent, `Strings` is an unresolved identifier and
  the symptom ("Strings is not defined", far from the cause) looks like a user bug instead of a
  build-wiring bug.
- **W2 — Catalog emission (compiler + SDK).** Reachable-key collection, per-culture JSON emission
  with the fallback chain flattened at emit time (D12), a build channel + output path, incremental
  rebuild behavior, and hot reload: editing a `.resx` must invalidate the emitted catalog and ride
  the same SSE reload path code edits use — a translation edit with no feedback loop is a dead
  workflow.
- **W3 — Runtime (TypeScript).** Catalog store, the `$eq.culture` atom (D13), `$eq.str`,
  `$eq.format` (D11), `setCulture`, re-render integration, missing-key policy (return the key,
  warn once — never throw, never blank the UI).
- **W4 — Server.** `__EQ_CULTURE__` emission next to `__EQ_THEME__`, `RequestLocalizationMiddleware`
  wiring in `AddUI` (ordered BEFORE the render path), SSR resolution under the request culture,
  and the caching consequence: the shell is now per-culture — any SSR output caching must include
  culture in its key.
- **W5 — Formatting subset.** The mapped set, `$eq.format` specifier behavior, the shared
  cross-pin fixture, the EQ2100 diagnostic (alignment `{0,10}` is outside v1).
- **W6 — Native verification.** Satellite assemblies under NativeAOT/trimming; a Photon sample
  rendering two cultures.
- **W7 — Tests.** Conformance (the same key+culture resolves identically in .NET and in the JS
  runtime — the existing conformance harness extended to strings), the D12 flattening pin (a
  neutral-only key resolves identically in .NET and JS), an EQ2101 negative test (a mismatched
  `pt-BR` template fails the build), realizer pins, and an e2e:
  serve pt-BR → SSR text is Portuguese → hydrate with class+text identity → switch to en → re-render
  without reload.

---

## 6. Milestones

- **M0 — One culture, end to end.** resx → rewrite → catalog → SSR → hydrate, including plain
  positional composite format (`"Olá, {0}!"`) — the first real page hits it immediately. EQ2100
  exists from day one with the accepted set = plain positional: specifiers are refused until M2
  widens the set, never approximated.
  - Exit: `[ ]` the e2e identity test passes on a resx-backed page (including a `{0}` string).
- **M1 — Two cultures + switching.** Request negotiation, `__EQ_CULTURE__`, `setCulture` re-render.
  - Exit: `[ ]` pt-BR SSR → switch to en → re-render, no reload, no hydration warning.
- **M2 — Formatting subset.** The D7 specifier set green in the conformance harness; EQ2100's
  accepted set widens from plain-positional to the full subset; EQ2101 fires on malformed or
  arity-mismatched culture templates.
  - Exit: `[ ]` cross-pinned fixture asserted by BOTH C# and vitest.
  - Exit: `[ ]` the EQ2101 negative test fails the build on a mismatched `pt-BR` template.
- **M3 — Native.** Two cultures on Photon under AOT.
  - Exit: `[ ]` the macOS shell renders both cultures from the same component classes.
- **M4 — Docs + template.** Wiki page, the SDK template ships a `Strings.resx` and a working
  switcher.

---

## 7. Fences & honesty

- **v1 ships ONE catalog per culture for the whole app.** Per-page splitting is an optimization,
  and saying so up front is better than pretending the first cut is optimal. A marketing site with
  40 pages and 600 strings is a few KB gzipped; an app with 10k strings will want the split, and
  it rides the existing chunk splitter when it does.
- **Formatting outside the D7 subset is refused at build time, not approximated.** A wrong number
  format in a foreign currency is worse than a compile error.
- **Plural languages with 3+ forms are unsupported in v1 and say so out loud** (D8).
- **RTL is not in this track** (D9).
- **Translator workflow is `.resx`/XLIFF** — the framework will not grow a translation UI.
- **Missing key never throws.** It renders the key and warns once: a missing translation must
  degrade to ugly, never to a blank page or a crashed render.
- **The rendered shell is per-culture.** The active culture's catalog rides inline in every page's
  shell (the D3 size trade-off, restated for the HTML), and any SSR output caching must include
  culture in its key (W4). Alternate cultures load on demand via `strings/{culture}.json` (D4/D6).
- **`setCulture` does not re-run `IHandleMetadata` in v1.** The SSR'd `<title>`/meta keep the
  landing culture until the next navigation. Documented, not silent; wiring SEO metadata into the
  culture swap is a v2 item.
- **Server-produced strings are data, not catalog entries.** Anything the server composes
  (ServerAction results, server-side validation messages) arrives already localized — the server
  has the real `ResourceManager` under the request culture. The client catalog covers only what
  client components render. Stated so nobody duplicates server strings into the client catalog.
- **The resx editing experience outside Visual Studio/Rider is dated, and this track will not fix
  it.** Law 1 bets familiarity over modernity — for a .NET audience, the right bet. But the fix
  for a weak editor is the IDE's resx tooling or the XLIFF/TMS round-trip, never a parallel
  catalog format: that "fix" is exactly what law 1 exists to forbid.
- **This plan is design-only.** Nothing here is implemented yet; the status log below records
  design history only, until code lands and the milestone exits start getting checked.

---

## Status log

- **2026-08-01 — Plan written (design only, no code).** Motivated by the eQuantic site dogfood
  (`../equantic-web`), where the design's `src/i18n.js` needs a .NET-native answer. Decision to
  design before implementing was Edgar's — the alternative (bolting strings onto the site and
  retrofitting later) would have set the authoring surface by accident.
- **2026-08-01 — Design review pass (no architecture change; the theme-bridge bet stands).**
  Added D11 (composite format lowers to `$eq.format`, never compile-time evaluated; EQ2101
  validates every culture's templates at build), D12 (fallback chain flattened at emit time — the
  runtime never reimplements .NET resolution), D13 (the `$eq.culture` atom as the client mirror
  of `CurrentUICulture`). W1 gains the `*.Designer.cs` source-set prerequisite; W2 gains resx hot
  reload; M0 gains plain-positional composite format with EQ2100 active from day one. Fences
  gain: per-culture shell/caching, SEO-on-switch (v1), server-produced strings, and the
  resx-editor honesty note.
