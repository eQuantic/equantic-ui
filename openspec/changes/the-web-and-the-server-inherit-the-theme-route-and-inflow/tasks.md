## 1. The lookup crosses to the browser

- [x] 1.1 An IR strategy lowers `DependOn<T>()` and `Find<T>()` with `T`'s twin, and the runtime's position looks it up by `instanceof`
- [x] 1.2 A spec reads an app's inherited value in the browser, and a change rebuilds its dependent alone

## 2. The route is the frame's

- [x] 2.1 `ComponentContext` takes the frame's route; a position reads it through its owner, which keeps the route's readers apart from the frame's
- [x] 2.2 The server hands its request's route to the owner it draws with and registers `RouteValues` per request; `RouteValues.Current` goes
- [x] 2.3 The browser hands the router's route to its page context, and a position reads its owner's; the runtime's ambient goes
- [x] 2.4 An escape-hatch page's bridges are drawn with its route
- [x] 2.5 A navigation within one page type rebuilds what read the route and nothing else, on both sides

## 3. InFlow and the theme

- [x] 3.1 `InFlow.Current` goes: the web visitor builds a component it expands without a position with its own flag
- [ ] 3.2 `Theme`, the vocabulary's inherited theme, and `context.Theme` reads the nearest one above, or the frame's

## 4. Proof and documentation

- [ ] 4.1 The SSR fixtures and the hydration contract pass unchanged
- [ ] 4.2 The suites, through the lock for Compiler and Conformance, and the e2e suite in a real browser before and after
- [ ] 4.3 The wiki's examples in five languages and the Upgrading page; `docs/ELEMENT-TREE-PLAN.md` 3.5's rows; one ledger entry citing #1395
