import { dec } from './utils/decimal';
import { long } from './utils/long';
import { round } from './utils/dotnet-math';
import { format, parseEnum } from './utils/format';
import { dateTime, timeSpan, dateOnly, timeOnly } from './utils/datetime';
import { stringBuilder } from './utils/string-builder';
import { queue, stack } from './utils/collections';
import { StyleBuilder } from './utils/style-builder';
import { ClassBuilder, joinClasses, whenClass } from './utils/class-builder';

/**
 * `$eq` — the single runtime namespace the transpiler emits for .NET-compat helpers, organised by
 * domain. Instead of scattering loose imports (`dec`, `long`, `dateTime`, …) into every generated
 * module — short, collision-prone names — the compiler emits `$eq.num.dec(...)`, `$eq.time.dateTime(...)`,
 * etc., and the runtime exposes `$eq` once (globally on `window`, like `StyleBuilder`). No per-module
 * imports, no risk of shadowing a user identifier.
 *
 * Members are the same function references as the individual exports, so the factories keep their
 * attached statics (`$eq.time.dateTime.now()`, `$eq.time.timeSpan.fromSeconds(1)`).
 */
export const $eq = {
  /** Numeric compat: exact decimal and 64-bit integer. */
  num: { dec, long },
  /** Math with .NET semantics (banker's rounding). */
  math: { round },
  /** Text: number/string formatting and StringBuilder. */
  text: { format, stringBuilder },
  /** Date and time, tick-precise. */
  time: { dateTime, timeSpan, dateOnly, timeOnly },
  /** Enum parsing (member-name string). */
  enums: { parse: parseEnum },
  /** Collections — Queue (FIFO), Stack (LIFO). */
  collections: { queue, stack },
  /** CSS class composition (the styling subsystem). */
  css: { styleBuilder: StyleBuilder, classBuilder: ClassBuilder, joinClasses, whenClass },
} as const;

export type EqNamespace = typeof $eq;
