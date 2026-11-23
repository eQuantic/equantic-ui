# Tasks

## 1. The proof

- [x] 1.1 A test of the gesture core makes the selection a press on text would, arms a gesture, and expects the selection cleared and none started until the gesture ends, then the page's `user-select` given back, including on a cancel. On the code before the cure the selection survives
- [x] 1.2 A sortable list's case drags an item made of text to a new place and expects no selection. On the code before the cure the item's text stays selected
- [x] 1.3 In a browser, on the dashboard sample, a drag of the `Method` heading past `Date`: before the cure `HOD` and `DATE` stay selected, and after it the column moves and nothing is selected, while a drag across ordinary text still selects it

## 2. The cure

- [x] 2.1 The gesture core clears the selection when a gesture arms, and keeps any from starting until it ends
- [x] 2.2 Two gestures armed at once hold the selection by count: the first takes the page's own user-select and the last gives it back. Before the count, the first to end gave it back under the second, and the last left the page at `none`
