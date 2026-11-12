# Proposal

Closes #1129, the third part of #1093 after E3b, as `docs/ELEMENT-TREE-PLAN.md` §3.7 sets it.

## Why

A client navigation replaces the page, and everything the page built goes with it. An app that draws
its frame (a sidebar, a toolbar) inside each page loses the frame's state on every navigation: the
dashboard sample's sidebar is a field of each of its 22 screens, so opening it on one screen and
following a link closes it. The DOM is patched position by position today, so the frame does not
flicker, but no instance survives, and every page repeats the frame's call.

*How does Flutter solve it?* go_router's `ShellRoute`: the shell is a route of its own, and the pages
are its child. *How does .NET say it?* Blazor's `DefaultLayout` and `LayoutComponentBase.Body`: a
layout component the router renders around every page.

## What Changes

**An app declares its layout once**, in `Program.cs`:

```csharp
builder.Services.AddUI(ui => ui.ScanAssembly(typeof(Program).Assembly).UseLayout<ConsoleLayout>());
```

A layout is a component with a public constructor that takes the page as its one parameter,
`VisualNode body`, and places it where the page belongs. `UseLayout<T>` refuses a type without that
constructor when it is called, naming the type. The word is .NET's (Blazor's layout and its `Body`),
because "shell" already names the HTML document this SDK serves (`ConfigureHtmlShell`).

**The layout stays mounted across client navigations.** The browser keeps one owner for the
document, above the page, built from `new TLayout(page)`. A navigation hands the layout a new page:
the layout's element updates in place and keeps its instance (the same type), and the page's element
is replaced under it (another type). The old page unmounts before the new one mounts, the order the
owner already keeps (`BuildOwner.EndFrame`). Without a layout nothing changes: a navigation patches
the DOM position by position, as today.

**The server renders the same tree.** A page under a layout is rendered as `new TLayout(page)` on the
server too, so hydration adopts the markup in the same order, and every position key (#1477) carries
the layout's steps on both sides.

**A layout knows where it is.** `Navigator.Of(context).Location` answers the path of the page being
shown: the request's on the server, the address bar's in the browser. A layout marks its active
destination with it.

**The dashboard moves its frame into a layout**, `ConsoleLayout`, which owns the sidebar's state.
Its screens return their content only. Opening the sidebar and following a link keeps it open, which
is #1129's proof.

## Parts reached

- **The server:** `UIOptions.UseLayout<T>`, the page render wrapped in the layout, the layout's module
  named in the client configuration.
- **The compiler:** the layout's type is an entry module, as a page's is.
- **The browser:** the document's owner and the page swap under the layout (`boot.ts`,
  `core/component.ts`).
- **The vocabulary:** `Navigator.Location`.
- **The sample** and its README captures, the wiki's routing page in its five languages, a ledger
  entry.

The public surface grows by `UseLayout<T>` and `Navigator.Location`. The developer surface does not
move. No member goes.
