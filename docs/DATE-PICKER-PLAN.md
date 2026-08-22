# The date/time pickers — what the vocabulary owes them

## Why a plan and not just a component

`DatePicker`, `DateTimePicker` and `TimePicker` do not exist. The design system already specifies
one (**C15**, marked `NOT YET IN SDK · REQUEST`), the marketing site already advertises it, and the
date arithmetic on both sides is complete — so the temptation is to write the component. The survey
says otherwise: what is missing is not the component, it is three words in the vocabulary and one
decision about the keyboard. Writing the component first would have meant inventing them inside it,
where the next composite could not reuse them.

## The shape, decided

**One `Calendar`, three thin wrappers.** The month grid with its keyboard is the expensive piece and
is written once; `DatePicker` is that grid inside an `Anchored`, `DateTimePicker` adds the time
strip, and `TimePicker` needs no grid at all — it is a list of hours and minutes, which `Select`
already is. An inline `Calendar` also has its own uses (date ranges, scheduling) and is what the
`Scheduler` the site promises would be built on.

## What C15 requires, verbatim from the handoff

- `new DatePicker(selected, onChanged, min?, max?, mode: DateMode.Single | Range)`
- Mobile tier: presented in a `BottomSheet` at content height; day cells 44×44; header is
  month/year plus chevron IconButtons; the day-name row is Caption 11/700 TextMuted.
- Pointer tier: an anchored popover calendar; day hover is a SurfaceSubtle circle; the wheel pages
  months.
- Keys: **arrows** move a day · **PgUp/PgDn** a month (**+Shift** a year) · **Home/End** the week's
  bounds · **Enter** selects · **Esc** closes.
- Semantics: `grid` / `gridcell` + `aria-selected`; **day names are column headers**; the native
  side needs a Selected field (a §10 REQUEST at the time of writing).
- States: selected = Primary circle · today = 1.5dp Primary ring · outside min/max = 38%,
  non-interactive · other-month days hidden.
- A **text-entry fallback row** (DD/MM/YYYY `TextInput`) is required for keyboard and switch users,
  and stays required after the calendar ships.
- Out of scope for v1, by the spec's own words: time-of-day picking. Far-past dates (birthdays)
  must lead with the year grid.

## Slices

| # | Slice | State |
|---|---|---|
| 1 | **The grid vocabulary.** `Navigable` (the 2-D twin of `Adjustable`), `PressableRole.GridCell`, `AnchorPanelRole.Dialog`, and Photon's `SemanticRole.GridCell` + `SemanticNode.Selected` — with the Shortcut question answered | done |
| 2 | **Culture.** First-day-of-week and the day and month names as DATA, not only inside formatting | done — and the cross-pin changed the design. Deriving on each side looked safe (.NET matched bun across ten cultures) until the same comparison ran under node: four disagreements, including whether zh-CN's week starts on Sunday. So the server COMPUTES the names for the request's format culture and ships them in `__EQ_CULTURE__`, boot installs them before hydration, and the twin prefers them over Intl — which stays as the fallback for a render with no server behind it. Narrow day names are absent from both sides on purpose: .NET's `ShortestDayNames` and CLDR's `narrow` are different data, and no shared derivation works (first-character gives Chinese seven identical headers) |
| 3 | **`Calendar`.** The month grid: navigation, min/max, today, single selection. Range mode after | |
| 4 | **The three wrappers** plus the typed row, the factories and the strings | done — DatePicker (calendar in an anchored DIALOG behind a typeable field), TimePicker (a listbox of slots: a time is a sequence, so no grid), DateTimePicker (composes the two; reports only when both halves are in, bounds checked on the MOMENT). The typed row is the field itself and commits as you type. Four gaps closed on the way: no calendar/clock glyph, a culture-blind DateOnly parse on the client, a missing TimeOnly.FromDateTime, and `ToString("d")` on a compat type reaching the class's own `toString` instead of the formatter |
| 5 | **What is still owed**: a date-shaped `FieldRule` (`Range` is numeric and the `[FormModel]` generator reads only `[Required]`/`[Range]`), range selection and the year grid in the Calendar, and the mobile tier (the same calendar in a BottomSheet) | |

## The Shortcut decision (slice 1)

`Shortcut` is page-level **by design**: mounting is the subscription and the chord fires from
anywhere, which is what a command palette, an Esc-dismiss and a panel's ↑/↓ all want. A composite's
own navigation is a different thing: two inline calendars on one page would both answer the same
ArrowDown. So the grid does not use `Shortcut` for movement — `Navigable` puts a keydown handler on
its own focusable host, the way `Adjustable` already does, and focus scoping falls out of where the
listener lives. Esc and Enter stay `Shortcut`'s job inside the open popover, where page-level and
lifetime-scoped are the same thing.

## What slice 2 learned, for whoever writes the Calendar

- Ask `CalendarNames` — never `Intl`, never `CultureInfo` — from component code. The arrays are
  ALWAYS Sunday-first, indexed by `System.DayOfWeek`; the calendar rotates them by
  `FirstDayOfWeek` itself, so the rotation happens exactly once.
- The C15 mock's day row ("S M T W T F S") is CLDR *narrow*, which is not available. Use the short
  names; the cells are 44dp and the design's Caption 11/700 fits three letters.
- A cell's accessible name wants `DayNamesLong` ("Friday, July 17"), not the abbreviation — a
  screen reader reads "Fri" letter by letter.

## Two things the survey turned up that are not ours to fix here

- The site's `UiPage.Library` advertises `DatePicker` — with "Range, locales, timezones", translated
  into five languages — alongside `Tree`, `Combobox`, `Splitter`, `Calendar`, `Scheduler` and
  `Chart`, none of which exist, and `LibraryComponent` has no availability field. It is the
  "documentation nothing compiles" failure, in the shop window.
- The built-in `Icons` vocabulary (23 entries) has no calendar or clock glyph, and `Components`
  references only `Primitives`, so it cannot reach the icon packs. Either the vocabulary grows by
  two entries or the picker takes its icon as a parameter.
