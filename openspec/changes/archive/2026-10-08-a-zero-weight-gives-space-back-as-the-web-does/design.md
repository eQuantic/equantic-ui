# Design

## How Flutter solves it

It does not shrink an inflexible child at all: a `Flex` lays it out unbounded on the main axis and
reports the overflow, so there is no rule there to borrow. The twin Photon is measured against here
is the web, as it was for #680: the web realizer writes a zero weight
`flex: 0 <shrink> <basis>; min-width: 0`, and a browser resolves it by CSS Flexbox §9.7. No row of
`docs/FLUTTER-PARITY.md` applies.

## Decisions

**A zero weight follows CSS's shrink rule, and nothing else is moved.** Its floor is zero, its weight
is its shrink times its size, and it is settled by CSS's loop: what is taken is shared by weight, an
item whose share would carry it past its floor stops at the floor, and what it could not give is
shared again among the rest. The other items keep their floor and their weight, the room above the
floor; a share weighted by room never passes a floor, so a line without a zero weight settles in the
first round, on the numbers and the float operations it always had.

**A zero weight shrinks with the others, not first.** The text cut runs before the flex-shrink pass
and cuts text to an ellipsis, which is Photon's own truncation contract for text. A zero weight is an
item with a declaration of its own, so it leaves the cut whatever it holds and gives space back in
the flex-shrink pass, where its share is weighed against everything else that shrinks.

**A shrinking zero weight goes back into a slot, with a basis or without one.** Re-measured through
its wrapper, it hugged its child, so a child wider than the extent held the item at the child's width.
In a slot it takes the extent, and the child overflows it, as the web lets it.

## Risks

- A zero weight beside an item that is not one resolves to CSS's numbers only when that item's floor
  is zero or its whole size, which covers every item that holds no text. A box of words floors at its
  longest word and is still asked by its room, where CSS asks it by its size, so such a line differs
  by its room's difference: measured, a box of three words beside a zero weight at 300, in 400, is
  109.39 and 290.61 in Chrome and 75.9 and 287.3 on Photon, which also re-measures the box to its
  re-wrapped text. That is the rule every item but a zero weight has, not a zero weight's, and it
  stays as it is here.
- Text inside a shrunk zero weight is no longer ellipsized by the text cut: it is laid out in its slot
  like any other child and wraps, as a browser wraps it. A Text with `MaxLines = 1` still ellipsizes
  by its own rule, on both targets.
