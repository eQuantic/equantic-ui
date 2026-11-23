## ADDED Requirements

### Requirement: The keyboard follows the field's kind

A `TextInputSession` SHALL tell a shell the kind of the field being edited, and SHALL start the
keyboard over when the caret moves to a field of another kind. The iOS shell SHALL open
`UIKeyboardType.EmailAddress`, `PhonePad`, `Url`, `NumberPad` and `DecimalPad` for `Email`, `Phone`,
`Url`, `Number` and `Decimal`, with the matching `UITextContentType` for the first three; the Android
shell SHALL describe `ClassText | TextVariationEmailAddress`, `ClassPhone`, `ClassText |
TextVariationUri`, `ClassNumber` and `ClassNumber | NumberFlagDecimal`. A field of any kind but `Text`
SHALL be edited without autocorrection or suggestions.

#### Scenario: From a name to a phone number

- **WHEN** the caret moves from a `Text` field to a `Phone` field on the next frame
- **THEN** the session reports `Retargeted`, and its kind is `Phone`

#### Scenario: The same kind

- **WHEN** a frame passes with the caret still in the `Decimal` field it was in
- **THEN** the session reports no change, and its kind is `Decimal`
