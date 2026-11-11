# Proposal

Closes #1095 (E5), a sub-issue of #937 (the element tree). Its web and server part is #1395 (E5b).

## Why

A tree built with an element per position still rebuilt by broadcast: a density switch on Photon
rebuilt every component, and nothing let a subtree hand its components a value of its own. The plan
(`docs/ELEMENT-TREE-PLAN.md`, 3.3 and 3.4) makes the context a position and adds inherited values, so
a change rebuilds what read it and nothing else, which E6's theme slots (#938) build on.

## What Changes

- `InheritedComponent`, a vocabulary node an app subclasses: it holds a value and one child, and
  answers `UpdateShouldNotify(old)`. Every target draws its child as if it were not there.
- `context.DependOn<T>()` finds the nearest `T` above a component and registers the component as its
  dependent. `context.Find<T>()` reads it once. When a parent's build hands the value's position a new
  node whose `UpdateShouldNotify` answers true, the dependents build again in the same frame, and the
  subtree between them does not.
- `StatefulComponent.OnDependenciesChanged()` runs before a dependent builds again because of it.
- A component position has a context of its own for as long as it lives. It reads the frame's theme,
  density and type scale from its owner and registers the position as their reader, so a frame that
  hands a new density rebuilds what read it. `PhotonHost.Density` no longer rebuilds every component.
  `context.InFlow` is read from the tree.
- `Navigator.Of(context).Go(destination)` sends a destination to the host of the component's tree.
  The static `Navigator.Go` and `Navigator.Handler`, where the last host to start took every request,
  go.

The parts reached are the vocabulary and the element engine (`eQuantic.UI.Primitives`), every visitor
of the vocabulary (the web realizer, the email walk, Photon's layout, paint and semantics), the Photon
host, and the runtime's twin of the node, the context and the navigator. The public surface grows
(`InheritedComponent`, `InheritedElement`, `DependOn`, `Find`, `OnDependenciesChanged`,
`Navigator.Of`, `BuildOwner.NavigationRequested`, a visitor method) and loses the static navigator.

A break: `Navigator.Go(destination)` becomes `Navigator.Of(context).Go(destination)`, from a component
that holds its context. A visitor of the vocabulary written outside the SDK implements
`Visit(InheritedComponent, TState)`.

On the web and the server, which build with no element per position until #1395, a context answers
`DependOn` and `Find` with null, and the theme, the route and `InFlow` the way it does today.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `element-tree`: inherited values, a context per position, and a navigator per tree.
