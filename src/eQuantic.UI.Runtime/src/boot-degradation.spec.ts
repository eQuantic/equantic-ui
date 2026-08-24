import { beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * A FRESH module every time: boot() latches `initialized` and no-ops on a second call, so a shared
 * import would leave every test after the first asserting against a boot that never ran — and the
 * first one they'd see pass is the one that proves nothing.
 */
async function freshBoot(): Promise<void> {
  vi.resetModules();
  const { boot } = await import('../../eQuantic.UI.Sdk/Resources/boot');
  await boot();
}

/**
 * What the server rendered STAYS. A page bundle that does not arrive costs interactivity, never
 * content — and a chunk lost to a CDN hiccup or a deploy race is exactly when it does not arrive.
 * Replacing a page the reader is looking at with "404 — this page could not be found" is the
 * client making the page worse than it found it.
 */
describe('a page that fails to load does not erase what the server rendered', () => {
  const SSR = '<h1>Pricing</h1><p>Ten euros a month.</p>';

  beforeEach(() => {
    document.body.innerHTML = `<div id="app">${SSR}</div>`;
    (window as unknown as Record<string, unknown>).__EQ_CONFIG = { page: 'NoSuchPage' };
    (window as unknown as Record<string, unknown>).__EQ_DEV__ = undefined;
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(console, 'log').mockImplementation(() => {});
  });

  // WHICH failure this exercises, because the two doors are not the same one. Node's dynamic
  // import fails with a message shaped unlike a browser's, so loadPageModule rethrows rather than
  // reporting "no such page", and it is boot's outer catch that degrades here. The genuine-404
  // door — a chunk the server answers 404 for — only exists in a browser, and is verified there.
  it('keeps the server-rendered markup', async () => {
    await freshBoot();

    const app = document.getElementById('app')!;
    expect(app.innerHTML).toContain('Ten euros a month.');
    expect(app.innerHTML).not.toContain('eq-status-page');
  });

  it('says so in the console, because a dead page must not be silent', async () => {
    await freshBoot();

    expect(console.error).toHaveBeenCalled();
  });

  it('still shows the loud page in dev, where it is the feature', async () => {
    (window as unknown as Record<string, unknown>).__EQ_DEV__ = true;

    await freshBoot();

    // A developer whose page renders but never responds would hunt for hours. Degrading is for
    // the reader in production, never for the person who can still fix it.
    expect(document.getElementById('app')!.innerHTML).toContain('eq-status-page');
  });
});
