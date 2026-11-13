# Tasks

## 1. Declaring the layout

- [x] 1.1 `UIOptions.UseLayout<TLayout>()`, refusing a type without a public `(VisualNode)` constructor, and the layout's module named in the client configuration. Verify: a server test reads the option, the refusal's message, and the configuration the shell writes
- [x] 1.2 The compiler emits the layout's type as an entry module, as it does a page's. Verify: a referenced-projects test builds an app with a layout and finds its module

## 2. The server renders inside the layout

- [x] 2.1 A page under a layout is rendered as `new TLayout(page)`, and its position keys carry the layout's steps. Verify: an SSR test reads the layout's markup around the page's, and a page's server data reaches it under the layout

## 3. The browser keeps the layout mounted

- [x] 3.1 One owner for the document above the page, built from the layout, and a navigation hands it the new page. Verify: a runtime spec navigates under a layout and finds the layout's instance kept and the page's replaced, the old page's unmount before the new one's mount
- [x] 3.2 Hydration adopts the layout's markup in the server's order. Verify: a runtime spec hydrates a layout's SSR markup with no DOM rewrite
- [x] 3.3 Without a layout, a navigation patches as before. Verify: the existing navigation specs pass unchanged

## 4. The location

- [x] 4.1 `Navigator.Location`, answered by the server and the browser. Verify: a test on each side reads the location a layout built with

## 5. The dashboard

- [x] 5.1 `ConsoleLayout` holds the frame and the sidebar's state, and the screens return their content. Verify: in a browser, the sidebar opened on one screen stays open across a navigation; the README captures retaken

## 6. The documentation

- [x] 6.1 The wiki's ServerIntegration page, where `UseLayout` is declared, in its five languages, and one ledger entry citing #1129. Verify: the docs guards pass against the wiki branch
