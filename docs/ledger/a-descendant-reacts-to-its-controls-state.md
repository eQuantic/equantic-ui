- **2026-11-10 · A descendant reacts to its control's state**: a hover inside an enabled `Pressable`
  is now the control's, as its focus and its press already were, up to a control nested in it, so
  the arrow of a button nudges with the pointer on its label; a box outside every control, or
  inside a disabled one, still hovers itself. Measured first: every hover a component here states
  inside a control sits on the control's own surface, so none changed where it applies, and the
  parity fixture swapped one class per such box. `BoxStyle.Foreground` and `StyleDiff.Foreground`,
  Flutter's `DefaultTextStyle` and `IconTheme`, hand a colour down to the text, icons, vectors and
  spinners that name none, resolved for the box's state; a text with no colour writes none on the web and
  inherits it, and the control's button inherits too. `Transform2D.FractionalTranslate`, Flutter's
  `FractionalTranslation`, moves a box by fractions of its own size. In Chromium the equantic.tech
  primary button built from the three draws its label in the surface's foreground in both themes
  (contrast 7.91 and 7.55 at rest), its arrow 2px over and its band from -130% to its far side on
  hover. A behaviour change: a `Hover` on a box inside a `Pressable` applies while the control is
  hovered; a box that should react to its own pointer there is its own control
  ([#614](https://github.com/eQuantic/equantic-ui/issues/614)). Proposed and archived through
  OpenSpec, in `interaction-states`.
