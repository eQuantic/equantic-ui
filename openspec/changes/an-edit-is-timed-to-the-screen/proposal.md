# Proposal

Closes #900, a Task under #898.

## Why

The roadmap's bar for production-ready says an edit reaches the screen in under a second (#898, item
3, and `ROADMAP.md`). Hot reload ships on the web and on the desktop, and the time it takes has never
been measured: not on the dashboard sample, from a save of a `.cs` file to the browser drawing it,
and not on the Photon desktop sample, from the save to the window drawing it.

What is known already says the web is far from the bar. The server waits 400ms after a save before
it starts, then starts a new `dotnet msbuild -t:CompileEQuanticUI` process for every save, which
runs eqc, which runs bun, and only then tells the browser to reload. A first timing of that rebuild
alone, on a machine running a load average of 22, took 11 to 48 seconds.

## What Changes

- An instrument that times an edit from the save to the screen, on both samples, as a developer
  meets it: the app under `dotnet watch`, a real `.cs` file edited on disk, and the time taken when
  the new text is drawn, by a browser on the web and by the window on the desktop.
- Each edit repeated enough to report a distribution, the first one after the app starts (cold) and
  the ones after it (warm), with the parts named: the wait after the save, MSBuild, eqc, bun, the
  reload and the replay on the web, and the delta's compile and apply and the next frame on the desktop.
- Where a sample's number holds the bar, a budget that fails above it, the way `ServedRuntimeBudgetTests`
  holds the runtime's size (#290). Where it does not, an issue that owns each part over the bar.
- `ROADMAP.md` says the measured number in place of "not measured yet".

The parts it reaches: the measuring tool (new), eqc (which reports how long each of its phases
took, so eqc and bun can be told apart), the Photon host (which can report when it draws the frame
an edit asked for) and CI. Nothing a developer using the SDK writes or meets changes, and neither the
public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: the time from a save to the screen is measured on both samples, and a budget holds it
  where the bar is met. The requirement is written once the numbers are measured.
