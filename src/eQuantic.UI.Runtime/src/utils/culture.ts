/**
 * Track L (docs/I18N-PLAN.md D13/W3): the client's culture state — the mirror of .NET's pair,
 * because .NET's IS a pair: `CurrentUICulture` picks RESOURCES, `CurrentCulture` picks FORMATS,
 * and collapsing them would quietly depart from the experience the track exists to reproduce.
 *
 * A LEAF module on purpose: `eq.ts` and `utils/format.ts` read it, and a store any deeper in the
 * graph would close an eval-time cycle — the module-cycle law this tree already enforces twice.
 * Its imports are GENERATED: data with no imports of its own, and a type the compiler erases, so the
 * leaf stays a leaf.
 */

import { sdkNeutralStrings } from '../shared/sdk-strings.generated';
import type { TextDirectionValue } from '../shared/enums.generated';

/** The catalog id the SDK's own strings live under — the class name eqc rewrites against. */
const SDK_RESOURCE_ID = 'SdkResources';

/**
 * A culture's `NumberFormatInfo`, under .NET's own names: what the formatter writes every number in,
 * a number's text with no specifier, every standard specifier and a custom picture (#634). The
 * patterns are .NET's indexes into its own pattern tables (`NumberNegativePattern` 1 is `-n`).
 */
export interface NumberFormatData {
  numberDecimalSeparator: string;
  numberGroupSeparator: string;
  /** The group next to the point first, as .NET's are: en-IN's are `[3, 2]`. */
  numberGroupSizes: number[];
  /** What `N` and `F` write with no precision: 3 in every specific culture on ICU, 2 on NLS. */
  numberDecimalDigits: number;
  numberNegativePattern: number;
  negativeSign: string;
  positiveSign: string;
  currencySymbol: string;
  currencyDecimalSeparator: string;
  currencyGroupSeparator: string;
  currencyGroupSizes: number[];
  currencyDecimalDigits: number;
  currencyPositivePattern: number;
  currencyNegativePattern: number;
  percentSymbol: string;
  percentDecimalSeparator: string;
  percentGroupSeparator: string;
  percentGroupSizes: number[];
  percentDecimalDigits: number;
  percentPositivePattern: number;
  percentNegativePattern: number;
  perMilleSymbol: string;
  nanSymbol: string;
  positiveInfinitySymbol: string;
  negativeInfinitySymbol: string;
}

/**
 * The patterns, symbols and names of a culture's `DateTimeFormatInfo`: what the formatter draws a
 * date with (`d` IS the short date pattern, `/` and `:` its separators in a picture, `g` its era),
 * and what a calendar says, its first day of the week and its day and month names (`CalendarNames`).
 * One copy of each: the calendar's names travelled beside these as a catalog of their own.
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
  /** The era's abbreviation, which .NET reads beside a date as it reads its name (ar-SA's `هـ`). */
  abbreviatedEraName: string;
  /** The day a week starts on, as `System.DayOfWeek` counts: 0 is Sunday. */
  firstDayOfWeek: number;
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
  /** The calendar every number of a date above is counted in (#1191). */
  calendar: CalendarData;
}

/**
 * A culture's calendar, as the server measures .NET's own (`CalendarBridge`): the Gregorian months under
 * another count of years (`yearOffset`, 543 for th-TH's Thai Buddhist calendar, 0 for the Gregorian one),
 * or months of its own (`months`, fa-IR's Persian calendar and ar-SA's Um Al Qura one). `utils/calendar.ts`
 * reads it.
 */
export interface CalendarData {
  /** The calendar's type, which .NET names when it refuses a clock: `System.Globalization.GregorianCalendar`. */
  name: string;
  /** The last year a year of two digits reads as: 2049 in the Gregorian calendar, 2572 in the Thai Buddhist. */
  twoDigitYearMax: number;
  /** The years its count is ahead of the Gregorian one, for a calendar of the Gregorian months. */
  yearOffset?: number;
  /** The months of a calendar of its own. */
  months?: CalendarMonths;
  /** The first and the last day it writes, as days since 0001-01-01, where they are not a DateTime's. */
  firstDay?: number;
  lastDay?: number;
  /** .NET's refusal of a day outside them, the sentence before the parameter's name. */
  outOfRange?: string;
  /** Whether a DateTime of the first day writes its text with no format in the invariant sortable form,
   * as .NET does where its calendar cannot write the year 1. */
  timeOfDayInvariant?: boolean;
}

