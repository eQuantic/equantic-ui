/**
 * A transpiled object initializer reaches a hand-written twin as its trailing config, typed by the
 * config the twin's constructor takes, so a member that config lacks is a TS2353 the moment a
 * component sets it (#1823). `EmittedConfigs` is what eqc writes for three initializers that each set
 * a member a config lacked until then (SharedComponentTranspilationTests transpiles it): `tsc` checks
 * that module with the runtime's source, and this spec runs it and reads each value back from the twin.
 * `VocabularyConfigShapeTests` pins every config to its C# class; this is the real checker, and the
 * real constructor, taking what that test says they must.
 */
import { describe, expect, it } from 'vitest';
import { EmittedConfigs } from './__fixtures__/EmittedConfigs';
import { Text } from './vocabulary';

describe('a config takes what its C# class lets an initializer set', () => {
  it('a box style holds the foreground it hands its descendants', () => {
    const style = EmittedConfigs.inked();

    expect(style.foreground).toMatchObject({ light: { r: 0, g: 0, b: 0, a: 255 } });
  });

  it('a node whose config is written in place holds every member a node has', () => {
    const group = EmittedConfigs.grouped(new Text('Totals'));

    expect([group.bookmark, group.alignSelf, group.gridSpan]).toEqual(['totals', 'center', 2]);
  });

  it('a spacer holds the fixed length its initializer sets', () => {
    expect(EmittedConfigs.rigid().fixedLength).toBe(8);
  });
});
