import { beforeEach, describe, expect, it } from 'vitest';
import { WebThemeController } from './theme-controller';

/**
 * The web half of C# IThemeController: forcing a mode is ONE color-scheme declaration on the
 * root, because the whole palette is authored as light-dark() pairs — the browser owns the swap.
 */
describe('WebThemeController', () => {
  beforeEach(() => {
    document.documentElement.style.colorScheme = '';
  });

  it('apply forces the color-scheme, and mode reads it back', () => {
    const controller = new WebThemeController();
    controller.apply('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
    expect(controller.mode).toBe('dark');

    controller.apply('light');
    expect(document.documentElement.style.colorScheme).toBe('light');
    expect(controller.mode).toBe('light');
  });

  it('with nothing forced, mode falls back to what the OS prefers', () => {
    const controller = new WebThemeController();
    // jsdom's matchMedia (when present) never matches; absent or non-matching both mean light.
    expect(controller.mode).toBe('light');
  });
});
