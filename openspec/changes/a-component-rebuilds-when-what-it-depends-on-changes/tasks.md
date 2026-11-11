# Tasks

## 1. Inherited values

- [x] 1.1 `InheritedComponent`, its visitor method answered by every pass as its child, and `ChildSlots`' arm
- [x] 1.2 `InheritedElement`, the nearest inherited position on each element, `DependOn` and `Find` by a predicate, the dependents marked into the same frame. Verify: `InheritedComponentTests`, and the case fails with the notification removed and with the dependent waiting for the next frame
- [x] 1.3 `OnDependenciesChanged` before the build a dependency caused. Verify: the case fails without the call
- [x] 1.4 The engine passes `ElementEngineTranspilesTests` with the new element in it

## 2. The context of a position

- [x] 2.1 A context per component position, reading the frame's values from the owner and registering their readers, and a frame with new values marks them. Verify: the density case fails without the mark
- [x] 2.2 `InFlow` read from the position, and the engine arms no ambient channel. Verify: the case fails reading the ambient one
- [x] 2.3 `PhotonHost.Density` asks for a frame instead of rebuilding every component
- [x] 2.4 `Navigator.Of(context)`, the owner's handler, and the static navigator gone. Verify: the navigator case

## 3. The proof

- [ ] 3.1 The web, native engine, component testing and server suites, and the compiler's and conformance suites
- [ ] 3.2 The runtime's twin of the node, the context's `dependOn` and `find`, and `Navigator.of`, with the runtime's own suites (subsection 2's commit)
- [ ] 3.3 Photon: a density switch on the desktop sample rebuilds what reads it, measured with the perf harness before and after

## 4. Documentation

- [ ] 4.1 One entry for the ledger citing #1095, as a file of its own under `docs/ledger/`
- [ ] 4.2 The wiki: the components page's inherited values and the navigator, in five languages, and the Upgrading page's break
