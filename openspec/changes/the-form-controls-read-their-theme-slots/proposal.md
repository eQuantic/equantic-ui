# Proposal

Closes #1242. This is the second slice of #938 (per-component theme slots on `IAppTheme`) and builds on
the mechanism E6a set: the change `a-theme-slot-reaches-a-component-in-order` and the capability
`theme-slots`.

## Why

E6a gave the theme a control ladder and a Button slot. The form controls still write their look in
their own source:
- the icon button's circle and its tonal press at 24%;
- the field's 14dp padding and its 2dp focus border;
- the checkbox's 22dp box;
- the switch's 52 by 32 track;
- the slider's 4dp track;
- the segmented control's 3dp lip;
- the chip's label at 13 over 16, semi-bold.

A theme cannot reach any of these. `MaterialTheme` repaints every colour and every radius, and still
draws a Photon text field, checkbox, switch and chip in Material's colours.

## What Changes

**Five slots**, one record per family, every member nullable and data only, each a default member of
`IAppTheme` that answers null:

| Slot | Components it styles |
|---|---|
| `IconButtonTheme? IconButton` | IconButton |
| `InputTheme? Input` | TextInput, SearchField, Select, TimePicker |
| `SelectionControlsTheme? SelectionControls` | Checkbox, RadioGroup, Switch, Slider |
| `SegmentedTheme? Segmented` | SegmentedControl, Stepper |
| `ChipTheme? Chip` | Chip |

**What moves into them**, what each component wrote that is look:
- the numbers: heights, paddings, gaps, the sizes of a box, a dot, a track and a thumb, border widths
  and radii;
- the elevations;
- the label's type;
- the colours the component chose itself: a role other than its variant's five, and an opacity it
  applied.

A colour taken straight from `Colors(variant)` stays there, because it is already the theme's. A
state fill the component derives from another fill (a midpoint toward the border) stays derived, from
the slot's fill. Behaviour stays in the component: the switch's release threshold, the slider's step
and the picker's panel height.

**The order is E6a's.** A ladder number goes through `ControlMetrics`, with the family's own
`Controls` ladder as its third argument: the family slot, then the theme's `Controls`, then `Sizing`. A
value outside the ladder reads `slot?.X ?? today's constant`. A selection control varies by density
alone, not by size, so a new shared record `DensityValue { Comfortable, Compact }` holds those values,
and `ControlMetrics.AtDensity` reads one at `context.Density`.

**Where the defaults live:**
- `PhotonTheme` fills the five slots with today's constants, so every pinned output stays byte for
  byte.
- `MaterialTheme` fills them from Material 3's component specifications: the 40dp icon button, the
  56dp text field with its 1dp outline and 2dp focus outline, the 18dp checkbox, the 20dp radio, the
  52 by 32 switch, the 40dp segmented button, and the 32dp chip with its label in Label large.
- `theme.With(…)` takes one parameter per new slot, after E6a's two.

The plan's table of `docs/ELEMENT-TREE-PLAN.md` §3.10 called the selection slot `Selection`. That
name is already taken on `IAppTheme` by the colour of selected text, so the slot is
`SelectionControls`, and the table changes with it.

## Parts reached

- **The vocabulary:** the five records, `DensityValue`, five default members of `IAppTheme`,
  `OverriddenTheme` and `AppThemeExtensions.With`.
- **The components:** the twelve this change names.
- **The two themes:** `PhotonTheme` and `MaterialTheme`.
- **The crossing:** the slots cross to the browser through E6a's generic slot writer, and each gets a
  TypeScript mirror in `theme-slots.ts`, a default in `interface-defaults.ts` and an export.

The public surface grows by the records and the members. The developer surface does not move.

**A change in what the browser receives.** A page served with `MaterialTheme` now draws its form
controls at Material's sizes. That is the point of the change, and a theme that names no slot is not
affected.
