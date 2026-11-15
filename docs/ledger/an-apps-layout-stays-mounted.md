- **2026-11-12 · An app's layout stays mounted across client navigations** (#1129, E3c of #1093):
  `UseLayout<TLayout>()` declares, once in `Program.cs`, a component every page is rendered inside,
  after Blazor's `DefaultLayout` and go_router's `ShellRoute`. The browser keeps one owner for the
  document above the page and builds the layout once around a slot the page stands in, so a navigation
  keeps the layout's instance and its state and replaces only the page, the old page unmounting before
  the new one mounts. The server renders the same tree measured from the layout, so every position key
  holds on both sides, and eqc reads the `UseLayout` call to bundle the layout as an entry module.
  `Navigator.Of(context).Location` answers the page's path as the app writes it, without the language
  segment, and the dashboard sample's frame moved out of its 22 screens into `ConsoleLayout`
  *([#1129](https://github.com/eQuantic/equantic-ui/issues/1129))*.
