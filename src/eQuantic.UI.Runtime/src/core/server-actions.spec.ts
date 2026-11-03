import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { installCulture } from '../utils/culture';
import { ServerActionsClient } from './server-actions';

/**
 * The culture a page was written in travels with its actions (#580), in the query keys ASP.NET's own
 * QueryStringRequestCultureProvider reads ahead of the cookie. The endpoint has no culture segment,
 * so the route that rendered `/pt-BR/...` answered nothing there, and a handler answered a Portuguese
 * page in the language of the cookie or of the browser.
 */
describe('a server action carries the culture of its page', () => {
  const addresses: string[] = [];

  beforeEach(() => {
    addresses.length = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        addresses.push(url);
        return new Response(JSON.stringify({ success: true, result: 'done' }), { status: 200 });
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    installCulture('', '', {});
  });

  it('names the page culture, the format and the language each', async () => {
    installCulture('pt-BR', 'en-GB', {});

    await new ServerActionsClient().invoke('Counter/Increment');

    expect(addresses).toEqual(['/api/_equantic/actions?culture=en-GB&ui-culture=pt-BR']);
  });

  it('follows a switch with no reload', async () => {
    const client = new ServerActionsClient();
    installCulture('pt-BR', 'pt-BR', {});
    await client.invoke('Counter/Increment');
    installCulture('es', 'es', {});
    await client.invoke('Counter/Increment');

    expect(addresses[1]).toBe('/api/_equantic/actions?culture=es&ui-culture=es');
  });

  it('names nothing on a page with no culture installed, which keeps the cookie and the browser', async () => {
    installCulture('', '', {});

    await new ServerActionsClient().invoke('Counter/Increment');

    expect(addresses).toEqual(['/api/_equantic/actions']);
  });

  it('joins a base address that has a query of its own', async () => {
    installCulture('fr', 'fr', {});

    await new ServerActionsClient('/custom/actions?tenant=a').invoke('Counter/Increment');

    expect(addresses).toEqual(['/custom/actions?tenant=a&culture=fr&ui-culture=fr']);
  });
});
