# Track W — Photon on the desktop

> The gaps between Photon today and a shippable desktop APP platform, verified in this repo's code
> on 2026-08-25, and the workstreams that close them. The first consumer is the OS Cleaner
> migration (its plan lives with that app and maps its S1–S6 onto W1–W6 here); nothing in this
> track is app-specific — every piece is what any desktop app would ask next.

## The gaps, with evidence

Each was verified by reading the code, not the docs. File references are the proof.

| # | Gap | Evidence |
|---|---|---|
| W-B1 | No Windows/Linux shell; Vulkan creates only Android surfaces | `VulkanSurfaceNative.cs` — `vkCreateAndroidSurfaceKHR` is the only surface entry point |
| W-B2 | No arbitrary paths/arcs in the engine — 8 draw commands, all SDF/texture | `DisplayList.cs` — `Clear..BackdropBlur` (0..7); paths are a normative "v2+" fence in `NATIVE-GPU-ENGINE-PLAN.md` |
| W-B3 | No app-facing frame clock | `IClock.cs` — the fence is verbatim: "this is a PERIODIC clock, not a frame clock… Per-frame animation is a [different thing]" |
| W-B4 | Hit-testing is rectangle-only | `PhotonHost.cs:1431-1459` — every pointer resolution is `Bounds.Contains(point)` on AABBs; `OnPressed` carries no coordinates |
| W-B5 | No desktop shell surface (menus, tray, file dialogs, notifications, deep links, drag & drop, dock, launch-at-login) | the macOS shell exposes a window + capabilities; none of these seams exist |
| W-B6 | Ad-hoc signing, no notarization hook | `src/eQuantic.UI.Sdk.Native/Sdk/Sdk.targets:121` — `codesign --force --deep --sign -`; TCC grants (Full Disk Access) are keyed to the identity, so they break every build |
| W-B7 | One window per process | `PhotonContentView` / `PhotonAccessibility` hold static fields; `PhotonWindow.Run` blocks |

**What already exists and shrinks the work** — found in the same sweep, and the reason estimates
below are smaller than a cold reading suggests:

- **A deterministic time channel already runs through layout.** `TransitionStore` (pure in
  `(path, target, timeMs)`, CSS-parity rules: first sighting mounts at target, mid-flight retarget,
  Reduce Motion snaps) resolves against `ctx.TimeMs` — wired today at two call sites (flex
  weights). `LoopEffect` resolves from the same clock. W2 is therefore *exposure and vocabulary*,
  not plumbing: the interpolation semantics and the time thread exist.
- **The 120 Hz driver exists but is a run-loop timer**, not a display link
  (`PhotonWindow.cs:293` — `CFRunLoopTimer` at 1/120). W2 should swap it for `CVDisplayLink` (or
  keep the timer as fallback) when exposing the ticker; "vsync" today would overpromise.
- **Two-stop elliptical radial gradients exist** (`Paint.cs` — `RadialGradient = 2`, center +
  radii), which is the shading an annular sector needs.
- **The shader toolchain self-resolves** (`scripts/generate-shaders.sh` downloads and
  SHA-256-verifies the pinned slangc), so W1 is startable immediately.
- **Headless screenshots** (`--Photon:ScreenshotPath`, Reference backend) already give any
  desktop app golden tests in CI.

## Workstreams

Estimates are orders of magnitude for one person familiar with the base, for DECISION — not
commitments.

### W1 — Engine: annular sector + convex polygon (3–5 w)

`DrawCommandKind.FillAnnularSector` (center, inner/outer radius, start/end angle, angular gap,
corner smoothing) as an exact SDF in `Sdf.slang`, Reference↔Metal↔Vulkan parity like every other
command. This is NOT a path engine — it is one more shape in the SDF family the engine is built
on, and the existing radial gradient covers its shading.

For blob-like art the decision stays OPEN, and the constraint that decides it is spelled out so
the consumer's spike measures both routes: the shape is DYNAMIC — on the order of 16 points
re-tessellated **every frame**. A `FillConvexPolygon` command (triangulated fan or half-plane SDF)
pays per-vertex work on the GPU each frame; the texture route (`IconRasterCache` eviction + the
dynamic-texture path that `TextureData.Version` already supports) pays a CPU re-raster per frame
instead. Neither is obviously right at that cadence — the spike benchmarks both on the real
16-point blob before W1 commits to either.

Unblocks: sunburst charts, ring selections, donut gauges, mascot bodies.

### W2 — Framework: frame clock + animation (3–6 w, revised down)

The `IClock` fence closed from the side it names: an `IFrameTicker` exposed from the shell's
driver (upgraded to `CVDisplayLink`), tweens over named curves (the exact curve pack is already
called out as a motion fence in `TransitionStore`), springs, and `BoxStyle.Transition` honored
natively by widening `TransitionStore` beyond its two call sites. Includes `IUiDispatcher` (a
`SynchronizationContext`) so state published from worker threads stops racing the render thread —
today `SetState` from a thread pool runs with no barrier at all.

Revised down from the consumer plan's 4–7 w: the time channel, the store semantics and the loop
resolution already exist (see above).

Unblocks: entrance/hover/zoom animation, physics-driven art, and a safe threading contract for
any streaming producer (scanners, network monitors).

