/**
 * A client navigation's page state: the page's server data and the document's metadata, asked of the
 * target route and applied to the hydration door and the head. Its own module, out of the boot, so
 * the client half of the contract runs under test as the server half does (ClientNavigationStateTests).
 */

/**
 * The header a client navigation sends, and the server's answer carries back: the route answers JSON
 * for the page's state instead of a document, and marks it as that answer whatever its status.
 */
export const NAVIGATION_HEADER = 'X-EQ-Navigate';

export interface PageStatePayload {
  title?: string;
  head?: string;
  /**
   * One field map PER COMPONENT, under the name the server's realizer gave it (`Type#ordinal`).
   * It was a flat field map while only a page root could prefetch; the extra level is what lets
   * two components holding a field of the same name keep their own values.
   */
  state?: Record<string, Record<string, unknown>>;
}

/**
 * Payloads already asked for, by href. The router warms a link on hover and dedupes per link, so
 * this holds at most one entry per link the reader pointed at — and the click that follows finds
 * the answer already on its way, or already here.
 *
 * Consumed ONCE. The data is the page's, not the session's: coming back to a page asks again, which
 * is the same thing a full load would do.
 */
const warmedState = new Map<string, Promise<PageStatePayload | null>>();

/** Starts the fetch a hover suggests is coming. Best-effort, exactly like the bundle beside it. */
export function warmPageState(url: string): void {
  if (warmedState.has(url)) return;
  warmedState.set(url, fetchPageState(url));
}

/**
 * The page's server data and metadata, asked of the TARGET URL itself.
 *
 * Not a side endpoint: the request goes to the route being navigated to, carrying a header, and the
 * page route answers with JSON instead of a document. That is what makes the route params, the query
 * and the page resolution the ones a full load would have — they ARE a full load's, minus the HTML.
 *
 * A failure is not fatal. The page then renders exactly what it rendered before this existed.
 */
export async function fetchPageState(url?: string): Promise<PageStatePayload | null> {
  if (!url || typeof fetch !== 'function') return null;
  const warmed = warmedState.get(url);
  if (warmed) {
    warmedState.delete(url);
    return warmed;
  }
  try {
    const response = await fetch(url, {
      headers: { [NAVIGATION_HEADER]: '1' },
      credentials: 'same-origin',
    });
    // A page answers with its own status, a 404 for content that does not exist among them, and
    // still sends its payload, which the server marks with the header the request carried. Anything
    // else that fails (a proxy's error page) is no payload.
    if (!response.ok && response.headers.get(NAVIGATION_HEADER) !== '1') return null;
    return (await response.json()) as PageStatePayload;
  } catch {
    return null;
  }
}

/**
 * Hands the payload to the two places that read it: the hydration door the SSR state comes through,
 * and the document head.
 *
 * The head's METADATA is replaced as a set. Every tag the server's metadata writes carries
 * `data-eq-meta`, on a full load and in a navigation's payload alike, so the previous page's set is
 * removed whole and the next page's written in its place: a canonical is one statement about one
 * document, and a description, a canonical or a translation group the next page does not have was
 * left standing when tags were matched by name alone. A tag without the marker is still patched by
 * IDENTITY (the attribute that names it), so nothing is duplicated.
 */
export function applyPageState(payload: PageStatePayload | null): void {
  // REPLACED, INCLUDING WITH NOTHING. The payload used to be deleted by whoever read it, so a
  // navigation to a page that prefetches nothing simply found none. It is per-component now and
  // nobody consumes it, so leaving the previous page's entries in place would let A → B → A hydrate
  // A from state the server never sent for that visit.
  const w = window as unknown as { __INITIAL_STATE__?: Record<string, Record<string, unknown>> };
  if (payload?.state && typeof payload.state === 'object') {
    w.__INITIAL_STATE__ = payload.state;
  } else {
    delete w.__INITIAL_STATE__;
  }

  if (!payload) return;
  if (typeof payload.title === 'string' && payload.title.length > 0) {
    document.title = payload.title;
  }
  if (typeof payload.head !== 'string') return;

  // Removed even when the next page writes none: an empty set is still the next page's set.
  for (const stale of Array.from(document.head.querySelectorAll('[data-eq-meta]'))) stale.remove();
  if (payload.head.length === 0) return;
  const template = document.createElement('template');
  template.innerHTML = payload.head;
  for (const incoming of Array.from(template.content.children)) {
    const selector = incoming.hasAttribute('data-eq-meta') ? null : headSelectorFor(incoming);
    const existing = selector ? document.head.querySelector(selector) : null;
    if (existing) existing.replaceWith(incoming);
    else document.head.appendChild(incoming);
  }
}

/** What makes a head tag THE one it is: the attribute that names it. */
function headSelectorFor(element: Element): string | null {
  const tag = element.tagName.toLowerCase();
  for (const attribute of ['name', 'property', 'rel']) {
    const value = element.getAttribute(attribute);
    if (value) return `${tag}[${attribute}="${CSS.escape(value)}"]`;
  }
  return null;
}
