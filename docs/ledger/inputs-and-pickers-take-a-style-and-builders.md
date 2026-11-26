- **2026-11-26 · Inputs and pickers take a style of their own, and builders for what they repeat**: the inputs
  and pickers of the catalog had no `style`, so an app that wanted a field's radius or a calendar's cell size
  rebuilt the component (#1608, the first half of #1548, under the catalog customization of #1580, after the
  buttons' #1606 and the lists). Twelve components now take `style:`, mirrored by the `UI` factory and laid over the
  theme's slot with the record's `Merge`, so a member the instance sets wins and a null one stays the theme's:
  `InputTheme` for `TextInput`, `FormInput`, `SearchField`, `Select`, `FileUpload`, `TimePicker`, `DatePicker` and
  `DateTimePicker`; `SegmentedTheme` for `SegmentedControl`, `Stepper` and `CultureSwitcher`; `CalendarTheme` for
  `Calendar`. `DatePicker` and `DateTimePicker` also take `calendarStyle:` for the panel they open.
  `FormInput` hands its style to the `TextInput` it draws, `DateTimePicker` hands `style` to its date and time
  fields and `calendarStyle` to the date field's calendar, and `CultureSwitcher` hands its style to its segments.
  `Select` takes `optionBuilder` and `SegmentedControl` takes `segmentBuilder`, each a `Func<int, VisualNode>`
  (a `Select`'s index is flat across its groups): what it answers is drawn inside the part's chrome and the
  option's or segment's text stays its accessible name. Nothing is removed in behaviour; the constructors and
  factories widened and the old shapes went, as the preview rule says
  ([#1608](https://github.com/eQuantic/equantic-ui/issues/1608)). Proposed and archived through OpenSpec
  (`openspec/specs/catalog-customization`).
