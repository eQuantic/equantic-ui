# eQuantic.UI — launch post drafts (Reddit)

Copy-paste ready. Everything below the horizontal rules is post copy; the surrounding notes are
for the poster, not for publishing.

---

## 0. Where to post (read this first)

**Do not reply to the r/webdev thread from the screenshot.** It is two years old — the OP shipped
something long ago, nobody is subscribed to it any more, and a framework plug on a dead thread reads
as spam to the only people who will see it (mods). Save the effort for a post that gets a front page.

Recommended order:

| # | Target | Why | Which draft |
|---|--------|-----|-------------|
| 1 | **r/dotnet** | The exact audience: people who already have C# teams and feel the Blazor/MAUI gap this framework was built around. Most likely to give real technical feedback. | Post A |
| 2 | **r/csharp** | Same audience, more language-focused — lead with the transpiler and the conformance harness. | Post A (swap the opening paragraph for the transpiler angle) |
| 3 | **r/webdev** | Big, but JS-first and skeptical of "C# for the web". Self-promotion is restricted here — check the current rules and post on **Showoff Saturday**. | Post B |
| 4 | **Hacker News (Show HN)** / **r/programming** | Where the "compiles to JS, not WASM" and "own GPU engine, no Skia" angles carry on their own. | Post A, trimmed |

Rules change; read the sidebar of each sub the day you post. Disclose that it is your project in the
first line everywhere — every one of these communities punishes the omission far harder than the plug.

**Attach a visual.** The single highest-leverage thing in the whole post is a GIF of the *same* C#
component running in a browser tab and in a native GPU window side by side, edited once and hot
reloading in both. That image is the entire thesis, and it is what makes a post travel. A wall of
text without it will underperform a mediocre post that has one.

**Be there for the first three hours.** Reddit ranks on early engagement, and section 4 below exists
so you can answer the predictable comments fast and well.

---

## 1. Post A — r/dotnet / r/csharp

### Title options

1. `I built a C# UI framework that compiles to JavaScript at build time (no WASM) and renders native through its own GPU engine — 0.2 preview` ← **recommended**
2. `eQuantic.UI: write components once in C#, get a real web app and a real native app (dev preview, MIT)`
3. `Two years on a C#→JS compiler and a Metal/Vulkan UI engine, so one component class can be both a web page and a native screen`

Option 1 states the two non-obvious technical claims in the title, which is what earns the click from
this audience. Option 3 works if you want the personal-story framing — it does well on r/csharp.

### Body

---

I've been building **eQuantic.UI** and it's finally at the point where showing it beats describing it.

The short version: you write components in C#. On the web they are **transpiled to JavaScript at
build time** — no WASM, no multi-megabyte runtime to download. On macOS, iOS and Android the *same*
component classes render through **Photon**, our own GPU engine on Metal and Vulkan — no WebView, no
Skia.

If you want to see it before installing anything, the playground compiles C# in the browser with the
same compiler the build uses: **https://ui.equantic.tech/playground**

**Why I built it**

I wanted the Flutter authoring model without leaving .NET, and the existing options each give up
something I wasn't willing to give up:

- **Blazor WASM** keeps me in C#, but ships a runtime measured in megabytes and still hands me Razor
  and CSS to write.
- **MAUI** gives me native, but it is a second codebase from the web one.
- **Any JS framework** means the team writes TypeScript and CSS, and maintains a Node toolchain.

**What the code looks like**

```csharp
[Page("/", Title = "MyApp")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context) =>
        Column(gap: Space.S4, children: [
            Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary),
            Row(gap: Space.S3, children: [
                Button("Increment", onPressed: () => SetState(() => _count++)),
                Button("Reset", Variant.Outline, onPressed: () => SetState(() => _count = 0)),
            ]),
        ]);
}
```

No markup language, no CSS, no `new` — just C# expressions. Styles are *typed values*
(`Space`, `ColorToken`, `TypeRole`, `EdgeInsets`), so the compiler checks your layout and styling the
same way it checks the rest of your code. On the web those values are lowered to deduplicated atomic
CSS classes (the hundredth card adds zero bytes of CSS); on native the same values become GPU paint,
and no CSS exists on that path at all.

