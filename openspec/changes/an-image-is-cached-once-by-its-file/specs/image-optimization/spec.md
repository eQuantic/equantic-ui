## ADDED Requirements

### Requirement: The optimizer's cache is keyed by the file and bounded

The optimizer SHALL cache one copy per source file, width, quality and format, the file named by the path
it resolves to under the web root, whatever the url's spelling. A request's quality SHALL be rounded to
the nearest of `Qualities`, and `DefaultQuality` SHALL be one of them, or the app SHALL stop at start. The
cache SHALL keep at most `MaxCacheSize` bytes on disk, removing the oldest copies first.

#### Scenario: Two spellings of one file

- **WHEN** `/images/test.jpg` and `//images/test.jpg` are asked at one width
- **THEN** the cache holds one copy

#### Scenario: Qualities that round alike

- **WHEN** one file is asked at `q` 74, 75 and 76
- **THEN** the cache holds one copy, at quality 75
