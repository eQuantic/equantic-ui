/**
 * The space a captured gesture is read in on the web: the one its surface's offset moves it in,
 * which is its PARENT's. What turns or scales the page around the surface is taken out of the
 * pointer, and what the surface's own `transform` draws stays with the surface.
 *
 * Photon reads a captured gesture through the inverse of the transform the press found, the one of
 * the surface's ancestors, and paints the offset as a translate outside the surface's own transform
 * (#734). The web has the same two parts: a drag's offset rides the individual `translate` property
 * and a dismiss's a `translateY`, both applied outside the box's own transform, so both resolve in the
 * parent's space, and that space reaches the screen through the transforms of every ancestor. A
 * finger's travel taken through the inverse of their composition is in it, which is what the offset
 * is written in and what the caller hears (#1195, #1204, #1492). Read on the screen, a horizontal drag under
 * a box turned a quarter heard nothing along its track, and under an ancestor at `scale(2)` a row
 * moved 80px for a 40px move of the finger.
 *
 * Only the LINEAR part matters: a gesture measures a travel, the difference of two points, and every
 * offset, scroll position and transform origin cancels out of one. The vocabulary writes a box's
 * transform through `transform` alone, so that is the property read, and a perspective is read as
 * the plane it starts from.
 */

/** A 2D linear map as CSS lists it in `matrix(a, b, c, d, …)`: x' = a·x + c·y, y' = b·x + d·y. */
type Linear = readonly [number, number, number, number];

const IDENTITY: Linear = [1, 0, 0, 1];

export interface GestureSpace {
  /** The travel of a move of `dx`, `dy` on the screen along the gesture's axis, in the surface's space. */
  along(horizontal: boolean, dx: number, dy: number): number;
  /**
   * How far along the axis, in the surface's space, a move of `distance` on the screen reaches when
   * it runs where the axis climbs fastest. A slop is the finger's, the same distance on the screen
   * whatever the surface is drawn at, so this is what a gesture compares its travel against: in the
   * surface's own space 16px on the screen were only 8 of a surface drawn at twice the size, and a
   * surface drawn at a quarter armed on a tremor of 4, as Photon measured it (#734).
   */
  onScreen(horizontal: boolean, distance: number): number;
}

/**
 * The space of `surface`'s offset as the press finds it. It is read once, when the gesture starts, and
 * held until the release: a surface that follows the finger moves under the pointer, and the gesture
 * measures against where it started.
 */
export function gestureSpace(surface: Element): GestureSpace {
  let toScreen = IDENTITY;
  for (let at = surface.parentElement; at; at = at.parentElement) toScreen = compose(linearOf(at), toScreen);

  const [a, b, c, d] = toScreen;
  const determinant = a * d - b * c;
  // An ancestor drawn at no size draws nothing that can be pressed; if one ever is, the screen is
  // the only space left to read it in.
  const [ia, ib, ic, id] =
    Math.abs(determinant) > 1e-9
      ? [d / determinant, -b / determinant, -c / determinant, a / determinant]
      : IDENTITY;

  return {
    along: (horizontal, dx, dy) => (horizontal ? ia * dx + ic * dy : ib * dx + id * dy),
    onScreen: (horizontal, distance) => distance * (horizontal ? Math.hypot(ia, ic) : Math.hypot(ib, id)),
  };
}

/**
 * The linear part of an element's own `transform`, the identity when it has none. A browser reports a
 * transform with any 3D part (a `translateZ` layer hint) as `matrix3d`, whose first two columns hold
 * the same four numbers a `matrix` does.
 */
function linearOf(element: Element): Linear {
  const matrix = /^matrix(3d)?\(([^)]+)\)$/.exec(getComputedStyle(element).transform);
  if (!matrix) return IDENTITY;
  const values = matrix[2].split(',').map(Number);
  return matrix[1] ? [values[0], values[1], values[4], values[5]] : [values[0], values[1], values[2], values[3]];
}

/** `outer` applied after `inner`: what an ancestor's transform makes of what its descendant drew. */
function compose(outer: Linear, inner: Linear): Linear {
  const [a, b, c, d] = outer;
  const [e, f, g, h] = inner;
  return [a * e + c * f, b * e + d * f, a * g + c * h, b * g + d * h];
}
