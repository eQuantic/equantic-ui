import { test, expect, type Page } from '@playwright/test';

/**
 * The dashboard declares its frame once, as the app's layout (`UseLayout<ConsoleLayout>()` in its
 * `Program.cs`, #1129), and every screen returns its content only.
 *
 * What only a browser can prove is that the layout STAYS MOUNTED across a client navigation: the
 * runtime builds it once and hands it each page in turn, so what the layout holds outlives the page
 * under it. The compact drawer is that state. It is a field of the layout, opened by the layout's own
 * trigger, so it is still open on the next screen only when the layout's instance survived the
 * navigation: a layout built again for the new page starts with it closed, and so did every screen
 * while each one drew the frame itself.
 *
 * The server renders the same tree, the page inside the layout, which the second test reads in the
 * markup it sends. The tree's shape (the page placed once, outside the frame's adaptive arms) is
 * pinned on the node by `ConsoleLayoutTests`.
 */

/** A phone's width: the docked sidebar is the expanded arm's, and below it the nav rides a drawer. */
const compact = { width: 390, height: 844 };

/**
 * Lets the page settle the density it runs at before the drawer is opened. A first visit hydrates at
 * the comfortable density and then switches the whole page to the one its pointer asks for, keeping it
 * in the `eq-density` cookie the server renders at from the next request on (#623, #688). That switch
 * is a re-render after hydration, so it is done with before anything is pressed.
 */
async function settleDensity(page: Page): Promise<void> {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await expect
    .poll(async () => (await page.context().cookies()).some((cookie) => cookie.name === 'eq-density'))
    .toBe(true);
}

