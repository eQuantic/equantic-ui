- **2026-11-11 · Navigation and collections read their theme slots, and the document's style is one**:
  six slots join `IAppTheme`: `Navigation` (Tabs, BottomNavigation, NavigationRail, AppBar, Breadcrumb,
  Pagination and PageIndicator), `List` (ListItem, List, Divider, Accordion and SwipeableRow), `Table`
  (Table and DataTable), `Badge` (Badge, Avatar and EmptyState), `Calendar` (Calendar and DatePicker)
  and `Markdown`. What each of the twenty components wrote that is look moved into its family's slot,
  and `MarkdownStyle` became `MarkdownTheme`, which a document's own `Style` still comes ahead of, member
  by member. `PhotonTheme` fills the six with what the components drew, so its pins are byte for byte.
  A colour that is one of its roles it leaves null, the Button's outline and quiet fill included, so the
  component reads the role itself and a theme that recolours the role recolours it. `MaterialTheme`
  fills the four Material 3 specifies, so a page under it now draws an 80dp
  navigation bar of 64 by 32 indicators, a 64dp top app bar, 56dp list items, a 6dp small badge and
  40dp days. With E6a to E6c, every family of the plan's table has its slot, which is #938's
  done-when ([#1244](https://github.com/eQuantic/equantic-ui/issues/1244)). Proposed and archived
  through OpenSpec, in `theme-slots`.
