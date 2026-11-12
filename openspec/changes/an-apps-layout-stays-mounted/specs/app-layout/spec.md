## ADDED Requirements

### Requirement: An app declares one layout around its pages

`UIOptions.UseLayout<TLayout>()` SHALL declare a component every page is rendered inside, on the
server and in the browser. `TLayout` SHALL have a public constructor whose only parameter is the page,
a `VisualNode`, and `UseLayout` SHALL refuse a type without one when it is called, with a message
naming the type. Without a call to `UseLayout`, a page SHALL be rendered as it is today.

#### Scenario: A page is rendered inside the layout

- **WHEN** an app calls `UseLayout<ConsoleLayout>()` and a request reaches the page at `/charts`
- **THEN** the served markup is `new ConsoleLayout(chartsPage)` lowered, with the page's markup inside
  the layout's

#### Scenario: A type that cannot hold a page is refused

- **WHEN** an app calls `UseLayout<T>()` with a component that has no public constructor taking one
  `VisualNode`
- **THEN** the call throws, and the message names `T` and the constructor it needs

### Requirement: The layout stays mounted across client navigations

In the browser, the document SHALL keep one owner above the page while a layout is declared. A
client navigation SHALL hand the mounted layout the new page: the layout's element SHALL update in
place and keep its instance, and the page's element SHALL be replaced under it. The old page's
components SHALL unmount before the new page's mount.

#### Scenario: The sidebar keeps its state

- **WHEN** a layout holds the open state of a sidebar, the sidebar is opened, and a link to another
  page is followed
- **THEN** the sidebar is still open on the new page, and the layout's instance is the one that
  opened it

#### Scenario: The old page leaves first

- **WHEN** a navigation replaces a page whose component unmounts with a page whose component mounts
- **THEN** the old component's `OnUnmount` runs before the new component's `OnMount`

#### Scenario: An app without a layout

- **WHEN** an app declares no layout and a link is followed
- **THEN** the new page is patched over the old one position by position, as before this change

### Requirement: A layout reads the location it shows

`Navigator.Of(context).Location` SHALL answer the path of the page being shown as the app writes it,
with no query, no base and no language segment: the request's path on the server, and the address
bar's in the browser, updated before the layout builds for a navigation.

#### Scenario: A page under a language segment

- **WHEN** an app declares `UseCultureRoutes("en", "pt-BR")` and the page at `/charts` is served at
  `/pt-BR/charts`
- **THEN** `Navigator.Of(context).Location` answers `/charts` on the server and in the browser

#### Scenario: The active destination follows the page

- **WHEN** a layout marks the sidebar entry whose href equals `Navigator.Of(context).Location`, and the
  reader navigates from `/charts` to `/table`
- **THEN** the server's markup for `/charts` and the browser after the navigation each mark the entry
  of the page shown
