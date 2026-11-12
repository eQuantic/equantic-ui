## Context

The issue offers two ways: the README's head in Markdown, which GitHub then draws left-aligned, or a readme of the packages'
own. The second keeps the page visitors see first as it is.

## How Flutter answers it

pub.dev reads Markdown, and a package's README is written for it. Here the same text has two readers with different rules.

## Decisions

- **A second head, not a converter.** The head is a handful of lines that change rarely. An HTML to Markdown converter would
  be code in an inline task, a test and a script, for six tags. The file is plain, and a test that fails on any difference of
  link, picture or word keeps it from drifting.

## Risks / Trade-offs

- A change to the head is made in two files, and the test says so.
