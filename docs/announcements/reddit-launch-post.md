# eQuantic.UI: launch post drafts (Reddit)

The posts are short on purpose. Everything that got cut lives in section 4, ready to paste as a
reply when somebody asks, which is where detail actually helps: a long post gets skimmed, a good
answer gets upvoted.

> **House style**: no em dashes. Use ":", "…", "()" or a full stop.

---

## 0. Where to post

**Not the r/webdev thread from the screenshot.** It is two years old, so nobody is subscribed to it
any more and the only people who would see the plug are the mods.

| # | Target | Which draft |
|---|--------|-------------|
| 1 | **r/dotnet** | Post A. The exact audience: teams that already write C# and feel the Blazor/MAUI gap. |
| 2 | **r/csharp** | Post A, same as is. |
| 3 | **r/webdev** | Post B, on **Showoff Saturday** (self-promotion is restricted there; check the sidebar that week). |
| 4 | **Show HN** / **r/programming** | Post A. The "no WASM, no Node, our own GPU engine" angle carries on its own. |

Three rules for all of them:

1. **Say it's yours in the title and the first line, and flair it.** Every one of these subs punishes
   hiding it more than the plug. r/dotnet warns on submit if it reads the post as self-promotion, and
   the fix is the **Promotion** flair it offers right there: the sub does not forbid showing your own
   work, it requires the label. Never click past that warning unedited.
   The half a flair cannot fix is a participation clause, where a sub asks that self-promotion not be
   your only activity. Read the rule in the sidebar; if your account has no history there, comment on
   a few threads first or send modmail asking whether a flaired post is welcome.
2. **Attach an image.** `assets/post/hero.png` is ready to use: the real code, the two targets, in
   the framework's own dark tokens (regenerate with `assets/post/render.sh` after editing
   `hero.html`). It is a designed graphic and deliberately not dressed up as a screen capture,
   because a post whose whole argument is "check these claims yourself" cannot afford a mocked-up
   screenshot.
   **A GIF of the real thing beats it**, and only you can record one: the same component in a
   browser tab and in a native window, edited once, hot reloading in both. That is the whole thesis
   in one image, and it is what makes a post travel.
3. **Stay for three hours.** Reddit ranks on early engagement, and section 4 is there so you can
   answer fast.

---

## 1. Post A: r/dotnet, r/csharp, HN

### Title

`One C# class, a web page and a native GPU screen: the UI framework I've been building (0.2 preview)`

Alternative, if you'd rather be judged on evidence than on description:
`Our docs site is a C# app with no JavaScript, no CSS and no npm in its repo. Here's the framework behind it`

Keep it near 100 characters. The longer version this replaced ran to seven lines on a phone and
pushed the image off the screen before anyone had read a word. "I've been building" is doing a second
job besides brevity: it puts the authorship disclosure in the title itself, which is the half of a
self-promotion rule that a flair cannot cover.

> **On naming the output.** The SDK exists to hide the JavaScript, so the pitch shouldn't hand it the
> lead role: what you're selling is C# in, and a small ordinary page out. Say what the browser gets
> (nothing to download, no WASM boot) instead of how it's produced. The mechanism isn't a secret and
> the answer is in section 4 for the moment somebody asks, which they will within minutes. That order
> matters: mentioning JavaScript only as something the developer never writes reinforces the promise,
> while leading with it undermines it.

### Body

---

My project, flagging that up front.

**eQuantic.UI**: you write components in C#, and that is the entire surface. No markup language, no
CSS, no JavaScript, no `package.json`. The build produces a server-rendered web app, and the same
component classes render natively on macOS, iOS and Android through our own Metal/Vulkan engine (no
WebView, no Skia).

What the browser gets is an ordinary page: nothing to download before it runs, no WASM boot, split
per route. The only prerequisite on your machine is the .NET 10 SDK.

The itch: Blazor keeps me in C# but ships megabytes to the browser, MAUI is a second codebase from
the web one, and a JS framework means a second language plus a toolchain to maintain.

```csharp
[Page("/", Title = "MyApp")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context) =>
        Column(gap: Space.S4, children: [
            Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary),
            Button("Increment", onPressed: () => SetState(() => _count++)),
        ]);
}
```

No markup, no CSS, no `new`. Styles are typed values, so the compiler checks your layout and styling
too. That class is a server-rendered web page *and* a native screen; which one you get is a project
setting, not a rewrite.

Two things you can check instead of taking my word for it:

- the playground runs the real compiler in your browser: https://ui.equantic.tech/playground
- https://ui.equantic.tech is itself built with it, from the published NuGet packages

It's a 0.2 preview, MIT. No global state solution yet, no server push, and native means mobile plus
macOS (no Windows or Linux shell). Happy to go into any of it below.

One thing I'd like to know: which C# construct would you throw at the compiler first to see if it
breaks?

