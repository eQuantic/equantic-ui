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
  middleware. On native, the PLATFORM locale sets the pair — and that needs a hand, because a GUI
  process launched outside a terminal has no `LANG`/`LC_*` and .NET starts invariant on a pt-BR
  machine: `PhotonCultureController` (shipped 2026-08-12, the `PhotonThemeController` shape) owns
  the statics write; shells resolve the truth (`AppleLocale` — `preferredLanguages[0]` is the UI
  culture, `currentLocale` the format culture, natively the D13 pair; `AndroidLocale` — one
  locale feeds both) and apply it before the first realize, with the attached sink repainting on
  a switch. Observing the OS's own locale CHANGE at runtime is a fence per shell, not v1.

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
  The atom is a PAIR, because .NET's is: `CurrentUICulture` picks RESOURCES and `CurrentCulture`
  picks FORMATS, and an app can legitimately run pt-BR strings over en-US number formats (or the
  reverse) — collapsing them into one value would be a quiet departure from the exact experience
  this track exists to reproduce. `$eq.culture = { ui, format }` — set by boot from
  `__EQ_CULTURE__` (which carries both names; they default equal) before hydration, swapped by
  `setCulture`. `$eq.str` resolves against `ui`; `$eq.format` and every D7 formatter resolve
  against `format` — `DateTime.Now.ToString("d")` with no explicit culture formats against
  `CurrentCulture`, exactly as the same C# line does on the server. Naming this atom prevents the
  drift where each formatter grows its own culture plumbing — a drift that has ALREADY happened
  once: `runtime/src/utils/format.ts` calls `toLocaleString(undefined, …)`, which silently formats
  in the BROWSER's locale instead of the request's culture. W5 sweeps it onto the atom.

