## ADDED Requirements

### Requirement: A cell's id names one element on the page

On the web, the id of each cell of a table or a grid, which its row's `aria-owns` names, and of each cell of a `Navigable` grid, which the grid's `aria-activedescendant` names, SHALL be scoped by the grid's place in the tree, so that no two elements of a page share an id whatever the grids are labelled. The server's first pass and the browser's lowering SHALL write the same ids for the same tree.

#### Scenario: Two spreadsheets on one page

- **WHEN** a column holds two `Spreadsheet`s of 30 rows and 6 columns, both labelled alike
- **THEN** no id names two elements, every row's `aria-owns` names cells of its own grid, and the browser's lowering writes the same markup as the server's

#### Scenario: Two unlabelled tables

- **WHEN** a column holds two tables of 3 rows and 2 columns with no label
- **THEN** no id names two elements, and the browser's lowering writes the same markup as the server's

#### Scenario: Two calendars of one month

- **WHEN** a column holds two `Calendar`s of July 2026
- **THEN** no id names two elements, and the browser's lowering writes the same markup as the server's

#### Scenario: A sheet hydrates with the ids the server wrote

- **WHEN** the dashboard sample's `/sheet` is served and then hydrated in a browser
- **THEN** its cells' ids are scoped alike in the server's markup and in the hydrated page, and each names one element
