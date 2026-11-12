- **2026-11-11 · A component rebuilds when a value it depends on changes, and nothing else does**:
  E5's core on the element engine Photon runs. `InheritedComponent` hands its subtree a value, a
  component reads the nearest one through `context.DependOn<T>()` (or `Find<T>()` once), and when a
  parent's build places a value whose `UpdateShouldNotify` answers true, the components that read it
  build again in the same frame, a stateful one hearing it first in `OnDependenciesChanged()`. Each
  component position has a context of its own, which reads the frame's theme, density and type scale
  from its owner and registers its readers, so a density switch on Photon builds what reads the
  density instead of every component, and `InFlow` is read from the tree. `Navigator.Of(context)`
  sends a destination to the host of the component's tree, and the static navigator goes. The web and
  the server answer null until their element trees (#1395)
  ([#1095](https://github.com/eQuantic/equantic-ui/issues/1095)). Proposed through OpenSpec
  (`openspec/specs/element-tree`).
