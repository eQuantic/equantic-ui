## ADDED Requirements

### Requirement: A hand-written twin's config takes every member its C# class lets an initializer set

An object initializer on a vocabulary type whose twin the runtime writes by hand SHALL pass the runtime's strict
`tsc` for every member the C# class lets it set, and the twin SHALL hold each value it sets. The config each such
twin's constructor takes, a named interface or a literal written in place, SHALL hold the C# class's public
settable members, less those its public constructors take as parameters, and nothing C# does not have, but for
the members `vocabulary-config.baseline.txt` names with a reason, a list that may only shrink. A config typed as a
record of anything (`EqConfig`) takes every member.

#### Scenario: A box style's foreground

- **WHEN** a module eqc writes builds `new BoxStyle { Foreground = new ColorToken(Color.Black) }`
- **THEN** the runtime's `tsc` takes it, and the twin's `foreground` holds that token

#### Scenario: A node's config written in place

- **WHEN** a module eqc writes builds `new Group(child) { Label = "Totals", Bookmark = "totals", AlignSelf = CrossAlign.Center, GridSpan = 2 }`
- **THEN** the runtime's `tsc` takes it, and the twin holds `bookmark` `totals`, `alignSelf` `center` and
  `gridSpan` `2`

#### Scenario: A spacer's fixed length

- **WHEN** a module eqc writes builds `new Spacer { FixedLength = 8 }`
- **THEN** the runtime's `tsc` takes it, and the twin's `fixedLength` is `8`

#### Scenario: Every config of the mirror

- **WHEN** the suite reads every config a class of the hand-written mirror takes, and the C# type of that class
- **THEN** no member differs either way but those the baseline names, and an entry of the baseline that no longer
  differs fails the suite
