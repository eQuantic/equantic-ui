- **2026-11-09 · An app tests a component without a browser**: a new package, `eQuantic.UI.Testing`,
  which an ordinary xUnit, NUnit or MSTest project references. Its `ComponentTester` draws frames
  through the vocabulary's own `BuildOwner`, a live one like Photon's, and lowers each through the
  web realizer the server uses, so a stateful instance keeps its state across frames, `OnMount` runs
  once, and the markup is the server's. A test acts on a control by the name a screen reader
  announces (`Tap`, `EnterText`) and reads what is announced as `Announcement`s that speak
  `SemanticRole`, never an ARIA string; `PumpPageAsync` loads a page's server data first, marking
  only the position that loaded. Underneath, the web realizer gained `LowerFrame`, which lowers a
  frame of a live tree from its positions (the first step of E4, its first frame the server's markup
  byte for byte save hydration's unmeasured mark, its public form E4's to decide), the style sink
  `HidesAt`, the element `VisitChildren`, and `ComponentElement.MarkNeedsBuild` made public, as
  Flutter's is. Neither engine addition is a break: `Element`'s constructor is `private protected`,
  so nothing outside the engine derives from it. The web suite's semantics tests read through the
  package, and two of them got stronger in the move
  ([#1245](https://github.com/eQuantic/equantic-ui/issues/1245)).
