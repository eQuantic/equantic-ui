- **2026-11-12 · The web and the server read the theme, the route and InFlow from the tree**: a
  component's `DependOn<T>()` and `Find<T>()` cross to the browser with the type they name, so an app's
  inherited value is read there as on Photon and the server. The route is the frame's: a component reads
  it from its context, its owner keeps the route's readers apart, and a navigation that keeps the page
  type builds what read the route and nothing else, in the browser and on .NET. `RouteValues.Current`
  goes, and a prefetch resolves the request's route from the services it is handed. `InFlow.Current`
  goes, since a position answers from the tree. `new Theme(value, child)` hands a subtree another theme,
  read by the components below it from `context.Theme`, and a message draws that subtree in it too.
  Two ambient channels leave the list that may only shrink, and the SSR fixtures are unchanged
  ([#1395](https://github.com/eQuantic/equantic-ui/issues/1395), under
  [#937](https://github.com/eQuantic/equantic-ui/issues/937)). Proposed and archived through OpenSpec
  (`openspec/specs/element-tree`).
