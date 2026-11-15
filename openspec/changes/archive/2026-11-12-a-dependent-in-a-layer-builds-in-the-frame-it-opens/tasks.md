# Tasks

## 1. The fix

- [x] 1.1 `BuildOwner` knows which layer the realizer is showing (`EnterLayer`, `ExitLayer`), and a dependent marked meanwhile goes to the list `BuildDeferred` reads
- [x] 1.2 `BuildDeferred` walks its list by index, and a dependent marked while it builds joins the list behind the position building, by depth
- [x] 1.3 Pins in `InheritedComponentTests`: a value inside a layer changed by the layer's parent, and one changed by a holder inside the layer while `BuildDeferred` builds it, and a reader under a layer inside the layer, built by its own layer and not the outer one; each failing on `main`, the second on a fix that leaves the list being built alone, the third on one that builds every dependent in the outer layer

## 2. Documentation

- [x] 2.1 One ledger entry citing the issue, as a file of its own under `docs/ledger/`
