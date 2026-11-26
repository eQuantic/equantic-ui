import { afterEach, describe, expect, it, vi } from 'vitest';
import { Navigator } from './navigator';

/**
 * `Navigator.of(context).openAsync` and `downloadAsync` (C# `Navigator.OpenAsync`, `DownloadAsync`,
 * #1787): a handler that awaited an action opens the address it answered with in a new tab, or
 * downloads the file there, without a second press on a link. A page had to show the person a link to
 * press after the action resolved.
 */
describe('a navigator opens and downloads an address beside the app', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('opens an address in a new tab, resolved against the page, cut from the page', async () => {
    const tab = { opener: window } as unknown as Window;
    const open = vi.spyOn(window, 'open').mockReturnValue(tab);

    const answer = await Navigator.of(null).openAsync('/print/ticket-1?lang=pt');

    expect(answer).toBe(true);
    expect(open).toHaveBeenCalledWith(new URL('/print/ticket-1?lang=pt', document.baseURI).href, '_blank');
    expect(tab.opener).toBeNull();
  });

  it('answers false when the browser blocked the tab', async () => {
    vi.spyOn(window, 'open').mockReturnValue(null);

    expect(await Navigator.of(null).openAsync('https://example.com/a.pdf')).toBe(false);
  });

  it.each(['javascript:alert(1)', 'data:text/html,<p>x</p>', 'file:///etc/passwd'])(
    'never opens %s, an address that is not http or https',
    async (address) => {
      const open = vi.spyOn(window, 'open');
      vi.spyOn(console, 'warn').mockImplementation(() => {});

      expect(await Navigator.of(null).openAsync(address)).toBe(false);
      expect(open).not.toHaveBeenCalled();
    },
  );

  it('refuses an empty address, as .NET does', async () => {
    await expect(Navigator.of(null).openAsync('')).rejects.toThrow("Parameter 'address'");
  });

  it("downloads a file of the page's own origin under its name, from an anchor it removes", async () => {
    const clicked: { href: string; download: string }[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push({ href: this.href, download: this.download });
    });
    const open = vi.spyOn(window, 'open');

    const answer = await Navigator.of(null).downloadAsync('/print/ticket-1.pdf', 'Catalogue.pdf');

    expect(answer).toBe(true);
    expect(clicked).toEqual([{ href: new URL('/print/ticket-1.pdf', document.baseURI).href, download: 'Catalogue.pdf' }]);
    expect(document.querySelector('a[download]')).toBeNull();
    expect(open).not.toHaveBeenCalled();
  });

  it("opens another origin's file in a tab, since a browser would navigate the page to it", async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click');
    const open = vi.spyOn(window, 'open').mockReturnValue({ opener: window } as unknown as Window);

    expect(await Navigator.of(null).downloadAsync('https://files.example.com/c.pdf', 'Catalogue.pdf')).toBe(true);
    expect(open).toHaveBeenCalledWith('https://files.example.com/c.pdf', '_blank');
    expect(click).not.toHaveBeenCalled();
  });

  it('refuses a download with no name', async () => {
    await expect(Navigator.of(null).downloadAsync('/a.pdf', '')).rejects.toThrow("Parameter 'fileName'");
  });
});
