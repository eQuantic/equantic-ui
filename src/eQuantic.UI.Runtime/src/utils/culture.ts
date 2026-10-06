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
 * The symbols of a culture's `NumberFormatInfo` the formatter writes by hand, under .NET's own
 * names: what a number's text with no specifier, `E`, `G`, `D`, a custom picture and a value that is
 * not a finite number are written in. `Intl` lays out `N`, `F`, `C` and `P` itself.
 */
export interface NumberFormatData {
  numberDecimalSeparator: string;
  numberGroupSeparator: string;
  /** The group next to the point first, as .NET's are: en-IN's are `[3, 2]`. */
  numberGroupSizes: number[];
  negativeSign: string;
  positiveSign: string;
  percentSymbol: string;
  perMilleSymbol: string;
  nanSymbol: string;
  positiveInfinitySymbol: string;
  negativeInfinitySymbol: string;
}

/**
 * The patterns and symbols of a culture's `DateTimeFormatInfo` the formatter draws a date with:
 * what a standard specifier stands for (`d` IS the short date pattern), what `/` and `:` write in a
 * picture, and the era `g` writes.
 */
export interface DateTimeFormatData {
  shortDatePattern: string;
  longDatePattern: string;
  shortTimePattern: string;
  longTimePattern: string;
  monthDayPattern: string;
  yearMonthPattern: string;
  dateSeparator: string;
  timeSeparator: string;
  eraName: string;
  /** Sunday first, as `System.DayOfWeek` counts. */
  dayNames: string[];
  abbreviatedDayNames: string[];
  /** January first, twelve of them. */
  monthNames: string[];
  abbreviatedMonthNames: string[];
  /** What a month beside its day is called, where the culture says it otherwise (ru, pl). */
  monthGenitiveNames: string[];
  abbreviatedMonthGenitiveNames: string[];
  amDesignator: string;
  pmDesignator: string;
}

/**
 * What the formatter reads of a FORMAT culture, written by the server from .NET's own
 * `NumberFormatInfo` and `DateTimeFormatInfo` (`CultureFormatBridge`), the way Flutter's `intl` reads
 * a locale's `NumberSymbols` and `DateSymbols` rather than the platform's. It travels with every
 * page, in `__EQ_CULTURE__`, whether or not the app has a single string to translate (#471), and a
 * switch fetches it from the server (`/_equantic/culture/{name}.json`). It is shipped rather than
 * derived because the two sides read different ICU builds: `ar` writes its minus sign with a mark
 * that `Intl` keeps outside the sign, and its exponent's plus with another.
 */
export interface CultureFormat {
  /** The ISO code of the culture's currency, or null where .NET writes the generic ¤: the invariant
   * culture, a neutral one, a culture of no single country (`es-419`). */
  isoCurrencySymbol: string | null;
  numberFormat: NumberFormatData;
  dateTimeFormat: DateTimeFormatData;
}

const INVARIANT_DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const INVARIANT_MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

/**
 * .NET's `CultureInfo.InvariantCulture`, which is the culture a page is in until the server says
 * otherwise and whenever a conversion names it: what `CultureFormatBridge` writes for it, byte for
 * byte (`format-subset.spec.ts` compares the two).
 */
export const INVARIANT_FORMAT: CultureFormat = Object.freeze({
  isoCurrencySymbol: null,
  numberFormat: Object.freeze({
    numberDecimalSeparator: '.',
    numberGroupSeparator: ',',
    numberGroupSizes: [3],
    negativeSign: '-',
    positiveSign: '+',
    percentSymbol: '%',
    perMilleSymbol: '‰',
    nanSymbol: 'NaN',
    positiveInfinitySymbol: 'Infinity',
    negativeInfinitySymbol: '-Infinity',
  }),
  dateTimeFormat: Object.freeze({
    shortDatePattern: 'MM/dd/yyyy',
    longDatePattern: 'dddd, dd MMMM yyyy',
    shortTimePattern: 'HH:mm',
    longTimePattern: 'HH:mm:ss',
    monthDayPattern: 'MMMM dd',
    yearMonthPattern: 'yyyy MMMM',
    dateSeparator: '/',
    timeSeparator: ':',
    eraName: 'A.D.',
    dayNames: INVARIANT_DAYS,
    abbreviatedDayNames: INVARIANT_DAYS.map((day) => day.slice(0, 3)),
    monthNames: INVARIANT_MONTHS,
    abbreviatedMonthNames: INVARIANT_MONTHS.map((month) => month.slice(0, 3)),
    monthGenitiveNames: INVARIANT_MONTHS,
    abbreviatedMonthGenitiveNames: INVARIANT_MONTHS.map((month) => month.slice(0, 3)),
    amDesignator: 'AM',
    pmDesignator: 'PM',
  }),
});

/** The patterns a one-letter standard specifier stands for, by .NET's names for them. */
export type DatePatternName =
  | 'shortDatePattern'
  | 'longDatePattern'
  | 'shortTimePattern'
  | 'longTimePattern'
  | 'monthDayPattern'
  | 'yearMonthPattern';

/** The formatter's names for the patterns a standard specifier stands for, as the data names them. */
const PATTERNS: Readonly<Record<string, DatePatternName>> = {
  dateShort: 'shortDatePattern',
  dateLong: 'longDatePattern',
  timeShort: 'shortTimePattern',
  timeLong: 'longTimePattern',
  monthDay: 'monthDayPattern',
  yearMonth: 'yearMonthPattern',
};

