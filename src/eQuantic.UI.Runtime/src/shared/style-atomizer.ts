/**
 * The client twin of the C# StyleAtomizer (docs/STYLE-SEMANTICS-PLAN.md §2): every regular style
 * declaration becomes one deduplicated ATOMIC RULE; markup carries sorted class names. The hash,
 * the theme-variable rewrite and the class format are byte-identical to the C# side, so SSR markup
 * and client lowering agree by CLASS IDENTITY and hydration never repaints. Rules inserted at
 * runtime (dynamic values) are memoized forever — a repeated value is a Set hit, never a rewrite.
 * Custom properties (`--*`) are tier 3: per-element inputs that stay inline by design.
 */

import type { AppTheme, ColorToken } from './value-types';
import { getPhotonTheme } from './photon-context';

/** FNV-1a 32-bit over UTF-16 code units, base36 — the exact C# `StyleAtomizer.Hash`. */
export function hashDeclaration(text: string): string {
  let hash = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    hash ^= text.charCodeAt(i);
    // 32-bit FNV prime multiply without BigInt: split to keep precision.
    hash = (hash + ((hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24))) >>> 0;
  }
  return hash.toString(36);
}

// ---- theme variable map (mirror of C# ThemeVarMap — same names, same registration order) ---------

const SURFACE_VARS: ReadonlyArray<readonly [keyof AppTheme & string, string]> = [
  ['background', 'background'],
  ['surface', 'surface'],
  ['surfaceSubtle', 'surface-subtle'],
  ['surfaceHighlight', 'surface-highlight'],
  ['border', 'border'],
  ['borderStrong', 'border-strong'],
  ['textPrimary', 'text-primary'],
  ['textSecondary', 'text-secondary'],
  ['textMuted', 'text-muted'],
  ['textInverse', 'text-inverse'],
  ['focusRing', 'focus'],
  ['linkColor', 'link'],
  ['scrim', 'scrim'],
];

const VARIANT_ORDER = ['primary', 'secondary', 'destructive', 'success', 'warning', 'info', 'tertiary'];

function hex(color: { r: number; g: number; b: number; a: number }): string {
  const channel = (v: number) => v.toString(16).padStart(2, '0');
  const base = `#${channel(color.r)}${channel(color.g)}${channel(color.b)}`;
  return color.a === 255 ? base : base + channel(color.a);
}

function tokenCss(token: ColorToken): string {
  const light = hex(token.light);
  const dark = hex(token.dark);
  return light === dark ? light : `light-dark(${light}, ${dark})`;
}

type VarEntry = readonly [tokenValue: string, varName: string];

const varMaps = new WeakMap<AppTheme, VarEntry[]>();

function varMapFor(theme: AppTheme): VarEntry[] {
  let entries = varMaps.get(theme);
  if (entries) return entries;

  entries = [];
  const seen = new Set<string>();
  const register = (token: ColorToken, name: string) => {
    const value = tokenCss(token);
    if (!seen.has(value)) {
      seen.add(value);
      entries!.push([value, `--eq-color-${name}`]);
    }
  };

  for (const [prop, name] of SURFACE_VARS) {
    register(theme[prop] as unknown as ColorToken, name);
  }
  for (const variant of VARIANT_ORDER) {
    const colors = theme.colors(variant);
    register(colors.base, `${variant}-base`);
    register(colors.onBase, `${variant}-on`);
    register(colors.pressed, `${variant}-pressed`);
    register(colors.subtle, `${variant}-subtle`);
    register(colors.onSubtle, `${variant}-on-subtle`);
  }

  varMaps.set(theme, entries);
  return entries;
}

function rewrite(value: string, entries: VarEntry[]): string {
  if (!value.includes('light-dark(') && !value.startsWith('#')) return value;
  for (const [token, varName] of entries) {
    if (value === token) return `var(${varName}, ${token})`;
    if (value.includes(token)) value = value.split(token).join(`var(${varName}, ${token})`);
  }
  return value;
}

// ---- the rule registry (insert-once CSSOM) --------------------------------------------------------

const known = new Set<string>();
/** class → raw declaration (`prop:value`) — the lookup behind <see cref="effectiveStyle"/> and the
 * registry's own dedupe; survives document swaps (jsdom test remounts). */
const ruleTexts = new Map<string, string>();
let sheet: CSSStyleSheet | null = null;
let adopted = false;