/** Twelve months of a calendar's own, year by year from `firstYear`, which starts on day `start`. */
export interface CalendarMonths {
  firstYear: number;
  /** The day the first year starts on, as days since 0001-01-01. */
  start: number;
  /** How many years it counts. */
  years: number;
  /** Each month's shorter length, the first month first. */
  lengths: number[];
  /** The months, 1 to 12, whose length varies from one year to another. */
  varying: number[];
  /** For every year, a bit for each month in `varying`, set where that month is a day longer: base64. */
  longer: string;
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
  numberFormat: Object.freeze({
    numberDecimalSeparator: '.',
    numberGroupSeparator: ',',
    numberGroupSizes: [3],
    numberDecimalDigits: 2,
    numberNegativePattern: 1,
    negativeSign: '-',
    positiveSign: '+',
    currencySymbol: '¤',
    currencyDecimalSeparator: '.',
    currencyGroupSeparator: ',',
    currencyGroupSizes: [3],
    currencyDecimalDigits: 2,
    currencyPositivePattern: 0,
    currencyNegativePattern: 0,
    percentSymbol: '%',
    percentDecimalSeparator: '.',
    percentGroupSeparator: ',',
    percentGroupSizes: [3],
    percentDecimalDigits: 2,
    percentPositivePattern: 0,
    percentNegativePattern: 0,
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
    abbreviatedEraName: 'AD',
    firstDayOfWeek: 0,
    dayNames: INVARIANT_DAYS,
    abbreviatedDayNames: INVARIANT_DAYS.map((day) => day.slice(0, 3)),
    monthNames: INVARIANT_MONTHS,
    abbreviatedMonthNames: INVARIANT_MONTHS.map((month) => month.slice(0, 3)),
    monthGenitiveNames: INVARIANT_MONTHS,
    abbreviatedMonthGenitiveNames: INVARIANT_MONTHS.map((month) => month.slice(0, 3)),
    amDesignator: 'AM',
    pmDesignator: 'PM',
    calendar: Object.freeze({ name: 'System.Globalization.GregorianCalendar', twoDigitYearMax: 2049, yearOffset: 0 }),
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

/** The cultures whose WHOLE catalog is in memory (#1782). The shell inlines only the strings its page
 * reads and a navigation adds the next page's, so a key read beyond them asks for the rest once. */
const whole = new Set<string>();

/** The cultures whose whole catalog is being asked for, so a burst of misses asks once. */
const completing = new Set<string>();

/** The catalogs the build wrote, as the server listed them for the page (`neutral` among them), or
 * null where no server said. A culture with no catalog of its own was asked for one and answered
 * 404 before its parents or `neutral` were tried (#825). Keyed by the name in lower case, as .NET
 * takes a culture's name in any case and the file is written in the canonical one. */
let writtenCatalogs: ReadonlyMap<string, string> | null = null;

/** Format data already in memory, by FORMAT culture name. */
const formats = new Map<string, CultureFormat>();

/** Which way the UI culture's text runs: left to right until the server says otherwise, as the
 * invariant culture's does. */
let activeDirection: TextDirectionValue = 'leftToRight';

/** Which way a culture's text runs, by name, as the server said it (.NET's `TextInfo.IsRightToLeft`):
 * the browser keeps no list of languages of its own, which would be a second answer that drifts from ICU. */
const directions = new Map<string, TextDirectionValue>();

/** The cultures whose document was asked for, answered or not: a switch asks the server once. */
const askedForDirection = new Set<string>();

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
  /** Whether the culture's text runs right to left (.NET's `TextInfo.IsRightToLeft`). */
  rightToLeft?: boolean;
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
 * the server rendered and the SSR-identity contract holds on a translated page (D4) — with how the
 * FORMAT culture formats and what its calendar is called. The invariant culture (an empty format
 * name) needs no data: the runtime carries it. A culture installed with no data is formatted and
 * named through `Intl`, the nearest the browser has, rather than with the OLD culture's data, which
 * would be the one answer that is certainly wrong.
 */
export function installCulture(
  ui: string,
  format: string,
  strings: Record<string, string>,
  formatData?: CultureFormat | null,
  written?: readonly string[],
  /** Whether the UI culture's text runs right to left, as the server said; null or absent where it did
   * not say, and then the direction the culture was last said to have stands, or the one in force. */
  rightToLeft?: boolean | null,
): void {
  active = { ui, format };
  if (typeof rightToLeft === 'boolean') directions.set(ui, rightToLeft ? 'rightToLeft' : 'leftToRight');
  activeDirection = directions.get(ui) ?? (ui.length === 0 ? 'leftToRight' : activeDirection);
  activeStrings = strings;
  // What was installed is what a page was handed, until a switch says it fetched the whole (#1782).
  whole.delete(ui);
  activeFormat = formatData ?? (format.length === 0 ? INVARIANT_FORMAT : null);
  warned.clear();
  if (ui.length > 0) catalogs.set(ui, strings);
  if (format.length > 0 && formatData) formats.set(format, formatData);
  if (written !== undefined) writtenCatalogs = new Map(written.map((name) => [name.toLowerCase(), name]));

  // The DOCUMENT's language, not just the catalog's. The server stamps `<html lang>` on the page
  // it renders and a no-reload switch left it behind, so the page said `lang="en"` while every
  // word in it was Portuguese. A screen reader takes its pronunciation from that attribute and
  // nothing else — it reads the Portuguese aloud with English phonemes — and it is the same
  // attribute a browser's translation offer and the hyphenation dictionary read. One line, at the
  // single point where the active culture changes, so no caller has to remember it.
  if (ui.length > 0 && typeof document !== 'undefined' && document.documentElement) {
    document.documentElement.lang = ui;
  }
  // And which way it reads, beside the language and for the same reason: the server stamps
  // `<html dir>` from the culture, and a switch to Arabic that left `ltr` there laid the page out as
  // English. Every start and end the lowering writes is logical CSS, so this one attribute mirrors them.
  if (typeof document !== 'undefined' && document.documentElement) {
    document.documentElement.dir = activeDirection === 'rightToLeft' ? 'rtl' : 'ltr';
  }
}

/**
 * Adds a page's strings to the active culture's catalog (#1782): what a navigation's payload brings of
 * the page it opens, which the document it started from was not handed.
 */
export function addStrings(strings: Record<string, string>): void {
  activeStrings = { ...activeStrings, ...strings };
  if (active.ui.length > 0) catalogs.set(active.ui, activeStrings);
}

/**
 * Asks once for the active culture's whole catalog, after a key it was not handed was read (#1782), and
 * draws again with it. A page is handed the strings its modules read; one read some other way (a key a
 * build could not see) still arrives, a moment later, rather than as its key for good.
 */
function completeActiveCatalog(): void {
  const ui = active.ui;
  if (whole.has(ui) || completing.has(ui)) return;
  completing.add(ui);
  void (async () => {
    try {
      for (const candidate of [...parentChain(ui), 'neutral']) {
        const name = catalogToAskFor(candidate);
        if (name === null) continue;
        const loaded = await loadCatalog(name);
        if (loaded === null) continue;
        whole.add(ui);
        if (active.ui === ui) {
          addStrings(loaded);
          warned.clear();
          invalidate?.();
        }
        return;
      }
      whole.add(ui);
    } finally {
      completing.delete(ui);
    }
  })();
}

/** Registers the re-render (boot) and the catalog loader (a host with its own transport). */
export function setCultureInvalidator(onChanged: () => void): void {
  invalidate = onChanged;
}

/** Registers where a catalog comes from, for a host with its own transport; with none, it is the
 * build's own files again. */
export function setCultureCatalogLoader(
  loader: (culture: string) => Promise<Record<string, string> | null> = defaultLoader,
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
 * and the lookup itself stays flat, and it asks for no file the build did not write. A culture
 * whose catalog cannot be found keeps the strings it has and still switches NAMES — formats follow
 * immediately, and the strings degrade to the neutral values rather than to keys. The FORMAT culture's data comes from the server, beside the
 * catalog, so a number reads after a switch as the server will write it on the next request.
 */
export async function setCulture(ui: string, format?: string): Promise<void> {
  const formatName = format ?? ui;
  if (ui === active.ui && formatName === active.format) return;

  const formatting = formatFor(formatName);
  // The UI culture's direction, beside the rest: the format culture's document says it when the two are
  // one, so it is read after that document arrives rather than asked for twice.
  const direction = ui === formatName ? formatting.then(() => rightToLeftOf(ui)) : rightToLeftOf(ui);
  const [found, data, rightToLeft] = await Promise.all([catalogFor(ui), formatting, direction]);
  installCulture(ui, formatName, found?.strings ?? activeStrings, data, undefined, rightToLeft);
  if (found?.whole) whole.add(ui);
  invalidate?.();
}

/** Whether `ui`'s text runs right to left, from memory or from the server's document for it, or null
 * when neither says (a switch with no server to ask). */
async function rightToLeftOf(ui: string): Promise<boolean | null> {
  if (ui.length === 0) return false;
  if (!directions.has(ui) && !askedForDirection.has(ui)) rememberDirection(ui, await loadFormat(ui));
  const known = directions.get(ui);
  return known === undefined ? null : known === 'rightToLeft';
}

/** Records which way a culture's text runs, when its document says, and that it was asked. */
function rememberDirection(culture: string, answer: CultureFormatDocument | null): void {
  askedForDirection.add(culture);
  if (typeof answer?.rightToLeft === 'boolean') {
    directions.set(culture, answer.rightToLeft ? 'rightToLeft' : 'leftToRight');
  }
}

/** The UI culture's catalog, from memory or by the server's walk, and whether it is the whole one, or
 * undefined when none answers. */
async function catalogFor(ui: string): Promise<{ strings: Record<string, string>; whole: boolean } | undefined> {
  const known = catalogs.get(ui);
  if (known !== undefined) return { strings: known, whole: whole.has(ui) };
  for (const candidate of [...parentChain(ui), 'neutral']) {
    const name = catalogToAskFor(candidate);
    if (name === null) continue;
    const loaded = await loadCatalog(name);
    if (loaded !== null) return { strings: loaded, whole: true };
  }
  return undefined;
}

/** The catalog to ask for `culture`, by the name the build wrote it under, or null when it wrote
 * none. The list speaks for the build's own files: a host with a loader of its own answers for itself. */
function catalogToAskFor(culture: string): string | null {
  if (loadCatalog !== defaultLoader || writtenCatalogs === null) return culture;
  return writtenCatalogs.get(culture.toLowerCase()) ?? null;
}

/** The format culture's data, from memory or from the server, or null when neither has it. */
async function formatFor(format: string): Promise<CultureFormat | null> {
  if (format.length === 0) return null;
  const known = formats.get(format);
  if (known !== undefined) return known;
  const answer = await loadFormat(format);
  // The same document says which way the culture reads, which a switch to it as the UI culture reads.
  rememberDirection(format, answer);
  return answer?.format ?? null;
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

/**
 * Which way the UI culture's text runs: what a component reads as its `textDirection` when no
 * `Directionality` stands above it (the C# `CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft`).
 */
export function activeTextDirection(): TextDirectionValue {
  return activeDirection;
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

  // Not handed with the page (#1782): the rest of the catalog is asked for, and the tree drawn again
  // with it. Only a key the whole catalog lacks is warned about.
  if (active.ui.length > 0 && !whole.has(active.ui)) {
    completeActiveCatalog();
    return key;
  }

  if (!warned.has(flat)) {
    warned.add(flat);
    console.warn(`[eQuantic.UI] missing string '${flat}' (culture '${active.ui}')`);
  }
  return key;
}
