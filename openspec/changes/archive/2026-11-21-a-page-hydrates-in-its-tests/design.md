# Design

## Where the page runs

A browser would be the faithful host, and is a toolchain an app's test project would have to install, which
the product principle rules out. The runtime's own tests run in happy-dom, a DOM in JavaScript; the SDK
already ships a JavaScript engine, the embedded bun. So the page runs in happy-dom under that bun.

happy-dom can load a page by itself (its `Browser`), with its own ES module loader, which parses modules with
its own scanner: the runtime's bundle is beyond it (`import.meta`, long strings). So the harness does what
Vitest's happy-dom environment does: one `GlobalWindow`, its DOM registered as the process's globals, the
document fetched and parsed without running scripts, then the scripts run in the order a browser runs them,
classic ones by `eval`, module ones by bun's own ES modules. A runtime plugin fetches each module from the
app's server; bun 1.4 hands a plugin only absolute paths, so each module's text has its imports written as
the absolute paths a browser resolves them to (the import map applied to a bare name, a relative path
resolved against the module), and the boot's computed `import('/_equantic/Page.js')` is already one.

The harness is `src/eQuantic.UI.Testing/Hydration/harness.ts`, bundled with happy-dom (from the runtime's
lockfile, one copy in the tree) by the embedded bun at the package's build, and carried as a resource.

## Which bun

The bun the app was built with: the SDK's `ResolveBunPath` resolves it for every app build (from the
per-OS runtime package, or the source tree), and now returns it. The package's `buildTransitive` targets ask
each project the test project references for it after the build and write it beside the test assembly.
`HydrationAssert` reads that file. No search of a package folder, no environment variable.

## Where the page is served from

The page's scripts are fetched over a socket, so the app is served by Kestrel: `HydrationAssert` calls
`UseKestrel` on a factory that has not started (on loopback, port 0) and starts it; one that already started in
memory is refused with a sentence saying what to do.

## What counts as a difference

The reconciler already makes the DOM say what the client's tree says (#1302). It now records each text and
attribute it wrote over, and each element or text the server wrote past the client's children (which it leaves
in place, and which is what a blank label is). Classes are compared as a set, since the two atomizers may
list one element's classes in another order, and `data-eq-*` markers are the client's infrastructure. Proven on
the dashboard sample: every page hydrates with no difference at 1280 and at 400 pixels.

## The router and the server

The boot already resolved the URL with the route table to seed the current route; it now compares that page
with the one the document says the server drew, and the navigation's payload names the server's page, so the
same comparison runs after a client navigation. A router that chose nothing (a not-found page) disagrees with
nothing. In development the console says it; the record carries both pages, so the test fails in either
environment.
