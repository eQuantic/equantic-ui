---
applyTo: "src/eQuantic.UI.Runtime/**,src/eQuantic.UI.Server/**,src/eQuantic.UI.Web/**,tests/eQuantic.UI.Server.Tests/**,tests/eQuantic.UI.Web.Tests/**"
---
# The web: runtime, realizer and server

Flag, as a defect:

- A Node or npm requirement. The runtime is type-checked and tested through the embedded Bun (`dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`), and the Server's BundleRuntime is the one writer of runtime.js, which is never committed.
- A module in the runtime's core/ that imports from shared/ (lowering, components): the cycle only fails when the module is evaluated, as "Class extends value undefined", with tsc clean. Invert the dependency with a registry the heavy module fills.
- A fix that samples something not yet there (layout settling, a size, a browser jump) once, instead of observing it (IntersectionObserver, ResizeObserver, the frame loop).
- A web lowering tested only by a DOM golden. A vitest spec drives it as the browser does: keydown with its flags, pointerdown with coordinates.
- The web realizer and its TypeScript twin changed on one side only, or both reading the same field without an outside oracle (the browser, .NET).
- Growth of the served runtime past its budget without EQ_UPDATE_RUNTIME_BUDGET and a sentence on what the bytes bought (ServedRuntimeBudgetTests).
- An atomic class or style decision made in only one of C# and TypeScript: the two produce the same classes byte for byte.
- A string a person reads hardcoded in a component, an accessibility label included: it comes from the culture's catalog, as .NET's resx would.
- A component state field read in Build and changed without SetState: the web rebuilds only what was marked dirty, so it goes stale there while Photon, which builds every frame, hides the bug.
