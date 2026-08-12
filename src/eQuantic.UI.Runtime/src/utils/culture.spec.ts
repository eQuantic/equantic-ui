import { beforeEach, describe, expect, it, vi } from 'vitest';
import { activeCulture, installCulture, str } from './culture';
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