test.describe('An app layout', () => {
  test('the drawer opened on one screen is still open on the next, lighting the new destination', async ({ page }) => {
    await page.setViewportSize(compact);
    await settleDensity(page);
    await page.goto('/charts');
    await page.waitForLoadState('networkidle');

    // A closed drawer is an empty box: no dialog until the layout's trigger opens it.
    const drawer = page.getByRole('dialog');
    await expect(drawer).toHaveCount(0);
    await page.getByRole('button', { name: 'Navigation' }).filter({ visible: true }).click();
    await expect(drawer).toBeVisible();
    await expect(drawer.getByRole('link', { name: 'Charts' })).toHaveAttribute('aria-current', 'page');

    // A full document load would wipe this marker, and would close the drawer for a reason this
    // test is not about: it says which of the two happened.
    await page.evaluate(() => {
      (window as unknown as { __eqLayoutProbe: number }).__eqLayoutProbe = 1129;
    });

    await drawer.getByRole('link', { name: 'Table' }).click();
    await page.waitForURL('**/table');

    // The new page's content is shown, under the drawer the previous page's reader opened.
    await expect(page.getByRole('table', { name: "Excel's grid" })).toBeVisible();
    await expect(drawer, 'the layout kept its instance, and with it the open drawer').toBeVisible();
    const probe = await page.evaluate(
      () => (window as unknown as { __eqLayoutProbe?: number }).__eqLayoutProbe,
    );
    expect(probe, 'the navigation was the router\'s, not a document load').toBe(1129);

    // The layout built again for the new location: it lights the destination the reader is on now.
    await expect(drawer.getByRole('link', { name: 'Table' })).toHaveAttribute('aria-current', 'page');
    await expect(drawer.getByRole('link', { name: 'Charts' })).not.toHaveAttribute('aria-current', 'page');

    // And the drawer is still the layout's to close.
    await page.keyboard.press('Escape');
    await expect(drawer).toHaveCount(0);
    await expect(page.getByRole('table', { name: "Excel's grid" })).toBeVisible();
  });

  test('the server renders the page inside the layout\'s frame', async ({ page }) => {
    const response = await page.request.get('/charts');
    expect(response.status()).toBe(200);
    const html = await response.text();

    // The client configuration names the layout the browser builds around the page.
    expect(html).toContain('"layout":"ConsoleLayout"');
    // The page is in the document once: placed inside the frame's adaptive arms, it was once per arm.
    expect(html.split('Revenue by quarter').length - 1, 'the page\'s heading, once').toBe(1);

    // The markup as the server sent it, parsed and never run: no hydration has touched it.
    const frame = await page.evaluate((markup) => {
      const doc = new DOMParser().parseFromString(markup, 'text/html');
      const app = doc.querySelector('#app[data-ssr="true"]');
      if (!app) return null;
      const heading = [...app.querySelectorAll('*')].find(
        (element) => element.children.length === 0 && element.textContent?.trim() === 'Revenue by quarter',
      );
      const rail = app.querySelector('a[href="/table"]');
      const crumb = app.querySelector('a[aria-label="Workspace"]');
      const before = (element: Element | null) =>
        !!element && !!heading && !element.contains(heading)
        && (element.compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
      return {
        heading: !!heading,
        // The layout's root holds the frame and the page alike.
        oneRoot: !!heading && !!rail && app.children.length === 1
          && app.firstElementChild!.contains(heading) && app.firstElementChild!.contains(rail),
        railBeforePage: before(rail),
        breadcrumbBeforePage: before(crumb),
        current: [...app.querySelectorAll('a[aria-current="page"]')].map((link) => link.getAttribute('href')),
      };
    }, html);

    expect(frame, 'the served document has the SSR root').not.toBeNull();
    expect(frame!.heading, 'the page\'s content is in the SSR root').toBe(true);
    expect(frame!.oneRoot, 'the layout is the root, and the page is inside it').toBe(true);
    expect(frame!.railBeforePage, 'the sidebar comes before the page, beside it').toBe(true);
    expect(frame!.breadcrumbBeforePage, 'the toolbar comes before the page, above it').toBe(true);
    expect(frame!.current, 'the server lit the destination of the page it rendered').toEqual(['/charts']);
  });

  test('a page the layout frames is never shown without it, when the layout\'s module does not arrive', async ({ page }) => {
    // The layout's chunk is lost to a CDN hiccup or a deploy race: the browser's import of it fails as
    // it does for any chunk the server answers 404 for.
    await page.route('**/_equantic/ConsoleLayout.js*', (route) => route.fulfill({ status: 404, body: '' }));
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/charts');
    await page.waitForLoadState('networkidle');

    // A first load keeps what the server rendered, the page inside its frame: it loses only interactivity.
    await expect(page.getByText('Revenue by quarter')).toBeVisible();

    // A navigation shows the next page inside the layout, or says it cannot: never the page alone, a
    // document the server never drew.
    await page.locator('a[href="/table"]').filter({ visible: true }).first().click();
    await page.waitForURL('**/table');
    await expect(page.locator('.eq-status-page code')).toHaveText('ConsoleLayout');
    await expect(page.getByRole('table', { name: "Excel's grid" })).toHaveCount(0);
  });

  test('what the payments screen owns still answers inside the layout', async ({ page }) => {
    // Its search, palette, sandbox switch and "New payment" sat in the frame while every screen drew
    // its own; they change the screen's state, so the screen draws them now, and the layout's
    // toolbar keeps only what is the frame's.
    await page.setViewportSize({ width: 1440, height: 900 });
    await settleDensity(page);

    // ⌘K (Ctrl+K elsewhere) opens the palette from anywhere on the screen, as its entry says.
    await page.keyboard.press('ControlOrMeta+k');
    const palette = page.getByRole('dialog', { name: 'Search the workspace' });
    await expect(palette).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(palette).toHaveCount(0);

    // The palette's entry point says the same.
    await page.getByRole('button', { name: 'Search the workspace, command K' }).filter({ visible: true }).click();
    await expect(palette).toBeVisible();
    await page.keyboard.press('Escape');

    const sandbox = page.getByRole('switch', { name: 'Sandbox mode' }).filter({ visible: true });
    await expect(sandbox).toHaveAttribute('aria-checked', 'true');
    await sandbox.click();
    await expect(sandbox).toHaveAttribute('aria-checked', 'false');

    await page.getByRole('button', { name: 'New payment' }).filter({ visible: true }).click();
    await expect(page.getByRole('dialog', { name: 'New payment' })).toBeVisible();
  });
});
