- **2026-11-27 · A part names or describes its nearest dialog, group, region or field**: a Task under #1550
  ([#1916](https://github.com/eQuantic/equantic-ui/issues/1916)). The family slices make a dialog's title, a field's
  label and helper and a group's heading nodes, and every nameable took its name as a string, so a title that is a
  component of the app's own named nothing. `LabelPart` and `DescriptionPart` mark the part that names, and the part
  that describes, the nearest nameable they reach: a modal layer, a dialog panel, a group, a live region, a text
  entry, a checkbox, a radio or a switch, over the string it carries. The web lists the parts' ids in
  `aria-labelledby` or `aria-describedby`, on the server's lowering and the browser's alike, and Photon names and
  describes the stop by the text under them, in `SemanticNode.Description`, which now carries a text entry's own
  description too. A node holding two fields a part could name is refused on every target in one sentence, and a
  field that names itself is never the one a title beside it names. `SemanticsParityTests` reads the cases with
  their descriptions on both targets, and the component-parity fixture holds the two web producers to one markup.
