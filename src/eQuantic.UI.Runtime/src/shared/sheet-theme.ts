/**
 * Mirror of the C# `SheetTheme`: the spreadsheet's slot of the theme, every member optional. A member
 * left null is the spreadsheet's default, derived from the theme's foundation colours. Built from a
 * config object, as eqc writes a record whose members are all `init` (`new SheetTheme({ gridline })`),
 * and from the bridge's JSON (`theme-bridge.ts`).
 */

import type { ColorToken } from './value-types';
import { hashesByValue } from '../utils/hash';

export class SheetTheme {
  gridline: ColorToken | null = null;
  headerBackground: ColorToken | null = null;
  headerText: ColorToken | null = null;
  headerSelectedBackground: ColorToken | null = null;
  headerSelectedText: ColorToken | null = null;
  cellBackground: ColorToken | null = null;
  cellText: ColorToken | null = null;
  rangeFill: ColorToken | null = null;
  activeOutline: ColorToken | null = null;
  fillPreview: ColorToken | null = null;
  fillHandle: ColorToken | null = null;
  entrySelection: ColorToken | null = null;

  constructor(config?: Partial<SheetTheme>) {
    if (config) Object.assign(this, config);
  }
}

/** A record: it hashes by its members, as `equals` compares it (`ValueTwinHashTests` holds this to the C#). */
hashesByValue(SheetTheme);
