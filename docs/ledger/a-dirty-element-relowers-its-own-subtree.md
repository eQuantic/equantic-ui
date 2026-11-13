- **2026-11-12 · A dirty element in the browser lowers its own subtree**: a nested component's `setState`
  built only its own element, but the page then lowered all of itself and the reconciler read the whole
  page. Each component's position now keeps what it lowered to and what it lowered with, and a render the
  page's owners asked for lowers the components that built alone, does again what their parents did to
  their root (refusing what it cannot say exactly), and patches only the DOM they drew. A component a
  parent read or walked into is lowered with the nearest component above it, the page in the end. The DOM a
  patch leaves is the DOM a whole lowering leaves ([#1455](https://github.com/eQuantic/equantic-ui/issues/1455),
  E3b-3 of [#1128](https://github.com/eQuantic/equantic-ui/issues/1128)). Proposed and archived through
  OpenSpec (`openspec/specs/element-tree`).