function registry(): CSSStyleSheet | null {
  if (typeof document === 'undefined') return null;
  if (sheet) return sheet;

  // Adopt the SSR-emitted rules once: the classes the server markup references are already styled;
  // seed the known-set so we never re-insert them.
  let el = document.getElementById('eq-atomic') as HTMLStyleElement | null;
  if (el && !adopted) {
    adopted = true;
    for (const match of (el.textContent ?? '').matchAll(/\.(eq-[0-9a-z]+)\{/g)) {
      known.add(match[1]);
    }
  }
  if (!el) {
    el = document.createElement('style');
    el.id = 'eq-atomic';
    document.head.appendChild(el);
  }
  sheet = el.sheet;
  return sheet;
}

function ensureRule(className: string, declaration: string): void {
  ruleTexts.set(className, declaration);
  if (known.has(className)) return;
  known.add(className);
  const target = registry();
  if (!target) return;
  try {
    target.insertRule(`.${className}{${declaration}}`, target.cssRules.length);
  } catch {
    // An unparsable declaration must never take the app down; the element just misses that rule.
  }
}

/** Test hook: forget everything (jsdom re-mounts between specs). */
export function resetAtomizerForTests(): void {
  known.clear();
  ruleTexts.clear();
  sheet = null;
  adopted = false;
}

/**
 * TEST SEAM: reconstitute an element's effective declarations from its atomic classes plus the
 * inline tail — `"prop: value; prop: value"`, the shape the pre-atomic specs asserted against.
 * Production code never calls this; specs assert INTENT (what the element resolves to) instead of
 * pinning class hashes.
 */
export function effectiveStyle(node: { attributes: Record<string, string | undefined> }): string {
  const parts: string[] = [];
  for (const cls of (node.attributes['class'] ?? '').split(' ')) {
    const rule = ruleTexts.get(cls);
    if (!rule) continue;
    const colon = rule.indexOf(':');
    parts.push(`${rule.slice(0, colon)}: ${resolveVars(rule.slice(colon + 1))}`);
  }
  parts.sort();
  if (node.attributes['style']) parts.push(node.attributes['style']);
  return parts.join('; ');
}

/** Collapse `var(--x, fallback)` to the fallback — the value the browser computes when the token
 * stylesheet defines --x to the same color (specs assert computed intent, not plumbing). */
function resolveVars(value: string): string {
  let out = value;
  for (;;) {
    const start = out.indexOf('var(');
    if (start < 0) return out;
    let depth = 0;
    let comma = -1;
    let end = -1;
    for (let i = start + 4; i < out.length; i++) {
      const ch = out[i];
      if (ch === '(') depth++;
      else if (ch === ')') {
        if (depth === 0) {
          end = i;
          break;
        }
        depth--;
      } else if (ch === ',' && depth === 0 && comma < 0) comma = i;
    }
    if (end < 0 || comma < 0) return out;
    out = out.slice(0, start) + out.slice(comma + 2, end) + out.slice(end + 1);
  }
}

/**
 * Merge ONE extra declaration into an already-atomized element (the align-self path: the parent
 * decides a child declaration after the child lowered). The atomic segment of the class attribute is
 * re-sorted with the new class so it stays byte-identical to the C# post-pass, which atomizes
 * everything in a single sorted batch. Semantic (non-registry) classes keep their leading position.
 */
export function mergeAtomicDeclaration(
  node: { attributes: Record<string, string | undefined> },
  prop: string,
  value: string,
): void {
  const added = atomizeEntries({ [prop]: value }).class;
  if (!added) return;
  const existing = (node.attributes['class'] ?? '').split(' ').filter(Boolean);
  const semantic = existing.filter((c) => !ruleTexts.has(c));
  const atomic = existing.filter((c) => ruleTexts.has(c));
  atomic.push(added);
  atomic.sort();
  node.attributes['class'] = [...semantic, ...atomic].join(' ');
}

// ---- the public seam ------------------------------------------------------------------------------

export interface AtomizedStyle {
  /** Sorted atomic class names for the regular declarations ('' when none). */
  class: string;
  /** The inline residue — custom-property tail only (undefined when none). */
  style?: string;
}

/**
 * Convert a style-entries object into atomic classes (rules ensured in the registry) plus the
 * inline custom-property residue. Values are rewritten against the ACTIVE theme's variable map, so
 * the generated rules match the SSR ones for theme-sourced colors.
 */
export function atomizeEntries(entries: Record<string, string | undefined>): AtomizedStyle {
  const vars = varMapFor(getPhotonTheme());
  const classes: string[] = [];
  const custom: string[] = [];

  for (const name of Object.keys(entries)) {
    const value = entries[name];
    if (value === undefined) continue;
    if (name.startsWith('--')) {
      custom.push(`${name}: ${value}`);
      continue;
    }
    const rewritten = rewrite(value, vars);
    const className = `eq-${hashDeclaration(`${name}:${rewritten}`)}`;
    ensureRule(className, `${name}:${rewritten}`);
    classes.push(className);
  }

  classes.sort();
  return { class: classes.join(' '), style: custom.length > 0 ? custom.join('; ') : undefined };
}
