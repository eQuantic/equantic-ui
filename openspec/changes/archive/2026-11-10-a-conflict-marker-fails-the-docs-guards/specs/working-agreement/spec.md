## ADDED Requirements

### Requirement: No page holds a conflict a merge left

The docs guards SHALL fail when a page of the wiki, in any of its languages, or a Markdown file git tracks in
this repository holds a git conflict marker, naming the file and the line: `<<<<<<<`, `|||||||`, `=======` or
`>>>>>>>`, alone on its line or followed by a space. A line of seven `=` SHALL count as a marker only inside a
conflict another marker opened, under a blank line or at the start of a page, so a title's Setext underline
never fails.

#### Scenario: A conflict committed into a wiki page

- **WHEN** the guards read the wiki at the commit before d7728c2, whose `Upgrading.md` holds a conflict
- **THEN** they fail naming `Upgrading.md` and its markers' lines, where every docs guard passed

#### Scenario: A title's underline

- **WHEN** a page underlines a title with `=======` on the line under it
- **THEN** the guards pass
