# Tasks

## 1. The bridge

- [x] 1.1 Keep one element per node id across reads, told what the node says now, released when the node leaves or its position holds another kind of node
- [x] 1.2 Take away a value, a pick and an open list a node no longer has
- [x] 1.3 Make the content view an `AXGroup` element, its elements' parent
- [x] 1.4 Answer the view's focused element from the host's `FocusedId`, and post the focused-element notification when the keyboard moves, once the tree was read
- [x] 1.5 Post AppKit's notifications through one class, the announcements included

## 2. Proof

- [x] 2.1 In process, through AppKit: the same element across reads, its value updated, a removed node's element gone, a new kind of node a new element; what a node stops saying taken away; the view an `AXGroup` element whose focused element holds the keyboard. Each fails without the change, and the reuse without the take-away fails the second
- [x] 2.2 From outside, with the AX API on the PhotonDesktop sample, before and after: the window's children, an element held across a second read, the parent chain, the application's focused element after a press moves the keyboard, and the focused-element notification an observer hears

## 3. Documentation

- [x] 3.1 The Photon page's macOS bridge item, in all five languages
- [x] 3.2 One entry under `docs/ledger/`
