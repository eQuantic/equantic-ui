## ADDED Requirements

### Requirement: A field may leave its label out of the drawing

A `TextInput`, a `DatePicker` or a `DateTimePicker` (its date's field) with `LabelHidden` SHALL draw no
label above its field, and the field SHALL still be named by the label for assistive technology. A page
that draws it again with `LabelHidden` changed SHALL see the change.

#### Scenario: A form that draws its own label

- **WHEN** a form draws `Text("Delivery day")` above a `DatePicker` labelled "Delivery day" with
  `LabelHidden`
- **THEN** one static text named "Delivery day" is drawn, and the field is named "Delivery day"

#### Scenario: Hidden on a redraw

- **WHEN** the form starts drawing its own label and hands the picker `LabelHidden` on the next drawing
- **THEN** the picker's label leaves the drawing
