# Spec Delta

## ADDED Requirements

### Requirement: The lift reaches through wrappers that draw no box

When a control's child draws no box of its own (`display: contents`: an InView, an Adaptive's arms, a
light and dark Image), the first descendants that draw one SHALL be lifted above the slop, through any
chain of such wrappers, so their hits and their hover stay theirs.

#### Scenario: A card behind an InView

- **WHEN** the mouse rests on the centre of the card in `Pressable(InView(card))` under a fine pointer
- **THEN** the card is the element under the pointer and matches `:hover`, where the pressable took
  the hit and the card's hover never showed
