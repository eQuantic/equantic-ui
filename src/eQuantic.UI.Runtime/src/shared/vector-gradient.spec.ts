import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { gradientId } from './lowering';
import type { VectorGradientValue } from './nodes';

/**
 * The gradient id, cross-pinned to the C# realizer (VectorGradientTests writes the cases). A shape
 * that names `url(#id)` is rendered twice — once by the server and once here on hydration — and a
 * byte of disagreement repaints every gradient in the page without saying so.
 */
describe('vector gradient ids (C# cross-pin)', () => {
  const fixture = readFileSync(join(__dirname, '__fixtures__', 'vector-gradient.txt'), 'utf8');

  const parse = (line: string): { run: VectorGradientValue; id: string } => {
    const [space, x1, y1, x2, y2, radius, stops, id] = line.split('|');
    return {
      id,
      run: {
        userSpace: space === 'u',
        x1: Number(x1),
        y1: Number(y1),
        x2: Number(x2),
        y2: Number(y2),
        radius: Number(radius),
        stops: stops.split(',').map((stop) => {
          const [offset, rgba] = stop.split(':');
          return {
            offset: Number(offset),
            color: {
              r: parseInt(rgba.slice(0, 2), 16),
              g: parseInt(rgba.slice(2, 4), 16),
              b: parseInt(rgba.slice(4, 6), 16),
              a: parseInt(rgba.slice(6, 8), 16),
            },
          };
        }),
      },
    };
  };

  const cases = fixture
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .map(parse);

  it('has cases to check', () => {
    // A fixture that stopped being written would make every assertion below vacuous.
    expect(cases.length).toBeGreaterThan(0);
  });

  for (const { run, id } of cases) {
    it(`agrees with C# on ${id}`, () => {
      expect(gradientId(run)).toBe(id);
    });
  }
});
