# Spec Delta

## ADDED Requirements

### Requirement: A TypeStyle scales as .NET scales it

`TypeStyle.ScaledSize` and `TypeStyle.ScaledLineHeight` SHALL answer in the browser what they answer in
.NET: the factor clamped from 0.5 to the style's `MaxScale`, and the result snapped to the 0.5dp step,
half to even, in single precision.

#### Scenario: A style of 13 on 16.5, scaled

- **WHEN** a `TypeStyle` of size 13, line height 16.5 and `MaxScale` 1.3 is scaled by 0.2, 1.15,
  1.237 and 3
- **THEN** its size is 6.5, 15, 16 and 17, and its line height 8, 19, 20.5 and 21.5, as in .NET
