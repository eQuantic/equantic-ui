/**
 * A transpiled object initializer reaches a hand-written twin as its trailing config, typed by the
 * config the twin's constructor takes, so a member that config lacks is a TS2353 the moment a
 * component sets it (#1823). Each construction below is the shape eqc writes for a member a config
 * lacked until then: `tsc` checks this file with the runtime's source, and the assertions read the
 * value back from the twin. `VocabularyConfigShapeTests` pins every config to its C# class; this is
 * the real checker taking what that test says it must.
 */
import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { BoxStyle, GlobalKey, Group, Spacer, Text } from './vocabulary';

describe('a config takes what its C# class lets an initializer set', () => {
  it('a box style takes the foreground it hands its descendants', () => {
    const style = new BoxStyle({ background: photonTheme.surface, foreground: photonTheme.textPrimary });

    expect(style.foreground).toBe(photonTheme.textPrimary);
  });

  it('a node whose config is written in place takes every member a node has', () => {
    const key = new GlobalKey('summary');
    const group = new Group(new Text('Totals'), {
      label: 'Totals',
      key: 'totals',
      globalKey: key,
      bookmark: 'totals',
      alignSelf: 'center',
      gridSpan: 2,
    });

    expect([group.key, group.globalKey, group.bookmark, group.alignSelf, group.gridSpan]).toEqual([
      'totals',
      key,
      'totals',
      'center',
      2,
    ]);
  });

  it('a spacer takes the fixed length its C# initializer sets', () => {
    const spacer = new Spacer(1, { fixedLength: 8 });

    expect(spacer.fixedLength).toBe(8);
  });
});
