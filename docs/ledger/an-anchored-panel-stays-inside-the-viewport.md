- **2026-11-27 · An anchored panel stays inside the viewport, and a date picker's calendar hangs under its field's
  end**: a date picker hung its calendar from the opener's start, so on a field that spans its column the calendar
  left the viewport at every width, and the page scrolled sideways on a phone. It now hangs `BottomEnd`, and every
  anchored panel its placement would put past the viewport's start or end edge comes back inside, 8dp from it: on
  Photon once its layer is laid out, on the web by the runtime, which measures the panel CSS placed
  ([#1933](https://github.com/eQuantic/equantic-ui/issues/1933)). Proposed and archived through OpenSpec
  (`openspec/specs/anchored-panels`).
