## ADDED Requirements

### Requirement: The browser keeps a node's state on its position

In the browser, what the runtime keeps for a node between renders SHALL belong to the node's position and
SHALL leave the tree with it. This covers a scroll view's applied offset, its last honoured request, its
last reported viewport and its observer; a table viewport's last report; an `InView`'s observer and last
report; a filling canvas's observer; and a code surface's last revealed version. A node that takes a
position another node held SHALL start with its own state, even when the reconciler draws it on the
other node's DOM element. A node that keeps its position across a render SHALL keep its state.

#### Scenario: A scroll view at another's place on the same DOM element

- **WHEN** a scroll view honoured a request of version 1 and was scrolled to 300, and a scroll view at
  another position is drawn on the same DOM element with a request of version 1 to 40
- **THEN** the element is scrolled to 40

#### Scenario: A scroll view at another's place that declares no offset

- **WHEN** a scroll view was scrolled to 300, and a scroll view at another position that declares no
  offset is drawn on the same DOM element
- **THEN** the element is scrolled to 0

#### Scenario: A scroll view keeps its position

- **WHEN** a scroll view was scrolled to 300 and its position draws it again
- **THEN** it is still at 300

#### Scenario: A scroll view leaves the tree

- **WHEN** a scroll view that reports its viewport leaves the tree
- **THEN** the observer watching its element is disconnected

#### Scenario: A code surface at another's place

- **WHEN** a code surface's model moved its caret since the last render, and a surface with another key
  takes its place over a model whose caret has moved too
- **THEN** the caret of the surface that kept its position is brought into view, and the replacing
  surface's caret is not

### Requirement: The browser names a node by its position's id

The attributes the browser's after-passes and focus requests find a node by (`data-eq-code`,
`data-eq-sheet`, `data-eq-table`, `data-eq-scroll`, `data-eq-canvas-fill`, `data-eq-inview`,
`data-eq-focus-scope`) SHALL carry the integer id of the node's position, and so SHALL the ids and
reconciler keys formed from a node's identity. The id SHALL stay the same while the position lives,
and a node at another's place SHALL carry a new one.

#### Scenario: A code surface keeps its id

- **WHEN** a code surface is lowered, lowered again from the position that keeps it, and then replaced
  by a surface with another key
- **THEN** the first two carry the same `data-eq-code`, which is their position's id, and the third
  carries another

### Requirement: The trees of a document count their ids together

An owner SHALL take its positions' ids, and their parts' ids, from the count it is handed, and from a
count of its own when it is handed none. The browser SHALL hand every owner of a document the same count,
so no two positions of the document share an id.

#### Scenario: Two trees share a count

- **WHEN** a live owner and two structural owners handed one count each build a tree
- **THEN** no two positions or parts of the three trees share an id, and an owner handed no count
  numbers its root 1
