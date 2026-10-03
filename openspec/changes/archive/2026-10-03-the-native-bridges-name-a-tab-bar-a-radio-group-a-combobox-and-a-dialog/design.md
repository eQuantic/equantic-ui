# Design

## How Flutter answers it

Flutter's `SemanticsRole` names the same containers: `tabBar` "contains tab buttons", `radioGroup` is
"a group of radio buttons", and `dialog`, `alertDialog` and `comboBox` are roles of their own. Its
`TabBar` is a container whose tabs are each a node with a selected flag, and a radio is a node in a
mutually exclusive group with a checked flag: the group is read, and each item is a stop. Nothing in
Flutter announces a tab strip as one adjustable stop. `docs/FLUTTER-PARITY.md` §9 records the
semantics tree and its bridges as SAME; what this change adds is that the tree now names the
containers with Flutter's words, and the platform words still live in one table, `NativeRole`.

## Decisions

### The group is a container, not one stop whose value is the pick

The issue offered two shapes: one stop whose value is the picked item, or a group whose items are each
a stop. The second is what the web does (a `tablist` of `tab`s, a `radiogroup` of `radio`s), what
Flutter does, and what each platform's own control does: NSTabView's `AXTabGroup` holds its tabs,
an `AXRadioGroup` holds its radio buttons, and Android's `RadioGroup` holds its `RadioButton`s. A
reader reaches every choice, hears which one is picked, and activates it with the handler a tap runs.
The first shape would hide the choices that are not picked, and UIKit's adjust gesture on it would
step the pick and say nothing, since a group has no value of its own.

### The group takes no adjust gesture and no press

The keyboard keeps its one Tab stop and its arrows, which is the ARIA tablist pattern and the reason
the vocabulary has an `Adjustable` at all. A reader's way through the same control is the items, as it
is for NSTabView, Android's RadioGroup and Flutter's tab bar, none of which offers an adjust action on
the group. `NativeRole` marks the four containers neither activatable nor adjustable, and the test
that holds every reachable control to what it advertises answers a container by the stops it holds.

### Five members, appended, in Flutter's words

`SemanticRole` is a `byte` enum a consumer compiles into its own IL, so the members go at the end,
17 to 21 (`EnumValueAbiTests`). The tab strip's role is `TabBar` rather than ARIA's `tablist`: the
vocabulary looks to Flutter when a name is missing, and Flutter's word is `tabBar`, as TalkBack's
spoken one is "tab bar". `AlertDialog` is the fifth member, beyond the handoff's seven: the web already
says `alertdialog` for a destructive confirm, and AppKit and Android have their own words for it, so a
dialog role alone would leave the Dialog component's own spec ("alertdialog role") unmet on Photon.

### The combobox is the anchor's pressable, found by where it sits

The web puts `role="combobox"` on the root of a listbox panel's anchor (`LowerAnchored`). The walk
does the same from the laid-out tree, which knows its parent: a pressable whose nearest ancestor that
is on screen is an `Anchored` with a `Listbox` panel is the combobox. Component seams are walked
through, so an anchor written as a component reaches the pressable it builds, the way the web reaches
the element it lowered to. A pressable inside an anchor that is a row stays a button, and a menu's
trigger stays a button, as on the web.

### A select-only combobox speaks each platform's drop-down

Core-AAM maps `combobox` to `AXComboBox` and `android.widget.EditText`, an editable combo box's words:
NSComboBox on the Mac, and a field TalkBack calls an "edit box" and offers to type into. Every combobox
the library builds is select-only, and the platforms each have a select-only control: NSPopUpButton,
whose `AXPopUpButton` is also Core-AAM's word for a button with `aria-haspopup` and the pair to an
option's `AXMenuItem` (#338), and `android.widget.Spinner`, which TalkBack names "drop-down list" and
which Chromium gives the web half of this very component (a combobox that is not a text field,
`kComboBoxMenuButton`). An editable combobox would take Core-AAM's pair, and nothing builds one.

### A dialog keeps Core-AAM's pair, and the measurement is recorded

`AXGroup` with the `AXApplicationDialog` subrole (and `AXApplicationAlertDialog` for the alert) is what
WebKit and Chrome expose for the web half. AppKit's own description of that pair, read back from an
`NSAccessibilityElement` in the test, is "group": the description a browser gives it is the browser's
own. The subrole is what marks the element a dialog to VoiceOver and the Accessibility Inspector; a
role description of our own would be words this framework does not ship. Android's classes are
Core-AAM's, `android.app.Dialog` and `android.app.AlertDialog`, which TalkBack's Role.java reads as
`ROLE_DIALOG` and `ROLE_ALERT_DIALOG`. UIKit has no dialog trait: a modal layer says so with
`accessibilityViewIsModal` on a container, which a flat bridge cannot set, so the row is a group's.

### The anchored panel's layer is a dialog only when its panel is one

Photon opens every anchored panel in a synthetic overlay layer, and the layer was modal by default.
The semantics walk announced it as an unnamed group in front of every open menu, select and tooltip,
and would have announced a dialog the moment a modal layer became one. The web has no layer there: its
panel sits in the page, and it says `role="dialog"` only for a panel that holds a composite. So the
realizer marks the layer modal exactly when the panel is `AnchorPanelRole.Dialog`. Nothing else on
Photon reads the flag: the pointer only ever hits registered regions.

## What stays out

- Position in a set, "tab, 2 of 3": Android's `CollectionItemInfo`, a container AppKit and UIKit can
  see, and the ancestor TalkBack expects a radio button to have (#502). The bridges are flat, so a
  container is an element beside what it holds rather than around it. Chrome maps a tablist to a
  plain `ViewGroup` because `TabWidget` makes TalkBack drop the position; #502 makes that trade again.
- UIKit's `tabBar` trait and `accessibilityViewIsModal`, both properties of a container that is not
  itself an element, which the flat iOS bridge does not build.
- A name for a `Tabs`: the component takes no label on either target, so its bar is unnamed on both.
- The menu trigger's popup: the web says `aria-haspopup="menu"` and the tree has no field for it.