/** The pair, by BCP-47 name. Empty = the invariant culture every page boots in until the server's
 * `__EQ_CULTURE__` installs the request's truth. */
export interface CulturePair {
  ui: string;
  format: string;
}

let active: CulturePair = { ui: '', format: '' };
let activeStrings: Record<string, string> = {};
/** The format data of the active format culture, or null when it did not travel (a switch with no
 * server to ask), where the formatter reads `Intl` instead. */
let activeFormat: CultureFormat | null = INVARIANT_FORMAT;
const warned = new Set<string>();

/** Catalogs already in memory, by culture name — the one the shell inlined, plus any `setCulture`
 * fetched. A second switch back is instant and silent. */
const catalogs = new Map<string, Record<string, string>>();

/** Format data already in memory, by FORMAT culture name, with the calendar names that came with it. */
const formats = new Map<string, { format: CultureFormat; calendar: CalendarCatalog | null }>();

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

/** A FORMAT culture's data as the server answers for it: what the shell inlines for the culture a
 * page boots in, for one it switches to. */
export interface CultureFormatDocument {
  format: CultureFormat;
  calendar?: CalendarCatalog | null;
}

/** Where a format culture's data is fetched from: the server, which writes it from .NET as it
 * writes the shell's, overridable as the catalog loader is. */
let loadFormat: (culture: string) => Promise<CultureFormatDocument | null> = defaultFormatLoader;

async function defaultFormatLoader(culture: string): Promise<CultureFormatDocument | null> {
  if (typeof fetch !== 'function') return null;
  try {
    const response = await fetch(`/_equantic/culture/${encodeURIComponent(culture)}.json`, {
      credentials: 'same-origin',
    });
    if (!response.ok) return null;
    return (await response.json()) as CultureFormatDocument;
  } catch {
    // No server behind the page: the formatter reads `Intl` for this culture instead.
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

/**
 * Installs the pair, the UI culture's strings, and what the FORMAT culture's calendar is called and
 * how it formats. The invariant culture (an empty format name) needs no data: the runtime carries
 * it. A culture installed with no data is formatted through `Intl`, the nearest the browser has.
 */
export function installCulture(
  ui: string,
  format: string,
  strings: Record<string, string>,
  calendar?: CalendarCatalog | null,
  formatData?: CultureFormat | null,
): void {
  active = { ui, format };
  activeStrings = strings;
  // A culture change without a catalog falls back to Intl rather than keeping the OLD culture's
  // names, which would be the one answer that is certainly wrong.
  activeCalendar = calendar ?? null;
  activeFormat = formatData ?? (format.length === 0 ? INVARIANT_FORMAT : null);
  warned.clear();
  if (ui.length > 0) catalogs.set(ui, strings);
  if (format.length > 0 && formatData) formats.set(format, { format: formatData, calendar: calendar ?? null });

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

/** Registers where a format culture's data comes from, for a host with its own transport; with
 * none, it is the server's own endpoint again. */
export function setCultureFormatLoader(
  loader: (culture: string) => Promise<CultureFormatDocument | null> = defaultFormatLoader,
): void {
  loadFormat = loader;
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
 * neutral values rather than to keys. The FORMAT culture's data comes from the server, beside the
 * catalog, so a number reads after a switch as the server will write it on the next request.
 */
export async function setCulture(ui: string, format?: string): Promise<void> {
  const formatName = format ?? ui;
  if (ui === active.ui && formatName === active.format) return;

  const [strings, data] = await Promise.all([catalogFor(ui), formatFor(formatName)]);
  installCulture(ui, formatName, strings ?? activeStrings, data?.calendar, data?.format);
  invalidate?.();
}

/** The UI culture's catalog, from memory or by the server's walk, or undefined when none answers. */
async function catalogFor(ui: string): Promise<Record<string, string> | undefined> {
  const known = catalogs.get(ui);
  if (known !== undefined) return known;
  for (const candidate of [...parentChain(ui), 'neutral']) {
    const loaded = await loadCatalog(candidate);
    if (loaded !== null) return loaded;
  }
  return undefined;
}

/** The format culture's data, from memory or from the server, or null when neither has it. */
async function formatFor(format: string): Promise<CultureFormatDocument | null> {
  if (format.length === 0) return null;
  return formats.get(format) ?? (await loadFormat(format));
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
 * culture is installed, which is the invariant culture: the formatter asks `Intl` for the locale
 * nearest it then, and never for the host's own (#471).
 */
export function formatLocale(): string | undefined {
  return active.format.length > 0 ? active.format : undefined;
}

/**
 * The format data of the active format culture: the invariant culture's while none is installed,
 * and null for a culture whose data did not travel, which the formatter reads through `Intl`.
 */
export function activeFormatData(): CultureFormat | null {
  return activeFormat;
}

/** One of the active culture's date/time patterns by the formatter's name for it (`dateShort` is
 * the short date pattern), or null when the culture's data did not travel. */
export function activePattern(role: string): string | null {
  const key = PATTERNS[role];
  if (key === undefined || activeFormat === null) return null;
  return activeFormat.dateTimeFormat[key];
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
