- **2026-10-30 · A container sees through a component to what only it can place**: three Bugs under #166
  ([#701](https://github.com/eQuantic/equantic-ui/issues/701), [#736](https://github.com/eQuantic/equantic-ui/issues/736),
  [#738](https://github.com/eQuantic/equantic-ui/issues/738)). A component is what it builds, and a line or a grid
  asked the component itself whether it was a Spacer, a Flexible or an AdaptiveNode, so the same tree laid out
  differently with a component around a node: on Photon a built Spacer took no space, and on both targets an arm's
  alignment and span were dropped. Photon now reads what a component built from the element its position already
  holds, and on the web a container's rule waits for what the component builds, so nothing builds twice. A
  bridge's theme and type scale are also the ambient ones for the length of its render, so a stateless component
  under it hydrates in the colours the server drew. Proposed and archived through OpenSpec (`adaptive-layout`,
  `hydration-contract`).
