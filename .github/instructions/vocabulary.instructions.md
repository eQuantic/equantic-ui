---
applyTo: "src/eQuantic.UI.Primitives/**,src/eQuantic.UI.Components/**,src/eQuantic.UI.Charts/**"
---
# The vocabulary and the shared components

Components are written once, here, and each realizer lowers them to its target. Flag, as a defect:

- A target's word in a name: would it exist if the web did not? Pressable, not Button; TextRun.Destination, not Href. HtmlElement, the web's escape hatch, is the one place that mirrors the DOM.
- A platform's shape in the API: a CSS string, a pixel value where a token (ColorToken, Space, TypeRole, EdgeInsets) exists, or a platform enum.
- A property added to a StatefulComponent without its line in AdoptConfig: a parent's later rebuild reaches the component only through it, and the property silently keeps its first value (AdoptConfigCoverageTests).
- A factory that is not named exactly like its type, overloads, or does not mirror the widest constructor parameter for parameter (or the one elected with [UiFactory]); containers take a trailing children.
- A colour, size or surface that departs from the design handoff in docs/design/, or a consumer's design reshaping the SDK's ladder.
- A string a person reads hardcoded, an accessibility label included.
- A component tested on one target only, or a state (pressed, focus, selection) proved by a golden alone.
- A new value in a vocabulary enum (SemanticRole, NodeKind and their siblings) inserted or renumbered: they are append-only (EnumValueAbiTests).
- A compatibility member kept beside a changed one while the version is a preview.