### W3 — Primitives: custom drawing node with pointer (2–4 w)

A `Canvas` node (name open) that takes a per-frame command builder inside its layout box, and
pointer events with LOCAL coordinates (down/move/up/hover + modifiers). Polar hit-testing and
per-item picking become the app's arithmetic, not the engine's. Honest fence: only display-list
commands — no generic paths, coherent with the engine's v1 philosophy. This is also the
hit-testing seam the headless-browser instrument proposal has been waiting for.

### W4 — Shell macOS: the desktop surface (6–10 w)

The largest slice, all of it generic:

- Native menu bar (`NSMenu`, declarative) + context menus (right-click is not even delivered today)
- Status item (tray) + an auxiliary panel window (`NSPanel`) — closes W-B7 for the real use case;
  general multi-window can wait
- `IFileDialogs` (open folder/file/save), `INotifications` (`UNUserNotificationCenter` — the
  `DeviceCapability.Notifications` slot is already reserved), `IWorkspace` (reveal in Finder, open
  URL), dock badge/menu
- Dark-mode follow (KVO on `effectiveAppearance` → the existing `IThemeController` seam), runtime
  window title, fullscreen
- Real `NSApplicationDelegate`: deep links (`kAEGetURL`), single-instance, open-file,
  launch-at-login (`SMAppService`), global shortcuts
- Dynamic DPI on monitor change — `backingScaleFactor` is read once today (`PhotonWindow.cs:122`)

### W5 — Sdk.Native: real packaging (4–6 w; SEQUENCE FIRST)

MSBuild properties for a real signing identity, entitlements, hardened runtime and notarization
(`notarytool` + stapler); arbitrary `Info.plist` keys (the set is fixed today); bundling from
PUBLISH output (trimming/self-contained never reach the .app today); DMG generation; a minimal
`IAppUpdater` (manifest endpoint → download → verify → swap → relaunch).

Sequenced FIRST among the independents deliberately: signing identity unblocks three things at
once — TCC grants that survive builds, notifications (which require a signed bundle), and any
auto-update story. W4's notification work lands on it.

### W7 — The developer loop: run, debug, hot reload (2–3 w)

Raised by Edgar on 2026-08-25, and verified: TODAY only the DESKTOP loop is one command —
`dotnet run` opens the Photon window, `dotnet watch` hot-reloads it in process (the window wakes).
Running on a SIMULATOR or EMULATOR is a hand recipe: discover the device (`xcrun simctl list` /
`adb devices`), boot it, install, launch with environment variables — steps that live in nobody's build.

- **One command per target, embedded in `Sdk.Native`**: `dotnet build -t:RunIos` (find or boot a
  simulator, install, `simctl launch` with environment variables), `-t:RunAndroid` (`adb` discovery, `-gpu host`
  boot when needed, install, `am start`). The recipe already exists as prose; it becomes MSBuild.
- **`launchSettings.json` profiles in the native template**, so Rider/VS can run and debug in one click:
  a "Photon (desktop)" profile (`commandName: Project` — full debugging, it IS the project
  process) and "iOS Simulator"/"Android Emulator" profiles invoking the targets. Honest limit:
  DEBUGGING inside a simulator/emulator process is its own story (attach), out of this slice.
- **Hot reload**: desktop stays `dotnet watch` (works today). For simulator/emulator the first
  honest rung is REDEPLOY-on-save through the same targets; true in-process hot reload across the
  device boundary is future work and says so.

Unblocks: the everyday loop of every native app — and the OS Cleaner's F1 onward.

### W6 — Shells Windows/Linux (post-M5, quarters)

Out of this plan's horizon, as the engine roadmap already says (D3D12-vs-Vulkan decision is
post-M5). This track only requires that nothing new re-couples to macOS: every OS call behind a
capability or `IPlatformStrategy`-style seam, as today.

## Sequencing

```
W5 (packaging)  ──────────────►  smallest independent; unblocks TCC/notifications/updates
W1 (engine)     ──────────────►  startable now (toolchain self-resolves); consumer spike gates it
W2 (framework)  ──────────────►  after W1's first shape lands (shares the golden harness)
W3 (primitives) ──────────────►  with W2 (pointer events ride the same host loop work)
W4 (shell)      ──── partial early (file dialogs + deep links), rest after W5
```

## What stays fenced, on purpose

- Generic vector paths (the "v2+" fence in the engine plan stands; W1 adds a shape, not a path
  engine).
- General multi-window (the auxiliary panel covers the desktop tray/popover case; a full
  multi-window story waits for a consumer that needs it).
- Windows/Linux shells (W6) — the decision point is unchanged.

## Acceptance, per workstream

- W1: golden parity Reference↔Metal↔Vulkan for the new commands, like every command before them.
- W2: a fixed-t golden of a mid-flight transition (determinism is the contract), and a
  threading test that publishes from a worker through `IUiDispatcher`.
- W3: a canvas sample whose polar hit-test is app arithmetic; pointer events golden-tested by
  coordinates.
- W4: each seam behind a capability with the macOS implementation registered, VoiceOver intact.
- W5: a notarized, stapled bundle from `dotnet publish` in CI, installed and relaunched by the
  minimal updater — the test the consumer's Electron never managed.
