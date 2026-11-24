- **2026-11-23 · VoiceOver walks into a Photon window on the Mac, and hears where the keyboard is**: VoiceOver
  stopped at the title bar, and the application's focused element was the window. Measured from outside with
  the AX API on the PhotonDesktop sample, every read of the tree built new elements and released the ones
  before (an element held across a second read answered `kAXErrorInvalidUIElement`), each element named a
  parent the window did not list, and nothing answered the focused element. The macOS bridge now keeps one
  element per node for as long as the node lives, told what the node says now; the content view is the
  `AXGroup` the window lists; and its focused element is the element of the node with the keyboard, with
  `NSAccessibilityFocusedUIElementChangedNotification` posted when the keyboard moves, which an observer
  hears ([#1111](https://github.com/eQuantic/equantic-ui/issues/1111)). Proposed and archived through
  OpenSpec (`openspec/specs/native-accessibility`).
