## Context

`TreeView` holds which nodes are open, what was loaded and where the keyboard is; the nodes are the app's. Editing
in place crosses that line both ways: the tree owns the field and its keys, the app owns what a new name means
(a file renamed on a disk, a container renamed on a server), and the children the tree loaded go stale when the
app changes them.

## Goals / Non-Goals

**Goals:** rename and create in place, a menu per node, busy nodes, and a way to read a node's children again, on
both targets and by the keyboard alone; a row whose content and measures the developer replaces without forking
the tree.

**Non-goals:** validation while typing (VS Code's "a file named X already exists" under the field), a follow-up;
selecting only a file's name without its extension; multiple selection; dragging nodes, which is #1533.

## Decisions

- **The app asks through versioned data, as `ScrollRequest` does.** `TreeEditRequest(Version, Key)` starts a
  rename of `Key`, or with `Creates` a new item under it; `TreeReloadRequest(Version, Keys)` reads the children of
  each key again. A tree is built again and again, so each version is honoured once, and the same request still in
  the tree after the person moved on changes nothing. A controller object (Flutter's `TreeViewController`) would be
  the first one a catalog component exposes; the code editor and the spreadsheet have controllers because they are
  engines, and a tree is not.
- **The callbacks return a task, and the tree derives the rest.** While `OnRenamed` or `OnCreated` runs, the node
  is busy; when it ends, the tree reads the parent's loaded children again. The developer states how to rename and
  nothing else, by the product principle. A node the app gives `Children` for, or a root, shows the app's next
  build instead, which is already how the app shows a change. A task that fails leaves the tree as it was; saying
  why is the app's (it awaits its own work and catches what it expects).
- **The field takes the label's place, in the tree's own row.** With a `RowBuilder`, the row being edited is drawn
  with the tree's own content (its icon and the field), whatever the builder draws: the builder cannot know where
  a field belongs in content it invents, and a rename that shows no field would be worse than one that shows the
  tree's.
- **Leaving the field commits.** Every file tree commits a rename when the field loses the keyboard (VS Code,
  Finder, Explorer). Enter and Escape give the keyboard back to the tree through `Navigable.FocusVersion`; leaving
  by a press elsewhere leaves it where the person put it.
- **One menu per tree.** Every row is wrapped in a `ContextTarget`; only the row whose menu is open is wrapped in
  an `Anchored`, and `MenuBuilder` is called for that node alone, when it opens. The panel, its keys and its
  choice are the `Menu`'s (`MenuPanel`), as `ContextMenu`'s are. A `ContextMenu` per row would build every row's
  items on every build.
- **The keyboard's menu goes where the keyboard is.** A composite keeps the focus on its one stop and names its
  active item to assistive tech. The web's controller starts its search for a target from the element the
  focused one's `aria-activedescendant` names; Photon's host from the region of the focused composite's active
  cell. Any composite gets it, a tree, a grid or a list of the app's own.
- **A field's keys are the field's.** The web's composite answered every keydown that bubbled to its host, so an
  arrow or a letter typed into a field inside it moved the tree instead. It now leaves a key typed into a field to
  the field, and only that: a calendar's cell a press focused still sends its keys up, as it always did. A tree
  item's press keeps the keyboard on the tree by cancelling the item's own focus, and leaves a press inside its
  field alone.
- **The instance style is the family's record.** A tree is of the list family, whose slot (`ListTheme`, #938)
  already holds the quiet fill and the chevron's colour the tree reads. The tree's measures join it, and
  `TreeView.Style` is laid over `IAppTheme.List` with `ListTheme.Merge`, as #1545 asks of every component.
- **The builder builds what follows the chevron.** The tree keeps the indentation, the chevron (a press opens a
  node whatever the row shows), the selection's fill, the focus ring and everything a reader hears, so a custom
  row is still a tree item.

## Risks / Trade-offs

- A reload replaces the loaded children only when the new ones arrive, so the old ones stay on screen meanwhile
  and nothing flickers; a node that vanished takes the keyboard's place with it, which then lands on the parent.
- A busy node is still walked by the keyboard (a reader hears it, dimmed), and refuses Enter, F2 and the menu.
