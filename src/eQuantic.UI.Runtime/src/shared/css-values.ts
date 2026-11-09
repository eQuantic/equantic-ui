/**
 * The smallest CSS spellings, in a LEAF module.
 *
 * They live here rather than in `lowering` because more than one module needs them and `lowering`
 * is not a leaf: the canvas painter imports them, `lowering` imports the painter, and a cycle
 * through `lowering` is how this runtime has broken three times — "Class extends value undefined"
 * at load, with every spec in the suite failing at once and nothing pointing at the cause.
 */

import type { ColorTokenValue, ColorValue } from './nodes';

/**
 * A float as C#'s `ToString("0.##")`, with as many `#` as `places`, writes it, which the server's
 * `TokenCss` does and an atomic class is named from: its seven significant digits, a tie among them going
 * to the even digit, those rounded half away from zero to `places` decimals, no trailing zero, and the
 * sign kept on a zero. The double the float widens to rounds elsewhere: `toFixed` read 1.005f as
 * 1.00499999523… and wrote 1, where the server writes 1.01, so the two sides minted different classes
 * (#729). Past seven digits .NET's own rounding shows: 16777216f is 16777220.
 */
export function floatText(value: number, places: number): string {
  if (!Number.isFinite(value)) return String(value);
  const negative = value < 0 || Object.is(value, -0);
  if (Number.isInteger(value) && !Object.is(value, -0) && Math.abs(value) < 1e7) return String(value);
  // Thirty digits past the first say whether what follows the seventh is exactly a half.
  const [mantissa, exponent] = Math.abs(value).toExponential(30).split('e');
  const written = mantissa.replace('.', '');
  const rest = written.slice(7);
  let digits = BigInt(written.slice(0, 7));
  if (rest === '5'.padEnd(rest.length, '0') ? digits % 2n === 1n : rest > '5') digits += 1n;
  const shift = Number(exponent) - 6 + places;
  const scale = 10n ** BigInt(Math.abs(shift));
  const scaled = shift >= 0 ? digits * scale : (digits + scale / 2n) / scale;
  const text = scaled.toString().padStart(places + 1, '0');
  const fraction = text.slice(-places).replace(/0+$/, '');
  return `${negative ? '-' : ''}${text.slice(0, -places)}${fraction === '' ? '' : `.${fraction}`}`;
}

/** Bare invariant number — mirrors C# `TokenCss.Number` ("0.####"). */
export function num(value: number): string {
  return floatText(Math.fround(value), 4);
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
