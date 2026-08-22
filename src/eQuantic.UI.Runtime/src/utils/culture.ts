/**
 * Track L (docs/I18N-PLAN.md D13/W3): the client's culture state — the mirror of .NET's pair,
 * because .NET's IS a pair: `CurrentUICulture` picks RESOURCES, `CurrentCulture` picks FORMATS,
 * and collapsing them would quietly depart from the experience the track exists to reproduce.
 *
 * A LEAF module on purpose: `eq.ts` and `utils/format.ts` read it, and a store any deeper in the
 * graph would close an eval-time cycle — the module-cycle law this tree already enforces twice.
 * Its one import is GENERATED DATA (no imports of its own), so the leaf stays a leaf.
 */

import { sdkNeutralStrings } from '../shared/sdk-strings.generated';

/** The catalog id the SDK's own strings live under — the class name eqc rewrites against. */
const SDK_RESOURCE_ID = 'SdkResources';

/**
 * The reserved catalog key carrying the culture's ISO currency code (Track L D7).
 *
 * It rides INSIDE the catalog rather than in a file of its own, and that is the whole trick:
 * `{0:C}` needs a currency to format with, `Intl` demands a CODE ("BRL", never "R$"), and the
 * browser has no API that maps a locale to one. The build knows it (`RegionInfo`), the catalog is
 * already inlined by the shell and already fetched by `setCulture` — so the fact travels with the
 * culture it belongs to, at zero extra cost. A `$` prefix cannot collide with a resource id, which
 * is always a C# identifier followed by `/`.
 */
const CURRENCY_KEY = '$currency';

/**
 * The culture's own DATE/TIME patterns, carried the same way and for the same reason: .NET's
 * standard specifiers (`d`, `D`, `g`…) are shorthand for a per-culture PATTERN
 * (`M/d/yyyy` in en-US, `dd.MM.yyyy` in de-DE), and `Intl`'s style presets are a different
 * editorial choice — its short date drops en-US to a two-digit year, which .NET never does. The
 * build copies the patterns from `DateTimeFormatInfo`; the renderer here fills them using `Intl`
 * for the NAMES (months, weekdays, AM/PM), which is the part `Intl` gets exactly right.
 */
const PATTERN_KEYS: Record<string, string> = {
  dateShort: '$dateShort',
  dateLong: '$dateLong',
  timeShort: '$timeShort',
  timeLong: '$timeLong',
  monthDay: '$monthDay',
  yearMonth: '$yearMonth',
};

/** The pair, by BCP-47 name. Empty = the neutral/invariant start every page boots from until the
 * server's `__EQ_CULTURE__` installs the request's truth. */
export interface CulturePair {
  ui: string;
  format: string;
}

let active: CulturePair = { ui: '', format: '' };
let activeStrings: Record<string, string> = {};
const warned = new Set<string>();

/** Catalogs already in memory, by culture name — the one the shell inlined, plus any `setCulture`
 * fetched. A second switch back is instant and silent. */
const catalogs = new Map<string, Record<string, string>>();

/** What re-renders the mounted tree after a swap. Registered by boot rather than imported: this
 * module is a LEAF, and reaching the component tree from here would close an eval-time cycle —
 * the same inversion the runtime already uses twice. */
let invalidate: (() => void) | null = null;

/** Where a culture's catalog is fetched from. The SDK's own output convention, overridable so a
 * host that serves the bundle from elsewhere (or a test) can answer without a network. */
let loadCatalog: (culture: string) => Promise<Record<string, string> | null> = defaultLoader;

async function defaultLoader(culture: string): Promise<Record<string, string> | null> {
  if (typeof fetch !== 'function') return null;
  try {
    const response = await fetch(`/_equantic/strings/${culture}.json`, {
      credentials: 'same-origin',
    });
    if (!response.ok) return null;
    return (await response.json()) as Record<string, string>;
  } catch {
    // Offline, blocked, or served from a host with no catalogs: the caller falls back.
    return null;
  }
}

/**
 * Installs the ACTIVE culture and its flat catalog (`"Strings/Hero.Title"` → value) — called by
 * boot from `window.__EQ_CULTURE__` BEFORE hydration, so the client resolves exactly the strings
 * the server rendered and the SSR-identity contract holds on a translated page (D4).
 */
export interface CalendarCatalog {
  firstDayOfWeek: number;
  dayNamesShort: string[];
  dayNamesLong: string[];
  monthNames: string[];
  monthNamesShort: string[];
}

/**
 * What the SERVER said a calendar is called, for this request's format culture. Present on any
 * page the server rendered; absent in a client-only render, where Intl answers instead.
 *
 * It is shipped rather than derived because the two sides read different ICU builds and they do
 * not always agree: `ar-EG` abbreviates Sunday as "أحد" in .NET and "الأحد" in a JS runtime's ICU
 * — both correct Arabic, one with the definite article — and even two JS engines disagreed in the
 * probe. Deriving on each side would put a different label in the SSR HTML and the hydrated tree.
 */
let activeCalendar: CalendarCatalog | null = null;

/** The server's calendar names for the active culture, or null when nothing installed them. */
export function calendarCatalog(): CalendarCatalog | null {
  return activeCalendar;
}

