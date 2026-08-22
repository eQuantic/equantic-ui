import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { installCulture } from './culture';
import { localizeDestination, pathForCulture, splitCulturePath } from './culture-routes';

/**
 * The browser twin of C# `CultureRouteMap`: the app's declared prefixes, never a guess by shape,
 * applied to every in-app destination and to nothing that is somebody else's.
 */
describe('culture routes', () => {
  const g = globalThis as { __EQ_CONFIG?: { cultureRoutes?: unknown } };

  beforeEach(() => {
    g.__EQ_CONFIG = { cultureRoutes: { default: 'en', prefixed: ['pt-BR', 'es'] } };
    installCulture('pt-BR', 'pt-BR', {});
  });

  afterEach(() => {
    delete g.__EQ_CONFIG;
    installCulture('', '', {});
  });

  it('splits a path into the language it names and the rest, case-insensitively', () => {
    expect(splitCulturePath('/pt-BR/pricing')).toEqual(['pt-BR', '/pricing']);
    expect(splitCulturePath('/PT-br/pricing')).toEqual(['pt-BR', '/pricing']);
    expect(splitCulturePath('/pricing')).toEqual(['en', '/pricing']);
    expect(splitCulturePath('/es')).toEqual(['es', '/']);
  });

  it('replaces a prefix and never stacks one', () => {
    expect(pathForCulture('pt-BR', '/pricing')).toBe('/pt-BR/pricing');
    expect(pathForCulture('pt-BR', '/pt-BR/pricing')).toBe('/pt-BR/pricing');
    expect(pathForCulture('es', '/pt-BR/pricing')).toBe('/es/pricing');
    expect(pathForCulture('en', '/pt-BR/pricing')).toBe('/pricing');
    expect(pathForCulture('pt-BR', '/')).toBe('/pt-BR');
  });

  it('localizes only rooted in-app destinations', () => {
    expect(localizeDestination('/about')).toBe('/pt-BR/about');
    expect(localizeDestination('/')).toBe('/pt-BR');
    expect(localizeDestination('https://example.com/about')).toBe('https://example.com/about');
    expect(localizeDestination('//cdn.example.com/x')).toBe('//cdn.example.com/x');
    expect(localizeDestination('#top')).toBe('#top');
    expect(localizeDestination('mailto:hi@example.com')).toBe('mailto:hi@example.com');
  });

  it('a page named like a language is a page unless the app declared the language', () => {
    g.__EQ_CONFIG = { cultureRoutes: { default: 'en', prefixed: ['pt-BR'] } };
    expect(splitCulturePath('/es/pricing')).toEqual(['en', '/es/pricing']);
  });

  it('does nothing when the app declared no prefixes', () => {
    delete g.__EQ_CONFIG;
    expect(localizeDestination('/about')).toBe('/about');
    expect(pathForCulture('pt-BR', '/about')).toBe('/about');
  });
});
