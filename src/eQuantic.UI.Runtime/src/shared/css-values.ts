/**
 * The smallest CSS spellings, in a LEAF module.
 *
 * They live here rather than in `lowering` because more than one module needs them and `lowering`
 * is not a leaf: the canvas painter imports them, `lowering` imports the painter, and a cycle
 * through `lowering` is how this runtime has broken three times — "Class extends value undefined"
 * at load, with every spec in the suite failing at once and nothing pointing at the cause.
 */

import type { ColorTokenValue, ColorValue } from './nodes';

/** Bare invariant number — mirrors C# `TokenCss.Number` ("0.####"). */
export function num(value: number): string {
  return `${parseFloat(value.toFixed(4))}`;
}

/** `#rrggbb`, or `#rrggbbaa` when the colour is translucent — the C# `TokenCss.Hex` twin. */
export function hex(color: ColorValue): string {
  const channel = (v: number): string => v.toString(16).padStart(2, '0');
  const base = `#${channel(color.r)}${channel(color.g)}${channel(color.b)}`;
  return color.a === 255 ? base : base + channel(color.a);
}

/**
 * A token as CSS: one hex when both modes agree, else `light-dark(…)` so the BROWSER answers.
 * Nothing here resolves a mode — that is the cascade's job on this target, and the reason a colour
 * authored once follows the theme without the realizer being told which one is on.
 */
export function tokenValue(token: ColorTokenValue): string {
  const light = hex(token.light);
  const dark = hex(token.dark);
  return light === dark ? light : `light-dark(${light}, ${dark})`;
}