That class above is a server-rendered, hydrated web page **and** a native screen. Which one you get is
a project setting, not a rewrite.

**How the build works**

```
dotnet build
  → Roslyn parses your components
  → eqc transpiles C# → TypeScript (so it's type-checked twice)
  → embedded Bun bundles, split per route
  → ASP.NET Core serves SSR; the client hydrates
```

The only prerequisite is the **.NET 10 SDK**. Bun ships inside the NuGet packages, so there is no
Node, no npm and no bundler config anywhere in your repo.

**The part I care about most: the transpiler is not allowed to guess**

A C#-to-JS compiler is worth exactly as much as its correctness. A silent miscompile is the worst
possible failure here, because the symptom appears in the browser and the developer it hurts most is
precisely the one who doesn't know JS well enough to debug it there.

So every construct resolves to one of three things: a real emission strategy, a faithful compat
helper, or a **build error with a diagnostic code**. Never quietly-wrong output. It's held there by a
conformance harness of 500+ cases that runs each one as emitted JS (through Bun) *and* as real .NET
and asserts the results are identical — including the things you'd expect to be wrong:

- `decimal` — exact base-10, not a float that rounds your invoice
- `long` / `ulong` — BigInt, exact past 2^53
- `record` / `struct` / value tuples — structural equality, `with` copies, and records come back as
  named JS classes with their methods after hydration
- `DateTime` / `DateTimeOffset` / `TimeSpan` — tick-precise arithmetic and formatting
- `Dictionary` with record or tuple keys — structural lookup, not reference identity

Things with no JS equivalent (pointers, `goto`, client-side `System.IO` or `HttpClient`) fail the
build with a canonical message pointing at the C# line, and tell you to move the work behind a
`[ServerAction]`.

**What works today**

- Web: SSR + hydration, client-side router with per-route code splitting, forms and validation
  (including a `[FormModel]` bridge that reads your existing DataAnnotations), Server Actions
  (typed RPC — `[ServerAction]` methods are an allowlist, with `[Authorize]` RBAC enforced before
  execution), SEO metadata, real 404/500 pages.
- Native: GPU windows on macOS, iOS and Android — real text through CoreText, keyboard with a single
  focus order, IME composition, gestures, clipboard, and accessibility bridged to VoiceOver,
  UIAccessibility and Android's `AccessibilityNodeInfo` from one shared semantics tree.
- **Hot reload on both targets** — save the file, the browser page and the native window update in
  place and keep their state.
- Localization from ordinary `.resx` — the compiler rewrites resource accessors into a runtime lookup
  and emits per-culture catalogs, so you localize like any .NET app and never see a JS catalog.
- 50+ components, plus some heavyweights authored once and running on both targets: a spreadsheet with
  Excel-grade selection, in-cell editing, fill handle and TSV clipboard that round-trips with Excel; a
  code editor with incremental highlighting; a virtualized list.

**What it honestly is not, yet**

- **0.2 preview.** The API surface moves between previews. Don't start a client project on it this
  month.
- **No global state management** — component-local `SetState` only. Signals/context are the next
  phase, and it's the gap I hear about most.
