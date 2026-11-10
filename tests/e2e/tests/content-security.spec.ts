import { test, expect, type Page } from '@playwright/test';

/**
 * The dashboard runs under the strict Content-Security-Policy the SDK writes (#1270), in a real
 * browser, and a single violation fails.
 *
 * The sample turns the policy on with `UseContentSecurityPolicy()` and states no source of its own:
 * every script and style the SDK writes carries the request's nonce (#1266), GTM's hosts are added by
 * the SDK (#1267), and the runtime stamps the style elements it creates (#1268). The browser is the
 * judge here, not a reading of the markup: it raises `securitypolicyviolation` for anything the policy
 * refuses, and this suite fails on the first one, naming the directive and what was blocked. Counting
 * that nothing was blocked is not enough, so the listener is installed before the page's first
 * script runs, and the policy itself is asserted on each response.
 */

/** Every violation the page raises from its first byte, as "directive refused source (sample)". */
async function watchViolations(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const seen: string[] = [];
    (window as unknown as { __violations: string[] }).__violations = seen;
    document.addEventListener(
      'securitypolicyviolation',
      (event) => {
        const source = event.blockedURI || 'inline';
        seen.push(`${event.effectiveDirective} refused ${source} at ${event.sourceFile}:${event.lineNumber}` +
          (event.sample ? ` (${event.sample})` : ''));
      },
      true,
    );
  });
}

async function violationsOf(page: Page): Promise<string[]> {
  // A violation is reported asynchronously, after the work that caused it: give the page a moment.
  await page.waitForTimeout(500);
  // Taken out as read, so a client navigation reports only what it caused itself.
  return page.evaluate(() => (window as unknown as { __violations: string[] }).__violations.splice(0));
}

/** The dashboard's own routes, read from the navigation the home page draws. */
async function routesOf(page: Page): Promise<string[]> {
  await page.goto('/');
  const hrefs = await page.locator('a[href^="/"]').evaluateAll((anchors) =>
    anchors.map((anchor) => (anchor as HTMLAnchorElement).getAttribute('href') ?? ''));
  return [...new Set(['/', ...hrefs.map((href) => href.split('#')[0].split('?')[0])])].filter(Boolean);
}

test.describe('The dashboard under the strict Content-Security-Policy', () => {
  test('every page is served with the policy, and loads with no violation', async ({ page }) => {
    test.setTimeout(180_000); // every screen of the dashboard, each a full load
    await watchViolations(page);
    const routes = await routesOf(page);
    expect(routes.length, 'the home page links the dashboard\'s screens').toBeGreaterThan(5);

    const offences: string[] = [];
    for (const route of routes) {
      const response = await page.goto(route);
      const policy = response?.headers()['content-security-policy'] ?? '';
      expect(policy, `${route} is served with the enforced policy`).toContain("'strict-dynamic'");
      // `load`, never `networkidle`: the live screen holds a server-events stream open for good.
      await page.waitForLoadState('load');
      for (const violation of await violationsOf(page)) offences.push(`${route}: ${violation}`);
    }

    expect(offences, 'a strict policy refuses nothing the SDK or the sample writes').toEqual([]);
  });

  test('client navigation through every screen raises no violation', async ({ page }) => {
    test.setTimeout(180_000);
    await watchViolations(page);
    const routes = await routesOf(page);

    // What the home page's own load raised is the home page's, not the first route's.
    const offences = (await violationsOf(page)).map((violation) => `/: ${violation}`);
    for (const route of routes.filter((route) => route !== '/')) {
      // A link the router owns: the page is not reloaded, so the runtime builds the screen, and the
      // styles and scripts it adds are judged by the policy the first document was served with.
      // A screen that does not draw the navigation is left for the home page, which links every
      // route, so no route is skipped in silence.
      // The route was read with its query and fragment cut off, so a link that carries one counts.
      const link = page.locator(`a[href="${route}"], a[href^="${route}?"], a[href^="${route}#"]`).first();
      if (await link.count() === 0) {
        await page.goto('/');
        for (const violation of await violationsOf(page)) offences.push(`/: ${violation}`);
      }
      await expect(link, `${route} is linked from the page the navigation starts on`).toHaveCount(1);
      await link.click();
      // The path alone: a link that carries a query or a fragment lands on the route with it.
      await page.waitForURL((url) => url.pathname === route);
      for (const violation of await violationsOf(page)) offences.push(`${route}: ${violation}`);
    }

    expect(offences, 'the runtime adds nothing the policy refuses').toEqual([]);
  });
});
