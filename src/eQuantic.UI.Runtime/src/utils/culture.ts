/**
 * Track L (docs/I18N-PLAN.md D13/W3): the client's culture state — the mirror of .NET's pair,
 * because .NET's IS a pair: `CurrentUICulture` picks RESOURCES, `CurrentCulture` picks FORMATS,
 * and collapsing them would quietly depart from the experience the track exists to reproduce.
 *
 * A LEAF module on purpose (imports nothing from shared/ or core/): `eq.ts` and `utils/format.ts`
 * read it, and a store any deeper in the graph would close an eval-time cycle — the module-cycle
 * law this tree already enforces twice.
 */

/** The pair, by BCP-47 name. Empty = the neutral/invariant start every page boots from until the
 * server's `__EQ_CULTURE__` installs the request's truth. */
export interface CulturePair {
  ui: string;
  format: string;
}

let active: CulturePair = { ui: '', format: '' };
let activeStrings: Record<string, string> = {};
const warned = new Set<string>();

/**
 * Installs the ACTIVE culture and its flat catalog (`"Strings/Hero.Title"` → value) — called by
 * boot from `window.__EQ_CULTURE__` BEFORE hydration, so the client resolves exactly the strings
 * the server rendered and the SSR-identity contract holds on a translated page (D4).
 */
export function installCulture(ui: string, format: string, strings: Record<string, string>): void {
  active = { ui, format };
  activeStrings = strings;
  warned.clear();
}

/** The pair in force — `str` reads ui; the D7 formatters read format when the subset lands. */
export function activeCulture(): CulturePair {
  return active;
}

/**
 * The runtime half of a rewritten resx accessor: `Strings.Hero_Title` arrives as
 * `$eq.str("Strings", "Hero.Title")` and resolves against the installed catalog. Missing-key
 * policy (W3): return the KEY and warn once — a missing translation must degrade to ugly, never
 * to a blank page or a crashed render.
 */
export function str(id: string, key: string): string {
  const flat = `${id}/${key}`;
  const value = activeStrings[flat];
  if (value !== undefined) return value;
  if (!warned.has(flat)) {
    warned.add(flat);
    console.warn(`[eQuantic.UI] missing string '${flat}' (culture '${active.ui}')`);
  }
  return key;
}
