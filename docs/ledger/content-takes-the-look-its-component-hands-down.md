- **2026-11-17 · Content takes the look its component hands down, and a string is a node**: a box
  could hand its text and icons a colour (`Foreground`) and nothing else, so a component that took
  its content as a node could not say the label is a `Label`, one line, or the icon 16 dp, and a
  bare string was no node. `BoxStyle.Content`, a `ContentStyle` (role, type, line limit, alignment,
  icon size, merged member by member), is what a `Text` and an `Icon` take when they name none; their
  role, line limit, alignment and size are optional and fall back to `BodyL`, no limit, start and
  24, so existing trees draw as before. A `string` converts implicitly to a node, so
  `Column(children: ["Title"])` works. A reader reads `text.Under(content)` and
  `icon.SizeUnder(content)`, and Email refuses a line limit a box states
  ([#1577](https://github.com/eQuantic/equantic-ui/issues/1577)). Proposed and archived through
  OpenSpec (`openspec/specs/content-defaults`).
