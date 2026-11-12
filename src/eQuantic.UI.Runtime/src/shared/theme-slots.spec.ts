import { describe, it, expect } from 'vitest';
import { photonTheme } from './design-system.generated';
import { AppThemeExtensions, ButtonTheme, ControlsTheme, DensityLadder, SizeLadder, mergeSlot } from './theme-slots';

// The twin of the C# slot records' Merge and of AppThemeExtensions.With: a slot given to With is laid over
// the theme's own, member by member, so overriding one value keeps every other the design system set.
describe('theme slots: With lays a slot over the theme, member by member', () => {
  it('keeps every member the given slot leaves null', () => {
    const theme = AppThemeExtensions.with(photonTheme, null, new ButtonTheme({ minWidth: 88 }));

    expect(theme.button?.minWidth).toBe(88);
    expect(theme.button?.labelWeight).toBe(photonTheme.button?.labelWeight);
    expect(theme.button?.outlineColor).toEqual(photonTheme.button?.outlineColor);
    expect(theme.controls).toBe(photonTheme.controls);
  });

  it('merges a nested ladder rung by rung', () => {
    const taller = AppThemeExtensions.with(
      photonTheme,
      new ControlsTheme({ height: new DensityLadder({ comfortable: new SizeLadder({ medium: 44 }) }) }),
    );

    expect(taller.controls?.height?.comfortable?.medium).toBe(44);
    expect(taller.controls?.height?.comfortable?.small).toBe(photonTheme.controls?.height?.comfortable?.small);
    expect(taller.controls?.height?.compact).toEqual(photonTheme.controls?.height?.compact);
    expect(taller.controls?.radius).toEqual(photonTheme.controls?.radius);
  });

  it('answers the same merged slot on every read, and forwards everything else', () => {
    const theme = AppThemeExtensions.with(photonTheme, null, new ButtonTheme({ minWidth: 88 }));

    expect(theme.button).toBe(theme.button);
    expect(theme.colors('primary')).toEqual(photonTheme.colors('primary'));
  });

  it('is null only when both sides are, and either side alone is itself', () => {
    const ladder = new SizeLadder({ small: 1 });
    expect(mergeSlot<SizeLadder>(null, null)).toBeNull();
    expect(mergeSlot(ladder, null)).toBe(ladder);
    expect(mergeSlot(null, ladder)).toBe(ladder);
    expect(SizeLadder.merge(new SizeLadder({ small: 1, large: 3 }), new SizeLadder({ large: 9 }))).toEqual(
      new SizeLadder({ small: 1, large: 9 }),
    );
  });
});