- **D14 — The SDK's own strings are localized by the SDK, through the same pipeline.** The
  components carry built-in UI strings of their own — accessibility announcements above all
  (2026-08-12 audit: ~14 literals — Checkbox "Checked"/"Unchecked"/"Partly selected", Switch
  "On"/"Off", SearchField "Search…"/"clear search", CodeEditor "Find"/"Previous match"/"Next
  match", Banner/Drawer/Dialog/BottomSheet "Dismiss" in two casings, Chip "Remove", Spreadsheet
  "Spreadsheet"). A 100% Portuguese app announcing "Checked" to VoiceOver fails the founding
  promise the same way a JS catalog would. The rule, mirrored from WinForms/WPF (which localize
  their own chrome and never ask the app to): **a component never hardcodes a UI string** — it
  reads `SdkStrings` (the SDK's own resx-backed accessor in `eQuantic.UI.Components`), which rides
  D2's rewrite on web exactly like an app resource class (it IS one — same ResXFileCodeGenerator
  shape, so W1's semantic detection needs nothing special) and plain satellite assemblies on
  server/native (D10). The SDK's catalog keys join every culture catalog the app emits, always —
  an app with zero resx of its own still gets localized checkbox announcements. Guarded the way
  the factory surface is guarded: a conformance test walks the component sources and fails on a
  string literal in a `Label`/`Placeholder` position, so the fourteen never grow back.

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
- **W4 — Server.** `__EQ_CULTURE__` emission next to `__EQ_THEME__` (both culture names, D13),
  `RequestLocalizationMiddleware` wiring in `AddUI` (ordered BEFORE the render path), SSR
  resolution under the request culture, the `<html lang>` attribute from the request's UI culture
  (the 2026-08-12 a11y audit flagged its absence — screen readers pick pronunciation from it, so
  it ships with the culture bridge, not as a separate nicety), and the caching consequence: the
  shell is now per-culture — any SSR output caching must include culture in its key.
- **W5 — Formatting subset.** The mapped set, `$eq.format` specifier behavior, the shared
  cross-pin fixture, the EQ2100 diagnostic (alignment `{0,10}` is outside v1). Includes the
  D13 sweep: every existing formatter (`utils/format.ts` first) resolves against `$eq.culture`
  — no `undefined` locale survives the workstream. Forward rule for components that RENDER
  culture-shaped data (the C15 DatePicker when it lands, any calendar/number surface): they are
  born reading `DateTimeFormatInfo`/`NumberFormatInfo` through this subset, never carrying month
  names or separators of their own.
- **W6 — Native verification.** Satellite assemblies under NativeAOT/trimming; a Photon sample
  rendering two cultures. STATUS: the risk-retiring half is DONE and executable —
  `scripts/verify-aot-satellites.sh` publishes `tests/eQuantic.UI.Aot.Harness` with NativeAOT and
  refuses to pass unless en/pt-BR/es (and the es-AR parent walk) all answer with the TRANSLATION,
  because a dropped satellite does not crash, it silently answers English. The windowed
  two-culture Photon sample remains as M3's showcase half.
- **W8 — The SDK's own strings (D14).** First the SEAM: extract the audited literals to
  `SdkStrings` with the English values as neutral defaults (mechanical, shippable today — it is
  the standing task chip). Then the resx behind it, the satellite assemblies, the always-included
  catalog keys on web, translations for the launch set of cultures, and the no-literal conformance
  guard. The seam lands before the mechanism so every other workstream can proceed against a
  single choke point.
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
  - Exit: `[x]` the e2e identity test passes on a resx-backed page (including a `{0}` string) — shipped 0.2.0-preview.26 (b8a17e5).
- **M1 — Two cultures + switching.** Request negotiation, `__EQ_CULTURE__`, `setCulture` re-render.
  - Exit: `[x]` pt-BR SSR → switch to en → re-render, no reload, no hydration warning (d2cfd22).
  - Exit: `[x]` a pt-BR page with ZERO app resx announces "Marcado" on a Checkbox (D14: the SDK's
    own strings localize without the app authoring anything), and `<html lang="pt-BR">` is SSR'd
    (c4c9f1b — measured live: pt-BR announces "Marcado", es announces "Seleccionado").
- **M2 — Formatting subset.** The D7 specifier set green in the conformance harness; EQ2100's
  accepted set widens from plain-positional to the full subset; EQ2101 fires on malformed or
  arity-mismatched culture templates.
  - Exit: `[x]` cross-pinned fixture asserted by BOTH C# and vitest (7e94d68 — generated FROM real .NET, three cultures, 100+ cases including the bare `{0}`).
  - Exit: `[x]` the EQ2101 negative test fails the build on a mismatched `pt-BR` template (extra {n}, dropped {n}, malformed — all pinned).
- **M3 — Native.** Two cultures on Photon under AOT.
  - Exit: `[x]` the macOS shell renders both cultures from the same component classes — the Studio's
    Language section carries an ordinary `CultureSwitcher` (it resolves `ICultureController` from
    the context, naming no platform), and the screenshot path renders it twice: default resolves
    the MACHINE's pair (en-GB resources, en-PT formats — D13 visible in one frame), `--culture
    pt-BR` renders Marcado/Ativado/Planilha with R$ 1.234,50. `WindowCultureTests` drives the real
    host: apply, re-render, assert the translated tree, plus es, an untranslated culture falling
    back to neutral, the repaint, and the ui/format pair set apart.
  - Found by writing it: `PhotonApplicationBuilder` registered only the CONCRETE controller, so a
    component asking for `ICultureController` in a window resolved null and the switcher switched
    nothing. Both registrations now point at one instance.
- **M4 — Docs + template.** Wiki page, the SDK template ships a `Strings.resx` and a working
  switcher.
  - Exit: `[x]` the wiki's Localization page documents everything shipped (M0/M1/M2 sections).
  - Exit: `[x]` `dotnet new equantic-app` scaffolds Resources/Strings.resx (+ pt-BR + Designer),
    a CultureSwitcher on the home page, a `{0}` composite tied to the counter, and
    UseRequestLocalization — proven by scaffolding from the packed template and running it: SSR
    answers pt-BR and neutral from the same binary. `equantic-native` ships the same trio
    resolved by plain satellites (D10).

---

## 7. Fences & honesty

- **v1 ships ONE catalog per culture for the whole app.** Per-page splitting is an optimization,
  and saying so up front is better than pretending the first cut is optimal. A marketing site with
  40 pages and 600 strings is a few KB gzipped; an app with 10k strings will want the split, and
  it rides the existing chunk splitter when it does.
- **Formatting outside the D7 subset is refused at build time, not approximated.** A wrong number
  format in a foreign currency is worse than a compile error.
- **Plural languages with 3+ forms are unsupported in v1 and say so out loud** (D8).
- **RTL is not in this track** (D9) — and neither is SCRIPT COVERAGE: Photon's bundled face has no
  CJK or Arabic glyphs, and per-script font fallback belongs to the native text stack, not here.
  Both stated so "we did i18n" is never read as "we render Arabic". The vocabulary already speaks
  Start/End rather than left/right, so when Track S does the RTL flip, this track's output needs
  no rework.
- **Translator workflow is `.resx`/XLIFF** — the framework will not grow a translation UI.
- **Missing key never throws.** It renders the key and warns once: a missing translation must
  degrade to ugly, never to a blank page or a crashed render. The SDK's OWN keys carry one more
  net: the neutral English rides the runtime as generated data, so a page with no catalog at all
  reads "Search…" rather than "SearchPlaceholder".
- **A culture negotiated at runtime with no authored catalog degrades to the NEUTRAL facts.** An
  app shipping pt-BR and es whose visitor switches to plain `en` gets neutral strings AND neutral
  (invariant) formats — deterministic and visible, never browser-dependent. The exact-agreement
  promise of the D7 subset is scoped to the cultures the app ships; emitting catalogs for every
  middleware-supported culture is a v2 build option.
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
- **This plan is design-first, and mostly still design.** Two pieces shipped ahead of the track
  because they owe nothing to the compiler bridge: the W8 `SdkStrings` seam and the D5 native
  culture hand (`PhotonCultureController` + shell locale resolution). Everything touching eqc,
  catalogs, the web bridge and formatting remains unimplemented; the milestone exits below are
  the ledger.

---

## Status log

- **2026-08-13 (later still) — M4 closed; Track L web-complete.** Both templates are born
  localized: `equantic-app` with the resx trio + CultureSwitcher + composite-on-the-counter (the
  whole story in one line), `equantic-native` with the same resx resolved by satellites. Proven by
  packing, scaffolding (`MinhaLoja`, `AppNativa` — sourceName rewrites namespace AND the
  ResourceManager string) and running against the PUBLISHED preview.27 packages. Remaining in the
  track: M3's windowed showcase; then the deliberate fences (plurals 3+, per-page split, metadata
  on switch).

- **2026-08-13 (later) — W6's satellite proof executable.** A NativeAOT publish of a binary
  referencing eQuantic.UI.Components answers the SDK chrome in en/pt-BR/es and lands es-AR on es —
  the satellites and the culture data survive the native image. What remains of the native story
  is M3's windowed sample, which is showcase, not risk.

- **2026-08-13 — M1 + M2 shipped (with W8 the day before).** `ICultureController` (the
  IThemeController shape, BCP-47 names because the contract crosses to a browser), CultureSwitcher,
  setCulture over cached catalogs with the server's own pick walk; the D7 subset cross-pinned by a
  fixture GENERATED from real .NET — banker's pre-rounding, currency codes and the culture's own
  date patterns as `$`-facts in the catalogs, EQ2100 widened, EQ2101 added. Three eqc holes found
  by writing ordinary C# in the sample (target-typed `new` for every compat type). M3's web half
  is effectively proven; the AOT/trimming verification (W6) remains.

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
- **2026-08-12 — Audit-driven review (Edgar restated the directive: every SDK component must be
  multi-language ready, with the exact experience .NET itself offers).** The component/a11y audit
  measured the plan against the shipped code and four gaps entered the design. D13 became the
  culture PAIR — `CurrentUICulture` picks resources, `CurrentCulture` picks formats, and the old
  single-atom text formatted against the wrong one; the audit also caught the drift it predicts
  already live (`utils/format.ts` formats in the browser's locale via `toLocaleString(undefined)`
  — swept by W5). NEW D14 + W8: the SDK's OWN strings — fourteen hardcoded English literals in
  the components, accessibility announcements above all — get `SdkStrings` (seam first, resx
  behind it, keys always in every culture catalog, no-literal conformance guard), because a
  pt-BR app announcing "Checked" fails the founding promise the same way a JS catalog would.
  W4 gains `<html lang>` from the request culture (flagged missing by the same audit). Fences
  gain script-coverage honesty (no CJK/Arabic glyphs in the bundled face; per-script fallback is
  the native text stack's, not this track's) and W5 gains the forward rule for culture-rendering
  components (DatePicker is born from `DateTimeFormatInfo`). Still design-only; the W8 seam is
  the one piece mechanical enough to land ahead of the track (standing task chip).
- **2026-08-12 — Two pieces land ahead of the track (the ones that owe nothing to eqc).**
  W8's seam: `SdkStrings` shipped and every component literal now routes through it — properties,
  never consts, and the transpiled twins prove eqc emits static getters instead of inlining, so
  the choke point survives on web too. And the native culture hand (Edgar's ask, named for the
  repo's precedent): `PhotonCultureController` in Hosting — Apply copies the D13 pair onto BOTH
  .NET statics (default-thread and current) and the attached sink repaints; registered TryAdd
  like the theme hand. Shells resolve the platform truth: `AppleLocale.Resolve()` reads
  `NSLocale` (a Finder-launched process has no `LANG` — .NET sat invariant on a pt-BR Mac),
  macOS wires apply-before-first-frame + repaint attach, iOS applies the statics at host
  creation (the controller instance joins when `PhotonApp.Run` carries services — the theme
  controller's own fence there), Android applies + attaches in the Activity. OS locale-change
  observation is a per-shell fence. Tests: `CultureControllerTests` (pair applied, formats
  follow UI when single, repaint on switch), with a restore-scope because culture statics are
  process state.