export function installCulture(
  ui: string,
  format: string,
  strings: Record<string, string>,
  calendar?: CalendarCatalog | null,
): void {
  active = { ui, format };
  activeStrings = strings;
  // A culture change without a catalog falls back to Intl rather than keeping the OLD culture's
  // names, which would be the one answer that is certainly wrong.
  activeCalendar = calendar ?? null;
  warned.clear();
  if (ui.length > 0) catalogs.set(ui, strings);

  // The DOCUMENT's language, not just the catalog's. The server stamps `<html lang>` on the page
  // it renders and a no-reload switch left it behind, so the page said `lang="en"` while every
  // word in it was Portuguese. A screen reader takes its pronunciation from that attribute and
  // nothing else — it reads the Portuguese aloud with English phonemes — and it is the same
  // attribute a browser's translation offer and the hyphenation dictionary read. One line, at the
  // single point where the active culture changes, so no caller has to remember it.
  if (ui.length > 0 && typeof document !== 'undefined' && document.documentElement) {
    document.documentElement.lang = ui;
  }
}

/** Registers the re-render (boot) and the catalog loader (a host with its own transport). */
export function setCultureInvalidator(onChanged: () => void): void {
  invalidate = onChanged;
}

export function setCultureCatalogLoader(
  loader: (culture: string) => Promise<Record<string, string> | null>,
): void {
  loadCatalog = loader;
}

/**
 * Switches culture WITHOUT a reload (D6) — the `setPhotonTheme` shape: fetch the catalog if it is
 * not already in memory, swap it, invalidate the tree. A component reaches this through C#
 * (`ICultureController`), never through JS.
 *
 * The catalog PICK mirrors the server's exactly (exact culture → parents → neutral): the files are
 * emitted with the .NET fallback chain already flattened (D12), so this walk only chooses a FILE
 * and the lookup itself stays flat. A culture whose catalog cannot be found keeps the strings it
 * has and still switches NAMES — formats follow immediately, and the strings degrade to the
 * neutral values rather than to keys.
 */
export async function setCulture(ui: string, format?: string): Promise<void> {
  const formatName = format ?? ui;
  if (ui === active.ui && formatName === active.format) return;

  let strings = catalogs.get(ui);
  if (strings === undefined) {
    for (const candidate of [...parentChain(ui), 'neutral']) {
      const loaded = await loadCatalog(candidate);
      if (loaded !== null) {
        strings = loaded;
        break;
      }
    }
  }

  installCulture(ui, formatName, strings ?? activeStrings);
  invalidate?.();
}

/** `pt-BR` → [`pt-BR`, `pt`]. The .NET parent walk, by name, with no CultureInfo to consult. */
function parentChain(culture: string): string[] {
  const chain: string[] = [];
  let name = culture;
  while (name.length > 0) {
    chain.push(name);
    const cut = name.lastIndexOf('-');
    if (cut < 0) break;
    name = name.slice(0, cut);
  }
  return chain;
}

/** The pair in force — `str` reads ui, every D7 formatter reads format. */
export function activeCulture(): CulturePair {
  return active;
}

/**
 * The locale every formatter resolves against: the FORMAT half of the pair, or undefined while no
 * culture is installed — which is `Intl`'s "use the host's default" and the closest thing the
 * browser has to .NET's invariant start.
 */
export function formatLocale(): string | undefined {
  return active.format.length > 0 ? active.format : undefined;
}

/** The active culture's ISO currency code, or null when the catalog carries none (the invariant
 * case — .NET prints the generic ¤ sign there, and so do we). */
export function activeCurrency(): string | null {
  const code = activeStrings[CURRENCY_KEY];
  return code === undefined || code.length === 0 ? null : code;
}

/** One of the active culture's date/time patterns by role, or null when the catalog carries none
 * — the formatter then falls back to `Intl`'s own presets. */
export function activePattern(role: string): string | null {
  const key = PATTERN_KEYS[role];
  if (key === undefined) return null;
  const pattern = activeStrings[key];
  return pattern === undefined || pattern.length === 0 ? null : pattern;
}

/**
 * The runtime half of a rewritten resx accessor: `Strings.Hero_Title` arrives as
 * `$eq.str("Strings", "Hero.Title")` and resolves against the installed catalog.
 *
 * Three steps, in order: the installed catalog, then the SDK's OWN neutral strings baked into
 * this bundle (D14 — a page mounted with no bridge at all still reads "Search…" rather than the
 * raw key "SearchPlaceholder"), then the missing-key policy (W3): return the KEY and warn once.
 * A missing translation must degrade to ugly, never to a blank page or a crashed render.
 */
export function str(id: string, key: string): string {
  const flat = `${id}/${key}`;
  const value = activeStrings[flat];
  if (value !== undefined) return value;

  if (id === SDK_RESOURCE_ID) {
    const builtIn = sdkNeutralStrings[key];
    // Only when the catalog is silent: an app translating the SDK's chrome keeps its translation.
    if (builtIn !== undefined) return builtIn;
  }

  if (!warned.has(flat)) {
    warned.add(flat);
    console.warn(`[eQuantic.UI] missing string '${flat}' (culture '${active.ui}')`);
  }
  return key;
}
