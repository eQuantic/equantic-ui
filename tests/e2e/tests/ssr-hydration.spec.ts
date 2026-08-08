import { test, expect } from '@playwright/test';

/**
 * Write-once smoke suite against samples/DefaultUIDashboard — the THREE product guarantees
 * the e2e layer exists to prove on a real browser + real server:
 *
 *   1. Hydration by identity: the classes the client computes for the tree are byte-identical
 *      to the classes the SSR emitted (the atomic style engine's contract — adoption, not repaint).
 *   2. Adopted DOM is ALIVE: events attach to server-rendered markup (no dead buttons).
 *   3. The SPA router owns internal navigation: Link anchors navigate without a full reload.
 */

/** Per-element class lists (each sorted), in document order — the hydration identity fingerprint. */
function classFingerprint(html: string): string[] {
  const matches = html.match(/class="([^"]*)"/g) ?? [];
  return matches.map((m) =>
    m
      .replace(/^class="|"$/g, '')
      .split(/\s+/)
      .filter(Boolean)
      .sort()
      .join(' '),
  );
}

test.describe('Write-once SSR + hydration', () => {
  test('hydration keeps the SSR class identity on the showroom', async ({ page }) => {
    // The whole tree — static AND the stateful/animated family — now adopts by identity. The three
    // divergences this test caught are fixed and pinned in unit tests: FlexNode's ES2022 `cross`
    // (vocabulary.spec.ts), the Spacer counterweight's B14 transition missing from the C# realizer
    // (WebRealizerTests), and eqc leaving an unset enum property `undefined` instead of its zero
    // member, which grew presence dots the SSR never emitted (vocabulary.spec.ts).

    // The RAW server response, before any script runs — no race with the runtime boot.
    const response = await page.request.get('/');
    expect(response.status()).toBe(200);
    const ssrHtml = await response.text();
    const appStart = ssrHtml.indexOf('<div id="app"');
    expect(appStart).toBeGreaterThan(-1);
    expect(ssrHtml).toContain('data-ssr="true"');
    const ssrClasses = classFingerprint(ssrHtml.slice(appStart));

    await page.goto('/');
    await page.waitForSelector('#app[data-ssr="true"]');
    await page.waitForLoadState('networkidle');

    const csrClasses = await page.evaluate(() => {
      const out: string[] = [];
      const app = document.querySelector('#app');
      if (!app) return out;
      if ((app as HTMLElement).className) {
        out.push((app as HTMLElement).className.split(/\s+/).filter(Boolean).sort().join(' '));
      }
      for (const el of app.querySelectorAll('*')) {
        if (el.getAttribute('class')) {
          out.push(el.getAttribute('class')!.split(/\s+/).filter(Boolean).sort().join(' '));
        }
      }
      return out;
    });

    expect(csrClasses).toEqual(ssrClasses);
  });

  test('adopted DOM is interactive: the showroom segmented control switches', async ({ page }) => {
    // The flagship screen, not a toy counter: a SegmentedControl from the shared library, rendered
    // by the server and then adopted. Selection is asserted through `aria-pressed` — the semantic
    // the component promises assistive tech — so a control that merely repaints the active pill
    // without moving selection still fails here.
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const sevenDays = page.getByRole('button', { name: '7d', exact: true }).first();
    const ninetyDays = page.getByRole('button', { name: '90d', exact: true }).first();

    await expect(sevenDays).toHaveAttribute('aria-pressed', 'false');
    await expect(ninetyDays).toHaveAttribute('aria-pressed', 'false');

    await ninetyDays.click();

    await expect(ninetyDays).toHaveAttribute('aria-pressed', 'true');
    await expect(sevenDays).toHaveAttribute('aria-pressed', 'false');
  });

  test('the declarative (no-new) page hydrates by identity and stays alive', async ({ page }) => {
    // The factory surface has TWO independent implementations that must agree: SSR runs the real
    // C# `UI.Column(gap, [children])` through the WebRealizer, the client runs the TRANSPILED
    // `UI.column(12, [...])` twin. A divergence in either (the children landing as config instead
    // of add() calls, a default filled as null instead of the twin's own) shows up here as a class
    // mismatch or a dead button — nowhere earlier.
    const response = await page.request.get('/declarative');
    expect(response.status()).toBe(200);
    const ssrHtml = await response.text();
    const appStart = ssrHtml.indexOf('<div id="app"');
    expect(appStart).toBeGreaterThan(-1);
    // The children the collection reached SSR at all — an empty container is the old silent bug.
    expect(ssrHtml).toContain('Count: 0');
    const ssrClasses = classFingerprint(ssrHtml.slice(appStart));

    await page.goto('/declarative');
    await page.waitForLoadState('networkidle');

    const csrClasses = await page.evaluate(() => {
      const out: string[] = [];
      const app = document.querySelector('#app');
      if (!app) return out;
      if ((app as HTMLElement).className) {
        out.push((app as HTMLElement).className.split(/\s+/).filter(Boolean).sort().join(' '));
      }
      for (const el of app.querySelectorAll('*')) {
        if (el.getAttribute('class')) {
          out.push(el.getAttribute('class')!.split(/\s+/).filter(Boolean).sort().join(' '));
        }
      }
      return out;
    });
    expect(csrClasses).toEqual(ssrClasses);

    const count = page.getByText(/Count: -?\d+/).first();
    await page.getByRole('button', { name: 'Up' }).first().click();
    await expect(count).toHaveText('Count: 1');
    await page.getByRole('button', { name: 'Down' }).first().click();
    await page.getByRole('button', { name: 'Down' }).first().click();
    await expect(count).toHaveText('Count: -1');
  });

  test('Link navigation is SPA (no full reload)', async ({ page }) => {
    // The console shell's sidebar renders real <a href> Links; the showroom '/' is the one screen
    // that carries it. Every destination it advertises is a screen this sample declares — a link
    // to a route with no page is a legitimate SERVER round-trip (the 404 needs a real status), so
    // pointing this test at one would assert the opposite of what it is named for.
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // A full document reload would wipe this marker; SPA navigation keeps it.
    await page.evaluate(() => {
      (window as unknown as { __eqNavProbe: number }).__eqNavProbe = 42;
    });

    await page.getByRole('link', { name: 'Spreadsheet' }).first().click();
    await page.waitForURL('**/sheet');

    const probe = await page.evaluate(
      () => (window as unknown as { __eqNavProbe?: number }).__eqNavProbe,
    );
    expect(probe).toBe(42);
    await expect(page.getByText('Spreadsheet', { exact: false }).first()).toBeVisible();
  });

  test('a link to an undeclared route still lands on the 404 screen', async ({ page }) => {
    // The other half of the routing contract, and the reason the test above had to move: a route
    // with no page is served by the SERVER with a true 404 status, not painted client-side. This
    // pins that the fallback screen is reached and the status is honest.
    const response = await page.request.get('/no-such-screen');
    expect(response.status()).toBe(404);

    await page.goto('/no-such-screen');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('This console has no such screen.').first()).toBeVisible();
  });
});
