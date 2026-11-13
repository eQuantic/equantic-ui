# Spec Delta

## MODIFIED Requirements

### Requirement: A position keeps its stateful instance

On Photon each node of the live tree SHALL hold a position, an element, and a `StatefulComponent`
SHALL keep the instance its position first mounted for as long as that position lives. A rebuild that
hands the position a node of the same runtime type and the same `Key` SHALL keep the element and its
instance, and the fresh instance's configuration SHALL reach the kept one through `AdoptConfig`. A
node of another type or another key SHALL replace the element, and its instance SHALL start fresh.
Among the children of a `Row`, a `Column`, a `Stack`, a `Grid` and a `Navigable`, a child with a key
SHALL be matched by its key wherever it moves, and a child without one by its index. A child no old
child can be, because none held its key or the one that held it is of another type, SHALL be made
without being compared with each old child (unless a key repeats among them), so replacing every
member of a list SHALL cost what building the list and letting the old one go cost.

#### Scenario: A parent rebuilds

- **WHEN** a page holds a stateful counter that was pressed three times, and the page rebuilds with a
  fresh counter whose label changed
- **THEN** the counter shows three presses and the new label

#### Scenario: Keyed rows swap

- **WHEN** a column holds two stateful rows keyed `a` and `b`, row `a` was pressed once, and the
  column rebuilds with `b` before `a`
- **THEN** the row keyed `a` still shows one press, now in second place, and `b` shows none

#### Scenario: A key inserted at the front

- **WHEN** a column holds two stateful rows keyed `a` and `b`, pressed once and twice, and the column
  rebuilds with `c` before `a`
- **THEN** the row keyed `c` is a new instance with no presses, and the row keyed `a` is the same
  instance with one press, now in second place

#### Scenario: Every key replaced

- **WHEN** a column of two stateful rows keyed `a` and `b`, both pressed, rebuilds with rows keyed `c`
  and `d`
- **THEN** both rows are new instances with no presses

#### Scenario: Every member of a long list replaced

- **WHEN** a column of 10,000 text cells keyed `a0` to `a9999` rebuilds with text cells keyed `b0` to
  `b9999`, or with rows keyed `a0` to `a9999`
- **THEN** the rebuild costs less than twenty times what the first build of the column cost, on .NET
  and on the transpiled engine

#### Scenario: The type at a position changes

- **WHEN** a position held a stateful component of one type and the rebuild puts another type there,
  then the first type back
- **THEN** the first type's state starts from its constructor again
