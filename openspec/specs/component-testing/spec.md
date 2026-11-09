# component-testing Specification

## Purpose
How an app's own tests draw a component with no browser: through the vocabulary's live owner and the
web realizer the server uses, so a test sees the frames a browser would, acts on a control by the name
a screen reader announces, and reads what is announced in the vocabulary's words rather than ARIA's.

## Requirements

### Requirement: A test renders a component with a theme and a culture, and no browser

`eQuantic.UI.Testing` SHALL provide `ComponentTester`, which a test in an ordinary xUnit or NUnit
project constructs with a theme, a culture, a viewport width in dp, a density and a service provider,
each optional, and which depends on no test framework. Without a theme it SHALL use the SDK's
(`PhotonTheme`), and without a culture the invariant one. Every build, lowering and event handler the
tester runs SHALL see the culture as both `CultureInfo.CurrentCulture` and `CurrentUICulture`, and the
tester SHALL restore the caller's cultures when it returns.

#### Scenario: A component that reads the theme renders without one being registered

- **WHEN** a test pumps a `Select` of `One` and `Two` with `One` chosen, with a `ComponentTester`
  constructed with no argument
- **THEN** the frame draws, and announces one combo box whose value is `One`

#### Scenario: The culture a test names is the one a component formats with

- **WHEN** a test pumps a component that writes `1234.5.ToString("N1")` with a tester for `pt-BR`
- **THEN** the markup reads `1.234,5`, and `CultureInfo.CurrentCulture` after the call is the one the
  test had before it

### Requirement: A test draws frames through a live owner

`ComponentTester.Pump(root)` SHALL mount the root in a live `BuildOwner` and draw the first frame;
`Pump()` SHALL draw the next one, building only the positions a `SetState` marked. A stateful instance
SHALL be kept by its position from frame to frame, `OnMount` SHALL run once when the frame that first
built it ends, and disposing the tester SHALL take the tree down so every `OnUnmount` runs once.
`HasScheduledFrame` SHALL say whether a `SetState` asked for a frame not yet drawn.

#### Scenario: A stateful component a page builds keeps its state

- **WHEN** a page builds a counter, the test taps the counter, and pumps again
- **THEN** the frame reads `Count: 1`, where the server's one-frame lowering of the same page reads
  `Count: 0`

#### Scenario: Mount, and unmount when the test ends

- **WHEN** a test pumps a component with `OnMount` and `OnUnmount`, pumps twice more, and disposes the
  tester
- **THEN** `OnMount` ran once and `OnUnmount` ran once

### Requirement: A test acts on a control by the name a screen reader announces

`Tap(name)` SHALL invoke the click handler of the control announced by that name, and `Tap(role,
name)` the one announced with that role and name. `EnterText(name, text)` SHALL hand the text to the
change callback of the text entry announced by that name. Neither SHALL draw a frame. A name that no
control, or more than one, is announced by SHALL fail with an exception that lists what is announced,
and so SHALL a disabled control.

#### Scenario: A form typed into and submitted

- **WHEN** a test pumps a form with an email field and a button, enters `ana@example.com` into
  `Email`, taps `Sign up` and pumps
- **THEN** the frame announces a static text `Welcome, ana@example.com`

#### Scenario: A name nothing announces

- **WHEN** a test taps `Delete` on a frame that announces `Save` and `Cancel`
- **THEN** the call throws, and its message names `Save` and `Cancel`

### Requirement: A test reads what a screen reader announces, in the vocabulary's words

`ComponentTester.Semantics` SHALL list what a screen reader announces in the current frame, in reading
order, as `Announcement`s carrying a `SemanticRole`, a name, a value and the disabled, checked,
selected, expanded and current states and the heading level. It SHALL be computed from the ARIA the
web realizer lowered: the role from the `role` attribute or the element's own, the name from
`aria-labelledby`, `aria-label`, `alt`, or the text of a role named from its content. A subtree hidden
by `aria-hidden="true"`, `hidden`, an inline `display: none` or `visibility: hidden`, or an adaptive
gate that does not show at the tester's width SHALL NOT be announced. `Find(name)` and
`Find(role, name)` SHALL answer the one announcement that matches, and fail as `Tap` does otherwise.
`Html` SHALL be the markup of the current frame.

#### Scenario: A checkbox

- **WHEN** a test pumps `Checkbox(true, null, "Accept")`
- **THEN** `Semantics` holds one announcement with role `Checkbox`, name `Accept` and checked `True`

#### Scenario: The arm a width hides

- **WHEN** a test pumps an adaptive node whose compact arm says `Phone` and whose expanded arm says
  `Desk`, with a tester 1280 dp wide
- **THEN** `Semantics` announces `Desk` and not `Phone`, and `Html` holds both

### Requirement: A page's server data loads before its first frame

`PumpPageAsync(page)` SHALL call `PrefetchAsync` on every `IServerPrefetch` component the tree
reaches, one at a time and with the tester's services, mark the position that keeps each one so that
only its subtree builds again, with that instance, and do the same for any component that build
reaches for the first time, until a round reaches none; then it SHALL draw the first frame. The
instance that prefetched SHALL be the one the frame draws. A page still reaching new components after
five rounds, the server's bound, SHALL fail with an exception naming them.

#### Scenario: A page that reads a service

- **WHEN** a page's prefetch reads `675617` from a service the test registered, and the test calls
  `PumpPageAsync`
- **THEN** the first frame reads `Downloads: 675617`

#### Scenario: A stateless component that loads, inside a page

- **WHEN** a page builds a new stateless component on every build, and that component's prefetch
  loads `Title stats`
- **THEN** the first frame reads `Title stats`, where rebuilding every component after the prefetch
  hands the position a fresh instance each round and never settles

#### Scenario: A component the data brings

- **WHEN** a page builds one prefetching card per item its own prefetch loaded, and each card's
  prefetch loads its title
- **THEN** the first frame shows every card's title

#### Scenario: A page that never settles

- **WHEN** each prefetch of a page builds a new component that prefetches
- **THEN** `PumpPageAsync` throws, saying the page still asks for data after five rounds