- **Native means macOS, iOS and Android.** There is no Windows or Linux desktop shell yet (the Vulkan
  backend is there, the shell isn't).
- **Some browser-only concerns still lack C# abstractions** — clipboard and file upload have surfaces,
  but things like observers and storage are still on the list. That matters because "you never touch
  JS" is only true if the framework covers them.
- **Web performance is not yet measured in CI.** The native side has a frame-allocation perf harness
  with regression ceilings; the web side does not have enforced bundle budgets yet. I'd rather say
  that than quote a benchmark I haven't automated.
- It's a small team. That's a real risk factor for anyone evaluating it, and I'd rather you weigh it
  now than discover it later.

**Try it**

```bash
dotnet new install eQuantic.UI.Templates
dotnet new equantic-app -n MyApp        # web
dotnet new equantic-native -n MyApp     # a real GPU window
cd MyApp && dotnet run
```

MIT. Published on nuget.org as `0.2.0-preview.*`. Playground: https://ui.equantic.tech/playground

**What I'd genuinely like feedback on**

1. The typed-styles-instead-of-CSS decision — does it hold up against real designs you've had to
   build, or does it fall apart the first time a designer hands you something specific?
2. Which missing C# constructs would block *your* code — I'd rather find them from your snippets than
   from my own test list.
3. Is "one class, both targets" actually worth it to you, or is the honest answer that web and mobile
   diverge so much in practice that sharing the component layer buys less than it looks like?

Happy to answer anything, including the uncomfortable questions.

---

## 2. Post B — r/webdev (Showoff Saturday)

Same project, different room. This audience does not care about Blazor's payload; it cares about what
lands in the browser and what it costs to maintain. Lead there, keep it shorter, and be explicit that
it's for .NET teams — trying to convert JS developers is how these posts die.

### Title

`Showoff Saturday: I wrote a UI framework where you author components in C# and the build emits plain JavaScript — no WASM, no Node toolchain`

### Body

---

My project, and I'm the author — flagging that up front.

**eQuantic.UI** compiles C# UI components to JavaScript at build time. What the browser gets is
ordinary JS: server-rendered HTML, a ~110 KB gzipped runtime, code split per route, hydrated on the
client. No WASM, no multi-megabyte runtime download, and no Node/npm/bundler config in the repo — the
bundler (Bun) ships inside the NuGet package, so `dotnet build` is the whole toolchain.

The part that might interest this sub even if you never touch C#: **there is no CSS to author.**
Components declare typed values — spacing, color tokens, type roles — and the compiler lowers each
declaration into one atomic CSS class, deduplicated across the whole app, with SSR and the client
hashing declarations identically so hydration never repaints. Hover and focus become real CSS
pseudo-classes, so those interactions cost zero JS.

```csharp
Column(gap: Space.S4, children: [
    Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary),
    Button("Increment", onPressed: () => SetState(() => _count++)),
])
```

The same component classes also render natively on macOS/iOS/Android through our own Metal/Vulkan
engine — not a WebView — which is the reason the styling layer is typed values instead of CSS strings
in the first place.

You can run it in the browser without installing anything: https://ui.equantic.tech/playground

**Honest scope:** it's a 0.2 preview, MIT, the API moves between releases, there's no global state
solution yet, and it is aimed squarely at teams that already write C#. If you're happy in React, this
solves a problem you don't have — I'm posting it for the "our backend is .NET and we'd rather not run
two stacks" crowd, and because the compiler and the GPU engine were interesting enough to build that
I think they're interesting to look at.

Feedback on the web side especially welcome — bundle size, hydration behaviour, anything you'd measure
before trusting it.

---

## 3. Post C — the short comment version

For when a "what should I use for cross-platform?" thread comes up and mentioning it is *actually*
relevant. Note what it does first: it answers the person's question honestly, and only then mentions
the project. A comment that recommends your own alpha framework to someone trying to ship a business
gets read as a sales pitch and is downvoted accordingly — and it would be bad advice besides.

---

If you already know React Native, use React Native. For a two-person project where one of you is a
junior and the goal is a shipped product, familiarity beats architecture every time, and Expo will
get you to Android, iOS and a web build with one codebase today.

The thing worth thinking about ahead of time is where your business logic lives. Whatever you pick for
UI, keep validation, pricing, permissions and the data model out of the components and behind an API
you own — that's the part you'll otherwise rewrite when the web version happens.

(Different-stack aside, if your backend is .NET: I build eQuantic.UI, which compiles C# components to
JS for the web and renders them natively on GPU for mobile from the same source. It's a 0.2 preview,
so I'm not going to suggest you bet a product on it — but if the "one language, both targets" idea is
what you're after, it's a real implementation of it: https://ui.equantic.tech/playground)

---

## 4. Comment-defence kit

Short, honest answers, written before the thread heats up. Do not paste them verbatim into every
reply — react to what the person actually said.

**"So it's Blazor?"**
No. Blazor WASM ships the .NET runtime to the browser and executes C# there. eQuantic compiles your
C# to JavaScript at build time — there is no .NET in the browser, nothing to download beyond ordinary
JS, and no WASM startup cost. The trade is the inverse: Blazor runs the whole BCL and I run a defined
subset, enforced by a conformance harness and build errors.

**"Why not Uno Platform or Avalonia?"**
Both are good, and both take the opposite bet on the web: their web target is WASM, and their
authoring model is XAML-descended. eQuantic emits JavaScript, and authoring is plain C# expressions
with typed styles — no XAML, no CSS. On native they render through Skia; we wrote our own Metal/Vulkan
engine specifically to avoid inheriting a foreign canvas's semantics. If you want maturity today,
those are the mature ones. I'm not going to pretend otherwise at 0.2.

**"Another framework, why?"**
Fair. The specific gap: no existing option lets a .NET team write one component and get both a small
JS web app and a real GPU native app. Blazor gives C# on the web at a payload cost; MAUI gives native
in a second codebase; JS frameworks give the web but not C#. If that gap doesn't hurt you, this isn't
for you and I'd say so.

**"Which C# doesn't work?"**
Documented as a supported subset, and anything outside it is a build error with a code, not a silent
miscompile — that's the whole design. Pointers, `goto`, and client-side `System.IO`/`HttpClient` are
the notable exclusions; data access goes through `[ServerAction]`. Send me a snippet that fails and it
becomes a conformance case.

**"Is the GPU engine actually not Skia / not a WebView?"**
Correct — Metal and Vulkan backends over a shared RHI, with a CPU reference backend as the normative
one that both are held to within ±1 LSB, and shaders precompiled offline from Slang to SPIR-V and
metallib. The engine source is in the repo; `src/eQuantic.UI.Native.Engine.Metal` and
`.Vulkan` are the two backends.

**"Proprietary engine but MIT license?"**
"Proprietary" there means we wrote it rather than wrapping someone else's — it's ours, and it's MIT
in the same repo as everything else. Poor word choice on our part; the whole thing is open source.
*(Consider fixing that wording in the README before posting — it will be asked.)*

**"Production ready?"**
No. 0.2 preview, the surface moves between previews, and I'll tell anyone evaluating it for client
work to wait. What's stable enough to judge is the model and the compiler's honesty about its own
limits.

**"How big is the runtime really?"**
Around 110 KB gzipped for the core runtime, plus your page bundle, split per route. Measure it
yourself — it's in `wwwroot/_equantic/` after any build, and I'd rather you check than take my number.

**"How do I debug it? Do I end up in JavaScript?"**
Source maps to the original C#, and the error overlay shows the C# stack trace. It's genuinely the
weakest link in the chain of promises — if it breaks down for you, that's the bug report I most want.

**"Who is behind this / what if you stop?"**
A small team. It's MIT and self-contained (the toolchain is embedded, there's no service to shut off),
but I won't pretend the bus factor is anything but a real consideration.

**"Show me a real app."**
The repo has three samples: a dashboard on the web track, a desktop Photon app and a mobile wallet.
The playground is the fastest look, since it runs the real compiler in the browser.

---

## 5. Pre-flight checklist

Verify these before you hit post — the numbers in a launch post get checked by strangers, and one
wrong figure costs more credibility than the figure was worth.

- [ ] **Runtime size.** The README says ~85 KB gzipped; the runtime artifact currently in the repo
      (`src/eQuantic.UI.Server/wwwroot/runtime.js`) measures **111 KB gzipped**. Measure your release
      build and make the README, CLAUDE.md and the post all say the same real number. The drafts above
      say ~110 KB.
- [ ] **The GitHub repo and wiki are publicly visible.** Open every link from the post in a
      logged-out browser. A 404 on the repo link in the first ten minutes is fatal to the thread.
- [ ] **The playground works on a phone.** Most of Reddit will open it on one. If it doesn't, either
      fix it or don't lead with the link.
- [ ] **`dotnet new install eQuantic.UI.Templates` works from a clean machine** with only the .NET 10
      SDK, following the quick start exactly as written. Someone will try it within the hour, and
      "it doesn't build" as the top comment ends the post.
- [ ] **Component count** — the drafts say "50+", which matches the 56 files in
      `src/eQuantic.UI.Components`. Update if you count differently.
- [ ] **The GIF exists and is under Reddit's size limit** (upload directly, not as an external link).
- [ ] **Self-authorship disclosed in the first line** of every version.
- [ ] You have a free evening. Posting and disappearing is worse than not posting.
