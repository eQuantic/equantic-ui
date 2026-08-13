import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  activeCulture,
  installCulture,
  setCulture,
  setCultureCatalogLoader,
  setCultureInvalidator,
  str,
} from './culture';
import { $eq } from '../eq';

/** Track L W3: the culture atom and the rewritten-accessor lookup (C# twin: the SSR resolves the
 * same key through the real ResourceManager under the request culture — D4's identity bet). */
describe('culture atom', () => {
  beforeEach(() => {
    installCulture('', '', {});
  });

  it('carries the PAIR — resources and formats are independent, exactly as .NET models them', () => {
    installCulture('pt-BR', 'en-US', {});
    expect(activeCulture()).toEqual({ ui: 'pt-BR', format: 'en-US' });
  });

  it('resolves a rewritten accessor against the installed catalog', () => {
    installCulture('pt-BR', 'pt-BR', { 'Strings/Hero.Title': 'Construa produtos' });
    expect(str('Strings', 'Hero.Title')).toBe('Construa produtos');
    expect($eq.str('Strings', 'Hero.Title')).toBe('Construa produtos');
  });

  it('a missing key renders the KEY and warns once — ugly, never blank, never thrown', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    try {
      installCulture('pt-BR', 'pt-BR', {});
      expect(str('Strings', 'Hero.Title')).toBe('Hero.Title');
      expect(str('Strings', 'Hero.Title')).toBe('Hero.Title');
      expect(warn).toHaveBeenCalledTimes(1);
    } finally {
      warn.mockRestore();
    }
  });

  it("the SDK's own strings fall back to built-in English rather than to raw keys", () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    try {
      // No catalog at all — a page mounted without the server bridge (a test host, the
      // playground). The chrome must still read as words, and quietly: this fallback is the
      // designed path, not a missing translation.
      expect(str('SdkResources', 'SearchPlaceholder')).toBe('Search…');
      expect(str('SdkResources', 'Find')).toBe('Find');
      expect(warn).not.toHaveBeenCalled();
    } finally {
      warn.mockRestore();
    }
  });

  it("an app's translation of an SDK string beats the built-in neutral", () => {
    installCulture('pt-BR', 'pt-BR', { 'SdkResources/Find': 'Localizar' });
    expect(str('SdkResources', 'Find')).toBe('Localizar');
  });

  it('an SDK key that exists in NO resx still degrades to the key', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    try {
      expect(str('SdkResources', 'NotAKey')).toBe('NotAKey');
      expect(warn).toHaveBeenCalledTimes(1);
    } finally {
      warn.mockRestore();
    }
  });

  it('setCulture fetches the catalog, swaps it and invalidates the tree — no reload (D6)', async () => {
    const asked: string[] = [];
    setCultureCatalogLoader(async (culture) => {
      asked.push(culture);
      return culture === 'es' ? { 'Strings/Hero.Title': 'Construye productos' } : null;
    });
    let renders = 0;
    setCultureInvalidator(() => renders++);

    installCulture('en', 'en', { 'Strings/Hero.Title': 'Build products' });
    await setCulture('es');

    expect(asked).toEqual(['es']);
    expect(activeCulture()).toEqual({ ui: 'es', format: 'es' });
    expect(str('Strings', 'Hero.Title')).toBe('Construye productos');
    expect(renders).toBe(1);
  });

  it('a culture already in memory switches back without asking for it again', async () => {
    const asked: string[] = [];
    setCultureCatalogLoader(async (culture) => {
      asked.push(culture);
      return { 'Strings/Hero.Title': `title-${culture}` };
    });
    setCultureInvalidator(() => {});

    // Cultures unique to this test: the catalog cache is module state BY DESIGN (a switch back is
    // meant to be instant), so a shared name would make these specs order-dependent.
    installCulture('de-AT', 'de-AT', { 'Strings/Hero.Title': 'Build products' });
    await setCulture('nl');
    await setCulture('de-AT');
    await setCulture('nl');

    expect(asked).toEqual(['nl']);
    expect(str('Strings', 'Hero.Title')).toBe('title-nl');
  });

  it('the catalog PICK walks parents then neutral — the server file walk, mirrored', async () => {
    const asked: string[] = [];
    setCultureCatalogLoader(async (culture) => {
      asked.push(culture);
      return culture === 'neutral' ? { 'Strings/Hero.Title': 'Build products' } : null;
    });
    setCultureInvalidator(() => {});

    await setCulture('fr-CA');

    expect(asked).toEqual(['fr-CA', 'fr', 'neutral']);
    // The NAME still switched even though only the neutral catalog answered: formats follow
    // immediately, and the strings degrade to neutral rather than to keys.
    expect(activeCulture().ui).toBe('fr-CA');
    expect(str('Strings', 'Hero.Title')).toBe('Build products');
  });

  it('switching to the culture already in force does nothing at all', async () => {
    let renders = 0;
    setCultureInvalidator(() => renders++);
    setCultureCatalogLoader(async () => {
      throw new Error('must not fetch');
    });

    installCulture('pt-BR', 'pt-BR', {});
    await setCulture('pt-BR');

    expect(renders).toBe(0);
  });

  it('the PAIR can differ — pt-BR strings over en-US formats (D13)', async () => {
    setCultureCatalogLoader(async () => ({}));
    setCultureInvalidator(() => {});

    await setCulture('pt-BR', 'en-US');

    expect(activeCulture()).toEqual({ ui: 'pt-BR', format: 'en-US' });
  });

  it('installing a new culture swaps the catalog and re-arms the warnings', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    try {
      installCulture('en', 'en', {});
      str('Strings', 'Greeting');
      installCulture('pt-BR', 'pt-BR', { 'Strings/Greeting': 'Olá, {0}!' });
      expect(str('Strings', 'Greeting')).toBe('Olá, {0}!');
      expect(warn).toHaveBeenCalledTimes(1);
    } finally {
      warn.mockRestore();
    }
  });
});
