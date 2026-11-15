## Why

Closes #1537, a user story of #1534 (sortable lists and drag and drop across containers), under the epic
#1510. The web sorts lists by pointer (#1535) and by keyboard (#1536), and Photon drew the same lists without
taking a drag: a board written once moved its cards in a browser and not in the desktop or mobile shells.

## What Changes

- **One arithmetic for both targets.** `SortPlacement` (Primitives) holds where a carried item lands among the
  others, how far its copy leans, where a key moves it, how fast a scroller scrolls near its edge, and the
  figures both realizers draw with (the pick-up travel, the hold, the edge, the opacities, the copy's shadow).
  The browser's controller runs its transpiled twin and the Photon host runs it, so neither can place an item
  differently. The TypeScript's own constants are gone.
- **Photon, the pointer.** The paint walk registers each list and item where it was drawn, every item of a
  list counted, the ones scrolled out of sight too. The host picks an item up past the travel under a mouse or
  a pen and after the hold under a finger, dims it in place, draws a copy of it over the frame that follows the
  pointer and leans with its speed (upright when the person asked for less motion), computes the place, asks
  the guard once per place, reports the hover and the drop, scrolls the list's scroll view near its edge, and
  puts the item back on Escape or when the system takes the pointer.
- **Photon, the keyboard.** With the focus in an item, Space picks it up, the arrows move it inside its list
  and to the neighbouring list of its group, Space or Enter drops it, Escape, Tab or a press puts it back, and
  the focus follows the item to where the app builds it. The steps are not spoken yet: the SDK's words live in
  `eQuantic.UI.Components`, which the realizer does not reference.
- **Engine.** A `Picture` (Flutter's): recorded commands with the textures they draw, drawn again in any later
  frame. The copy is one, recorded the first frame the item is drawn lifted.
- **Sample.** The PhotonDesktop gallery gains a Board section, the web dashboard's board on Photon.

## Capabilities

### Modified Capabilities

- `sortable-lists`: an item is carried on Photon as on the web.

## Impact

- **Vocabulary**: `SortPlacement`, with its twin. `PublicAPI.Unshipped.txt` moves, additions only.
- **Engine**: `Picture`, `DisplayListBuilder.PictureFrom`, `DrawPicture`, `CurrentTransform`, under
  `[EQX0001]`.
- **Photon**: the paint walk's sortable visits, the frame's sort regions, the host's gesture.
- **Web**: the controller reads the shared figures; nothing a page sees changes.
- **eqc, SDKs, templates**: nothing.
- **Docs**: the wiki's Sortable section in every language, the parity row, a ledger entry.

No break. Rules of `docs/PRINCIPLES.md` followed: 4, 5, 14.
