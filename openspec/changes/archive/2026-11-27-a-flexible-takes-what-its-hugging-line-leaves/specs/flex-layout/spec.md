## ADDED Requirements

### Requirement: A Flexible on a line its content sizes takes what the line has left

On a line with no leftover to share, whose main axis its content sizes and whose room is finite, a `Flexible` with
content SHALL contribute its content's size measured in the room the line has left once its rigid items and gaps are
laid, never the line's whole room. Flexibles that together overrun that room SHALL each give back by its shrink weight
times its size, as a browser shrinks flex items from their content size.

#### Scenario: An entry between an icon and a button

- **WHEN** a row of a 20dp icon, a flexible text entry and a 48dp button, with 10dp gaps, lies in a field that fills a
  hugging column of a 292dp grid cell, padded 14dp a side
- **THEN** the entry is 264 − 20 − 48 − 20 = 176dp wide, and the button ends inside the field, inside the cell

#### Scenario: Two entries side by side

- **WHEN** two flexible entries of one weight and a 48dp button lie in the same row
- **THEN** the two entries share the 196dp the button and the gaps leave, alike

#### Scenario: A short label

- **WHEN** a flexible holds a two-letter label in the same row
- **THEN** it keeps its own width
