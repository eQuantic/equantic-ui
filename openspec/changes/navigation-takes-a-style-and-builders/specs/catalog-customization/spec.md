## ADDED Requirements

### Requirement: A navigation component takes its family's record as its style

Each of `Tabs`, `AppBar`, `BottomNavigation`, `NavigationRail`, `Breadcrumb`, `Pagination` and `PageIndicator`
SHALL take `style`, a `NavigationTheme`, as a constructor parameter its factory mirrors. The component SHALL lay
the record over its theme's navigation slot with the record's `Merge`, so a member the instance sets wins and a
member it leaves null is the theme's, and every part it draws SHALL read the merged record. With no style, the
output SHALL be what it was.

#### Scenario: A taller tab strip

- **WHEN** a theme's navigation slot sets `TabHeight = 48` and a `Tabs` is given
  `style: new NavigationTheme { TabHeight = 56 }`
- **THEN** that strip is 56 high, on the web and on Photon, and every other strip stays 48

#### Scenario: A rail without its trailing edge keeps the theme's other members

- **WHEN** a theme's navigation slot sets `RailBorderWidth = 1` and `RailWidth = 88`, and a `NavigationRail` is
  given `style: new NavigationTheme { RailBorderWidth = 0 }`
- **THEN** that rail draws no trailing edge and is still 88 wide

#### Scenario: A pagination's style reaches its page cells

- **WHEN** a `Pagination` of five pages is given `style: new NavigationTheme { PageCellSize = 40 }`
- **THEN** every page number's cell and the ellipsis' are 40 by 40, not only the gaps between them

#### Scenario: Breadcrumb, Pagination and PageIndicator are reached by their factories

- **WHEN** a page that imports the declarative surface writes `Breadcrumb(crumbs, style: s)`,
  `Pagination(70, 3, onChanged, style: s)` and `PageIndicator(5, 1, style: s)`
- **THEN** each builds the node `new` builds with the same arguments

### Requirement: A tab strip, a destination bar, a rail and a breadcrumb draw their parts with a builder

`Tabs` SHALL take `tabBuilder`, `BottomNavigation` and `NavigationRail` SHALL take `destinationBuilder`, and
`Breadcrumb` SHALL take `crumbBuilder`, each a `Func<int, VisualNode>` over the part's index in the list the
component was given. The component SHALL draw the builder's node as the part's content inside the part's own
chrome (a tab's indicator and pressed fill, a destination's pill with its glyph, badge and selection, a
breadcrumb's separators and its ancestors' links), and SHALL hand that content the colour and the type the
part's own text was drawn with. The part's text SHALL stay its accessible name. With no builder, the part SHALL
draw its text as before.

#### Scenario: Tabs drawn by a builder

- **WHEN** a `Tabs` of three labels is given a `tabBuilder` that answers a row with an icon and the label's text
- **THEN** each tab shows that row above the indicator, the selected tab keeps its indicator, each tab's
  accessible name is its label, and pressing the second calls `onSelect(1)`

#### Scenario: A builder's text takes the part's look

- **WHEN** a `Tabs` is given `tabBuilder: i => Text(labels[i])`, a text that names no colour and no type
- **THEN** the selected tab's text is drawn in the selected tab's colour and weight, and the others in the
  inactive colour and weight, as the strip with no builder draws them

#### Scenario: A destination keeps its pill

- **WHEN** a `BottomNavigation` or a `NavigationRail` of three `NavItem`s is given a `destinationBuilder`
- **THEN** each destination shows its glyph in the pill and the builder's node under it, the selected one keeps
  its pill's fill, and each destination's accessible name is its item's `Label`

#### Scenario: Crumbs drawn by a builder keep their links

- **WHEN** a `Breadcrumb` of `Crumb("Home", "/")`, `Crumb("Payments", "/payments")` and `Crumb("Refund 42")` is
  given a `crumbBuilder` that answers an icon for the first crumb and a text for the others
- **THEN** the separators still stand between the crumbs, the first two are links to `/` and `/payments` named
  `Home` and `Payments`, and the last is not a link
