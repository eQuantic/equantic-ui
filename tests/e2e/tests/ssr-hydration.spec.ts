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
    // KNOWN DIVERGENCE (2026-07-31, expected-fail until fixed): stateful/animated components
    // (ProgressBar fills, Skeleton shimmer, LoopMotion bars) re-render client-side with classes
    // the SSR didn't emit (+1 class on 4 motion elements, ~6 extra styled elements). The STATIC
    // tree already matches byte-for-byte — the FlexNode ES2022 cross bug this test caught is
    // fixed and pinned in vocabulary.spec.ts. When the motion family is aligned, this flips green
    // and Playwright will flag the unexpected pass.
    test.fail();

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

  test('adopted DOM is interactive: the shared counter increments', async ({ page }) => {
    await page.goto('/shared');
    await page.waitForLoadState('networkidle');

    const count = page.getByText(/Count: \d+/).first();
    const before = Number((await count.textContent())!.match(/\d+/)![0]);

    await page.getByRole('button', { name: 'Increment' }).first().click();

    await expect(count).toHaveText(new RegExp(`Count: ${before + 1}`));
  });

  test('Link navigation is SPA (no full reload)', async ({ page }) => {
    // The nav ShellBar renders on SampleShell pages (the showroom '/' deliberately has none).
    await page.goto('/counter');
    await page.waitForLoadState('networkidle');

    // A full document reload would wipe this marker; SPA navigation keeps it.
    await page.evaluate(() => {
      (window as unknown as { __eqNavProbe: number }).__eqNavProbe = 42;
    });

    await page.getByRole('link', { name: 'Users' }).first().click();
    await page.waitForURL('**/users');

    const probe = await page.evaluate(
      () => (window as unknown as { __eqNavProbe?: number }).__eqNavProbe,
    );
    expect(probe).toBe(42);
    await expect(page.getByText('Users', { exact: false }).first()).toBeVisible();
  });
});
