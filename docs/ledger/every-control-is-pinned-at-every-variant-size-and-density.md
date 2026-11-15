- **2026-11-11 · Every shipped control is pinned at every variant, size and density**: the variant
  matrix of #294's production-ready line. The components are derived from the build output, through a
  catalog the semantics parity fixture (#339) now reads too. Each is drawn at every `Variant` and
  `SizeVariant` it accepts (through its widest constructor, or a property an author sets) and at both
  densities: 63 controls, more than 400 cells. Every cell is pinned three ways: the markup and the
  atomic rules the web lowers, what a screen reader hears, and every box Photon lays out. The pins are
  rewritten by `EQ_UPDATE_VARIANT_MATRIX=1`. The 105 cells that cannot exist are a baseline that may
  only shrink, each with its reason: a control with no variant or no size, and `TextInput` at `Small`,
  which it refuses. A new control joins with no edit to the test. To read the rules, `ComponentTester`
  gained `Css`, the atomic stylesheet its frame is written against, which an app's tests can read too.
  ([#1247](https://github.com/eQuantic/equantic-ui/issues/1247)). Proposed through OpenSpec, in
  `component-testing`.
