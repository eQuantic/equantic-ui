- **2026-11-16 · Any subtree hears the raw pointer**: `Listener` wraps any node and hears the pointer over it in
  its own coordinates, Flutter's six callbacks (down, move, up, cancel, hover, scroll) with a `PointerEvent` that
  carries the kind, the buttons (`PointerButtons`, Flutter's constants) and the keys held, a press followed to its
  up or its cancel wherever it goes. `HitTestBehavior` says where it hears, Flutter's rule: `DeferToChild` where
  its child draws or answers, `Opaque` anywhere in its box hiding the listeners behind it, `Translucent` anywhere
  letting them hear too. The rule is written for the web, over the browser's own hit test, and for Photon, over
  the frame's paint, and one fixture holds the cases both run. A listener only listens: what it holds still
  presses and scrolls. The dashboard's `/files` screen resizes its tree by the gap beside it
  ([#1562](https://github.com/eQuantic/equantic-ui/issues/1562)). Proposed and archived through OpenSpec
  (`openspec/specs/raw-pointer`).
