import { afterEach, describe, expect, it } from 'vitest';
import { Navigator } from './navigator';

/**
 * `Navigator.of(context).location` is the path the app writes (C# `Navigator.Location`): a layout
 * compares it with an href it wrote, and an in-app href never carries the language segment, which a
 * link gains as it is drawn (`UseCultureRoutes`). The server answers the same path for the same page
 * (`AppLayoutTests.UnderALanguageSegment_TheLocationIsThePathTheAppWrites`).
 */
describe("a navigator's location is the path the app writes", () => {
  const globals = globalThis as { __EQ_CONFIG?: unknown };

  afterEach(() => {
    delete globals.__EQ_CONFIG;
    window.history.replaceState(null, '', '/');
  });

  it('drops the language segment the app declared', () => {
    globals.__EQ_CONFIG = { cultureRoutes: { default: 'en', prefixed: ['pt-BR'] } };
    window.history.replaceState(null, '', '/pt-BR/charts?tab=2');

    expect(Navigator.of(null).location).toBe('/charts');
  });

  it('keeps a path in the default language as it is', () => {
    globals.__EQ_CONFIG = { cultureRoutes: { default: 'en', prefixed: ['pt-BR'] } };
    window.history.replaceState(null, '', '/charts');

    expect(Navigator.of(null).location).toBe('/charts');
  });

  it('keeps a first segment the app never declared as a language', () => {
    window.history.replaceState(null, '', '/pt-BR/charts');

    expect(Navigator.of(null).location).toBe('/pt-BR/charts');
  });
});
