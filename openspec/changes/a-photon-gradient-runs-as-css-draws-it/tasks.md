## 1. The corner's line (#1955)

- [x] 1.1 Measure `to bottom right` and `to bottom left` on a wide and a tall box in a browser
- [x] 1.2 `GradientLine.Across`, taken by the box's gradient and the text's gradient

## 2. The mix of the stops (#1956)

- [x] 2.1 Measure a CSS gradient's and an SVG gradient's mix over nothing in a browser
- [x] 2.2 `GradientInterpolation` on `Paint`, mixed alike by `Paint.ColorAt` and the shader, the shaders regenerated
- [x] 2.3 The box's gradient, its glow and the text's gradient premultiplied, an artwork's run straight

## 3. Proof, documentation and the suites

- [x] 3.1 `AGradientRunsAsTheBrowserDrawsItTests` on the Reference backend, Metal and Vulkan, failing on main, and with
  the two most plausible wrong fixes
- [x] 3.2 The diagonal test and the shimmer goldens, looked at before and after
- [x] 3.3 A ledger entry under `docs/ledger/`
- [ ] 3.4 Every test project of the solution with the Conformance suite in full, each read by its exit code
