- **2026-11-11 · A code block's problem of no width draws nothing**: the server measures every
  column of a code block as 0 wide, so a problem's wave was a drawing of no width, which `Drawing`
  refuses, and the dashboard sample's `/code` lost its server markup on every request.
  `CodeBlock.Marks` now skips a mark of no width, the hint's dots aside, and the browser draws the
  block again with its waves ([#1367](https://github.com/eQuantic/equantic-ui/issues/1367)).
  Proposed and archived through OpenSpec (`openspec/specs/code-diagnostics`).
