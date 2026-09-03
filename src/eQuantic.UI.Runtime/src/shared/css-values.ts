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

/**
 * The C# `[Flags] KeyModifiers` an event is carrying: shift 1, alt 2, command 4.
 *
 * `command` is the PLATFORM key — ⌘ on Apple, Ctrl elsewhere — so `ctrlKey` answers it, which is
 * the same resolution the shortcut controller makes. `Control` (8) is deliberately not reported:
 * the flag means "literally Control on every platform", and a browser cannot tell that apart from
 * the command key it just answered. One expression, every caller — the key path and the canvas
 * pointer disagreeing about ⌘ would be a difference nobody could explain.
 */
export function modifiersOf(event: {
  shiftKey?: boolean;
  altKey?: boolean;
  metaKey?: boolean;
  ctrlKey?: boolean;
}): number {
  return (
    (event.shiftKey ? 1 : 0) | (event.altKey ? 2 : 0) | (event.metaKey || event.ctrlKey ? 4 : 0)
  );
}
