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

const VARIANT_ORDER = [
  'primary',
  'secondary',
  'destructive',
  'success',
  'warning',
  'info',
  'tertiary',
];

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

/**
 * Substring replacement is WRONG for hex colors: they are variable-length, so an opaque token
 * (`#ffffff`) is a PREFIX of its own translucent form (`#ffffff0a`). Replacing the prefix strands
 * the alpha OUTSIDE the var — `var(--x, #ffffff)0a` — which is invalid CSS, so the browser drops
 * the declaration and the style silently vanishes. A match only counts when the next character
 * cannot extend the color literal. Mirrors C# ThemeVarMap.ReplaceAtTokenBoundary.
 */
function replaceAtTokenBoundary(value: string, token: string, replacement: string): string {
  let out = '';
  let index = 0;
  for (;;) {
    const found = value.indexOf(token, index);
    if (found < 0) return out + value.slice(index);
    const after = found + token.length;
    const extendsLiteral = after < value.length && /[0-9a-fA-F]/.test(value[after]);
    out += value.slice(index, found) + (extendsLiteral ? token : replacement);
    index = after;
  }
}

function rewrite(value: string, entries: VarEntry[]): string {
  if (!value.includes('light-dark(') && !value.startsWith('#')) return value;
  for (const [token, varName] of entries) {
    if (value === token) return `var(${varName}, ${token})`;
    if (value.includes(token))
      value = replaceAtTokenBoundary(value, token, `var(${varName}, ${token})`);
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
    const index = target.insertRule(`.${className}{${declaration}}`, target.cssRules.length);
    // A declaration the parser DROPS leaves an empty rule behind — the style silently vanishes.
    // Say so: this is the only place that knows the raw declaration.
    const inserted = target.cssRules[index] as CSSStyleRule | undefined;
    // jsdom's CSS parser predates light-dark()/color-mix and drops them wholesale — warning
    // there would drown every spec run in noise the browser never produces.
    const isJsdom = typeof navigator !== 'undefined' && navigator.userAgent.includes('jsdom');
    if (!isJsdom && inserted && inserted.style.length === 0 && declaration.length > 0) {
      console.warn(
        `[eQuantic.UI] atomic rule dropped by the CSS parser: .${className}{${declaration}}`,
      );
    }
  } catch (error) {
    // An unparsable RULE must never take the app down; the element just misses that rule.
    console.warn(`[eQuantic.UI] atomic rule rejected: .${className}{${declaration}}`, error);
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
    // Pseudo-variant declarations (\u0001-tagged) only apply under their pseudo-class — the
    // resting style this helper describes does not include them.
    if (rule.includes('\u0001')) continue;
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
  // Already there is already right. A node can be handed the same declaration twice — a painted
  // box that is also a drag surface gets `pointer-events: auto` from both — and appending it
  // again produced a duplicate class the C# side never writes, so the two trees stopped matching
  // over a declaration they agreed on.
  if (!atomic.includes(added)) atomic.push(added);
  atomic.sort();
  node.attributes['class'] = [...semantic, ...atomic].join(' ');
}

/**
 * Spec S6 gate names — the range is IN the name (`eq-vc600` = compact until 600, `eq-vx1024` =
 * expanded from 1024), which is what lets a design bring its own breakpoints with no shared
 * registry: both twins derive the identical name and CSS from the same thresholds.
 * Byte-identical to the C# `AdaptiveGates`.
 */
export function gateCompactUntil(until: number): string {
  return `eq-vc${gateNum(until)}`;
}

export function gateMediumFrom(from: number, until: number): string {
  return until > 0 ? `eq-vm${gateNum(from)}-${gateNum(until)}` : `eq-vm${gateNum(from)}`;
}

export function gateExpandedFrom(from: number): string {
  return `eq-vx${gateNum(from)}`;
}

/** Invariant number formatting — the C# TokenCss.Number twin ("0.####"). */
function gateNum(value: number): string {
  return `${parseFloat(value.toFixed(4))}`;
}

/** The gate's rules, derived from its NAME (the C# AdaptiveGates.Css twin). */
function adaptiveGateRules(gate: string): string[] {
  if (!gate.startsWith('eq-v') || gate.length < 6) return [];
  const kind = gate[4];
  const range = gate.slice(5).split('-');
  const first = parseFloat(range[0]);
  const second = range.length > 1 ? parseFloat(range[1]) : 0;
  const below = (dp: number) => `${parseFloat((dp - 0.02).toFixed(4))}px`;
  if (kind === 'c')
    return [
      `.${gate}{display:contents}`,
      `@media (min-width: ${gateNum(first)}px){.${gate}{display:none}}`,
    ];
  if (kind === 'm' && second > 0)
    return [
      `.${gate}{display:none}`,
      `@media (min-width: ${gateNum(first)}px) and (max-width: ${below(second)}){.${gate}{display:contents}}`,
    ];
  if (kind === 'm' || kind === 'x')
    return [
      `.${gate}{display:none}`,
      `@media (min-width: ${gateNum(first)}px){.${gate}{display:contents}}`,
    ];
  return [];
}

/** Ensure a size-class gate's rules exist in the registry (idempotent; adopted from SSR). */
export function ensureAdaptiveGate(gate: string): void {
  if (known.has(gate)) return;
  known.add(gate);
  const target = registry();
  if (!target) return;
  for (const rule of adaptiveGateRules(gate)) {
    try {
      target.insertRule(rule, target.cssRules.length);
    } catch {
      /* gates must never take the app down */
    }
  }
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
/** Spec S5: pseudo-variant rules of the same atomic family — `.eq-x:hover{decl}`; the pseudo is
 * part of the hash so hover/base variants of one declaration are distinct classes. */
export function atomizePseudo(pseudo: string, entries: Record<string, string | undefined>): string {
  const vars = varMapFor(getPhotonTheme());
  const classes: string[] = [];
  for (const name of Object.keys(entries)) {
    const value = entries[name];
    if (value === undefined) continue;
    const rewritten = rewrite(value, vars);
    const declaration = declarationFor(name, rewritten);
    const className = `eq-${hashDeclaration(`${pseudo}|${declaration}`)}`;
    // Stored with the pseudo tag (the C# sink's own \u0001 convention): the class must stay in
    // the map so mergeAtomicDeclaration classifies it as atomic, but effectiveStyle describes
    // the RESTING style and skips the tagged family.
    if (!known.has(className)) {
      known.add(className);
      ruleTexts.set(className, `${pseudo}\u0001${declaration}`);
      const target = registry();
      try {
        // C# twin (StyleAtomizer.Css): §10 — hover never fires on touch, and a touch browser's
        // tap leaves a STICKY emulated :hover behind, so the family only exists for devices that
        // can hover. The gate wraps the rule, never the hash: the class string stays identical.
        const rule =
          pseudo === ':hover'
            ? `@media (hover: hover){.${className}${pseudo}{${declaration}}}`
            : `.${className}${pseudo}{${declaration}}`;
        target?.insertRule(rule, target.cssRules.length);
      } catch {
        /* unparsable pseudo rules must never take the app down */
      }
    } else {
      ruleTexts.set(className, `${pseudo}\u0001${declaration}`);
    }
    classes.push(className);
  }
  classes.sort();
  return classes.join(' ');
}

/**
 * SCROLL-LINKED variant (Sticky.ScrolledStyle) — the C# ClassForScrolled twin: `scrolled|` in the
 * hash, rules gated by the root's `eq-scrolled` class (the runtime scroll listener toggles it).
 */
export function atomizeScrolled(entries: Record<string, string | undefined>): string {
  const vars = varMapFor(getPhotonTheme());
  const classes: string[] = [];
  for (const name of Object.keys(entries)) {
    const value = entries[name];
    if (value === undefined) continue;
    const rewritten = rewrite(value, vars);
    const declaration = declarationFor(name, rewritten);
    const className = `eq-${hashDeclaration(`scrolled|${declaration}`)}`;
    // TAGGED, for the reason the pseudo family is: a scroll-linked declaration is not the resting
    // style, so the class must stay in the map (mergeAtomicDeclaration classifies it as atomic)
    // while effectiveStyle, which describes what the element looks like AT REST, skips it. Stored
    // untagged, a scrolled veil read as part of the base style; the other branch reached for a
    // `pseudo` this function does not have, which is how the typecheck found both.
    const tagged = `scrolled\u0001${declaration}`;
    if (!known.has(className)) {
      known.add(className);
      ruleTexts.set(className, tagged);
      const target = registry();
      try {
        target?.insertRule(
          `html.eq-scrolled .${className}{${declaration}}`,
          target.cssRules.length,
        );
      } catch {
        /* unparsable rules must never take the app down */
      }
    } else {
      ruleTexts.set(className, tagged);
    }
    classes.push(className);
  }
  classes.sort();
  return classes.join(' ');
}

/**
 * A vendor prefix is not a declaration of its own — it is the SAME declaration written for another
 * engine, and the two belong in one rule.
 *
 * Alone in an atomic class, `-webkit-backdrop-filter` is dropped whole by engines that only take the
 * standard name (measured: `insertRule` leaves an empty rule in Chromium), so the class existed, sat
 * on the element and did nothing. Written as a pair, whichever name an engine knows survives and the
 * rule is never empty. Both spellings produce the same text, so the pair costs ONE class.
 *
 * The C# `StyleSink.Declaration` twin pairs the identical text — the classes are cross-pinned by hash.
 */
const VENDOR_PAIRS: Record<string, string> = {
  'backdrop-filter': '-webkit-backdrop-filter',
  '-webkit-backdrop-filter': 'backdrop-filter',
};

export function declarationFor(name: string, value: string): string {
  const twin = VENDOR_PAIRS[name];
  if (!twin) return `${name}:${value}`;
  // Prefixed FIRST, standard last: an engine that understands both must land on the standard.
  const prefixed = name.startsWith('-') ? name : twin;
  const standard = name.startsWith('-') ? twin : name;
  return `${prefixed}:${value};${standard}:${value}`;
}

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
    const declaration = declarationFor(name, rewritten);
    const className = `eq-${hashDeclaration(declaration)}`;
    ensureRule(className, declaration);
    classes.push(className);
  }

  classes.sort();
  return { class: classes.join(' '), style: custom.length > 0 ? custom.join('; ') : undefined };
}