---

That last question is doing work. It invites people to attack the hardest part, which is the
conversation you want, and every answer is a conformance case you didn't have.

---

## 2. Post B: r/webdev (Showoff Saturday)

Different room: this audience doesn't care about Blazor's payload, it cares what lands in the browser.
Be explicit that it's for .NET teams, because trying to convert React developers is how these die.

This is the one place to name the output plainly and early. Not as the pitch, but because a sub full
of people who will open the network tab reads any vagueness about what ships as a dodge. Said in one
disarming parenthesis it costs nothing and buys the room.

### Title

`Showoff Saturday: the whole app is C#, no CSS or npm in the repo, and what ships is an ordinary page`

### Body

---

My project, flagging that up front.

**eQuantic.UI**: the entire app is C#. No CSS, no `package.json`, no bundler config in the repo. What
ships is unremarkable, which is the point: server-rendered HTML, a ~110 KB gzipped runtime, split per
route, hydrated on the client. (Yes, the build emits JavaScript. You just never write it, read it, or
debug it: errors surface as C# stack traces.)

The part that might interest you even if you never touch C#: **there is no CSS to author.**
Components declare typed values (spacing, color tokens, type roles) and the compiler lowers each one
into a single atomic class, deduplicated app-wide, with SSR and the client hashing them identically
so hydration never repaints. Hover and focus become real pseudo-classes, so they cost zero JS.

Poke at it without installing anything: the playground runs the real compiler
(https://ui.equantic.tech/playground), and https://ui.equantic.tech is built with the framework from
the published packages, so the network tab is a fair test of what it ships.

0.2 preview, MIT, aimed at teams that already write C#. If you're happy in React this solves a
problem you don't have. Feedback on bundle size and hydration especially welcome.

---

## 3. Post C: the short comment version

For when a "what should I use for cross-platform?" thread comes up. It answers the person first and
mentions the project second, because recommending your own alpha to someone trying to ship would be
both a sales pitch and bad advice.

---

If you already know React Native, use React Native. For a two-person project with a shipped product
as the goal, familiarity beats architecture every time, and Expo gets you Android, iOS and a web
build from one codebase today.

What's worth deciding early is where your business logic lives. Whatever you pick for UI, keep
validation, pricing, permissions and the data model behind an API you own: that's the part you'd
otherwise rewrite when the web version happens.

(Aside, if your backend is .NET: I build eQuantic.UI, where one C# component class becomes both a web
page and a GPU-rendered mobile screen, with camera, location, biometrics and secure storage as
injected services. It's a 0.2 preview, so I won't suggest you bet a product on it… but if "one
language, both targets" is what you're after, it's a real implementation of it:
https://ui.equantic.tech/playground)

---

## 4. Answers, ready to paste

This is where the detail goes. Don't paste verbatim: react to what the person actually said.

**"So it's Blazor?"**
No. Blazor WASM ships the .NET runtime to the browser and runs C# there. Here your components are
compiled ahead of time into plain JS modules: no .NET in the browser, nothing to download before the
page runs, no WASM startup. The trade is the inverse: Blazor runs the whole BCL, I run a defined
subset enforced by build errors.

**"So it does compile to JavaScript. Why isn't that the headline?"**
Because it's the mechanism, not the deal. What you get is C# in and a small ordinary page out, and
the emitted modules are a build artifact you don't open, the same way you don't read IL. It isn't
hidden and I'll answer anything about it, including where the abstraction leaks. It just isn't what
the framework is for.

**"Why not Uno or Avalonia?"**
Both are good and both take the opposite bet on the web: their web target is WASM and their authoring
model is XAML-descended. We emit JavaScript, and authoring is plain C# expressions with typed styles.
On native they render through Skia; we wrote our own Metal/Vulkan engine to avoid inheriting a
foreign canvas's semantics. If you want maturity today, those are the mature ones, and I won't
pretend otherwise at 0.2.

**"Which C# doesn't work?"**
There's a documented supported subset, and anything outside it is a build error with a code, never a
silent miscompile. It's held there by a conformance harness of 500+ cases that runs each one as
emitted JS (through Bun) and as real .NET, then asserts the results match. That covers the things you
would expect to be wrong: `decimal` exact in base-10, `long`/`ulong` as BigInt past 2^53, structural
equality for records and tuples, tick-precise `DateTime`, dictionaries with record keys. Pointers,
`goto` and client-side `System.IO`/`HttpClient` are the notable exclusions; data access goes behind
`[ServerAction]`. Send me a snippet that fails and it becomes a case.

**"What doesn't work yet?"** (the full list, worth pasting when asked)
- No global state: component-local `SetState` only. Signals/context are next.
- Server Actions are request/response. No server push yet (`[ServerEvent]` over SignalR is on the roadmap), so live updates mean polling today.
- Native is macOS, iOS and Android. No Windows or Linux shell (the Vulkan backend exists, the shell doesn't). On Photon, `Image` still renders a placeholder.
- C# stack mapping is member-level, not statement-level.
- Web performance isn't measured in CI. Native has a frame-allocation harness with regression ceilings; the web side has code splitting and tree-shaking but no enforced bundle budgets.
- The VS Code extension packages as a `.vsix` and isn't on the Marketplace yet.
- Small team. That's a real risk factor and I'd rather you weigh it now.

**"Can it use the camera / GPS / Face ID / secure storage?"**
Yes: capabilities are services taken through a constructor and realized per host (`ICamera`,
`ILocation`, `IBiometrics`, `IPhotoLibrary`, `IAppStorage`, `ISecretStore`…). They resolve to `null`
where a host has none, so absence is something your code handles rather than something that throws
later. Secrets are deliberately a separate interface from ordinary storage: an API where safety is a
`secure: true` argument is an API where safety is one forgotten argument away from a token in
`localStorage`. On the web that interface resolves to null, because the browser has no vault and a
store any script on the origin can read is not one.

**"What if I need a JS library?"**
`<BunPackage Include="chart.js" Version="…" />` in the `.csproj`, installed by the embedded Bun at
build. No `package.json`, no Node. That's how the chart and Lottie integrations work.

**"Is the GPU engine really not Skia?"**
Metal and Vulkan backends over a shared RHI, with a scalar CPU rasterizer as the normative reference
both are held to across a shared golden-image catalog, and shaders precompiled offline from Slang to
SPIR-V and metallib. Source is in the repo: `src/eQuantic.UI.Native.Engine.Metal` and `.Vulkan`.

**"Proprietary engine but MIT?"**
"Proprietary" there means we wrote it rather than wrapping someone else's: it's ours, and it's MIT in
the same repo as everything else. Poor word choice on our part.
*(Worth fixing in the README and wiki before posting, because it will be asked.)*

**"Is there tooling?"**
Hot reload on both targets, keeping state. And a VS Code extension whose preview is the real web
realizer running the real compiled module, from the **buffer you're typing in** rather than the file
on disk (p50 293 ms on a 662-line page). Click an element to select the C# expression that built it;
the property panel edits it through Roslyn rewrites. Not on the Marketplace yet.

**"How big is the runtime really?"**
Around 110 KB gzipped for the core, plus your page bundle, split per route. Measure it yourself:
it's in `wwwroot/_equantic/` after any build, and the docs site runs on the published packages if you
would rather just open the network tab.

**"How do I debug it? Do I end up in JavaScript?"**
The error overlay is source-map aware: it fetches each bundle's `.js.map` and shows a C# stack trace.
The honest limit is that mapping is member-level today, so it names the method, not the statement
inside it.

**"How do I know the docs match the code?"**
Every wiki feature carries the release it shipped in, derived from git rather than written from
memory, and a test fails the build when the human-readable mark and the machine-readable one
disagree. Not proof, but it's why I'll quote a `Since` version instead of a vibe.

**"Show me a real app."**
The docs site itself, from the published packages. The repo also has three samples: a web dashboard,
a desktop Photon app and a mobile wallet. The playground is the fastest look.

---

## 5. Pre-flight checklist

One wrong number costs more credibility than the number was worth.

- [ ] **Runtime size.** The README says ~85 KB gzipped; the artifact in the repo
      (`src/eQuantic.UI.Server/wwwroot/runtime.js`) measures **111 KB**. Measure your release build and
      make README, CLAUDE.md and the post agree. The drafts say ~110 KB.
- [ ] **Every link opens in a logged-out browser**, repo and wiki included. A 404 in the first ten
      minutes ends the thread.
- [ ] **The playground works on a phone.** Most of Reddit will open it on one.
- [ ] **`dotnet new install eQuantic.UI.Templates` works on a clean machine** with only the .NET 10
      SDK. Someone will try it within the hour, and "it doesn't build" as the top comment ends the post.
- [ ] **The docs site really is running the published packages.** It's the strongest claim in the post.
- [ ] **The VS Code timing** (p50 293 ms) is the wiki's measurement on `samples/DefaultUIDashboard`.
      Only quote it if it still reproduces.
- [ ] **The GIF is uploaded to Reddit directly**, not linked externally.
- [ ] **The Promotion flair is selected** (r/dotnet) or the sub's equivalent, and authorship is stated
      in the title as well as the first line. Reddit warns on submit when it reads a post as
      self-promotion; submitting flagged and unflaired is how a launch post dies in ten minutes.
- [ ] **The title fits in roughly 100 characters.** Check it on a phone: seven lines of title push the
      image below the fold before anyone has read a word.
- [ ] You have a free evening.
