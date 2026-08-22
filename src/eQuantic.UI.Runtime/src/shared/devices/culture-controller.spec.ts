import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { setNavigationHandler } from '../../router/navigator';
import { setCultureCatalogLoader } from '../../utils/culture';
import { WebCultureController } from './culture-controller';

/**
 * Switching language when the language lives in the URL.
 *
 * Two things have to happen and their ORDER is the whole test: the catalog for the chosen
 * language has to be in memory, and the address has to change. Navigating first moved the reader
 * to /pt-BR and drew that page in English — a URL saying one language while every word said the
 * other, corrected only by the full reload the no-reload switch exists to avoid.
 */
describe('WebCultureController.apply with culture routes', () => {
  const loaded: string[] = [];
  const navigated: string[] = [];
  /** What the loader had already answered by the time the navigation was asked for. */
  let catalogAtNavigation: string[] = [];

  beforeEach(() => {
    loaded.length = 0;
    navigated.length = 0;
    catalogAtNavigation = [];
    (globalThis as Record<string, unknown>).__EQ_CONFIG = {
      cultureRoutes: { default: 'en', prefixed: ['pt-BR', 'es'] },
    };
    setCultureCatalogLoader(async (culture) => {
      loaded.push(culture);
      return { Greeting: `hello-${culture}` };
    });
    setNavigationHandler((href) => {
      navigated.push(href);
      catalogAtNavigation = [...loaded];
    });
    window.history.replaceState(null, '', '/');
  });

  afterEach(() => {
    setNavigationHandler(null);
    delete (globalThis as Record<string, unknown>).__EQ_CONFIG;
  });

  it('loads the catalog BEFORE it changes the address', async () => {
    new WebCultureController().apply('pt-BR', 'pt-BR');
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(navigated).toEqual(['/pt-BR']);
    expect(catalogAtNavigation).toContain('pt-BR');
  });

  it('carries the rest of the path, the query and the hash across', async () => {
    window.history.replaceState(null, '', '/products?tier=pro#pricing');
    new WebCultureController().apply('es', 'es');
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(navigated).toEqual(['/es/products?tier=pro#pricing']);
  });

  it('returns to the unprefixed root for the default language', async () => {
    window.history.replaceState(null, '', '/pt-BR/products');
    new WebCultureController().apply('en', 'en');
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(navigated).toEqual(['/products']);
  });

  it('navigates even when the catalog cannot be fetched — the language still switches', async () => {
    setCultureCatalogLoader(async () => {
      throw new Error('offline');
    });
    new WebCultureController().apply('pt-BR', 'pt-BR');
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(navigated).toEqual(['/pt-BR']);
  });

  it('stays put when the language asked for is the one already in the URL', async () => {
    window.history.replaceState(null, '', '/es/products');
    new WebCultureController().apply('es', 'es');
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(navigated).toEqual([]);
  });
});
