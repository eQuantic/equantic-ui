/**
 * The twin of the C# `MaskVocabularyTests` for what the browser holds of a mask (#1938): the refusals of each
 * kind, naming their parameter, and the stops as CSS resolves them. The C# `Mask.Resolved` is the list the web
 * writes, so the twin's `resolved` is the same list for the same stops: a stop below an earlier one moves up to
 * it, and nothing is sorted. The geometry is the browser's own (the twin writes CSS), so none of it is here.
 */

import { describe, expect, it } from 'vitest';
import { LinearMask, Masked, MaskStop, RadialExtent, RadialMask, Spacer } from './vocabulary';
import { Point } from './value-types';

const ramp = [new MaskStop(0, 1), new MaskStop(1, 0)];

function refusal(act: () => unknown): string {
  try {
    act();
  } catch (error) {
    return String((error as Error).message);
  }
  return '';
}

describe('a mask refuses what the C# one refuses, naming the parameter', () => {
  it('no stop', () => {
    expect(refusal(() => new LinearMask(90, []))).toContain("(Parameter 'stops')");
    expect(refusal(() => new RadialMask(new Point(0.5, 0.5), new RadialExtent(), []))).toContain("(Parameter 'stops')");
  });

  it.each([Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY])('an offset of %s', (offset) => {
    expect(refusal(() => new LinearMask(90, [new MaskStop(0, 1), new MaskStop(offset, 0)]))).toContain(
      "(Parameter 'stops')",
    );
  });

  it.each([1.2, -0.1, Number.NaN])('an opacity of %s', (opacity) => {
    expect(refusal(() => new LinearMask(90, [new MaskStop(0, opacity)]))).toContain("(Parameter 'stops')");
  });

  it('a centre that is not finite', () => {
    expect(refusal(() => new RadialMask(new Point(Number.NaN, 0.5), new RadialExtent(), ramp))).toContain(
      "(Parameter 'center')",
    );
  });

  it('an angle that is not finite', () => {
    expect(refusal(() => new LinearMask(Number.NaN, ramp))).toContain("(Parameter 'angle')");
  });

  it.each([0, -0.5, Number.NaN, Number.POSITIVE_INFINITY])('a radius of %s', (radius) => {
    expect(refusal(() => RadialExtent.fraction(radius, 0.5))).toContain("(Parameter 'x')");
    expect(refusal(() => RadialExtent.fraction(0.5, radius))).toContain("(Parameter 'y')");
    expect(refusal(() => RadialExtent.dp(radius, 20))).toContain("(Parameter 'x')");
    expect(refusal(() => RadialExtent.dp(20, radius))).toContain("(Parameter 'y')");
  });

  it('a node without its mask', () => {
    expect(refusal(() => new Masked(null as unknown as LinearMask, new Spacer()))).toContain("(Parameter 'mask')");
  });

  it('refuses nothing CSS accepts', () => {
    expect(() => new LinearMask(90, [new MaskStop(0.6, 1), new MaskStop(0.3, 0)])).not.toThrow();
    expect(() => new LinearMask(90, [new MaskStop(-0.5, 1), new MaskStop(1.5, 0)])).not.toThrow();
    expect(() => new RadialMask(new Point(-1, 2), RadialExtent.fraction(3, 3), ramp)).not.toThrow();
  });
});

describe('a mask resolves its stops as CSS does', () => {
  const stops = (...pairs: [number, number][]) => pairs.map(([offset, opacity]) => new MaskStop(offset, opacity));
  const pairsOf = (list: MaskStop[]) => list.map((stop) => [stop.offset, stop.opacity]);

  it('moves an offset below an earlier one up to it, and sorts nothing', () => {
    const mask = new LinearMask(90, stops([0.6, 1], [0.3, 0]));
    expect(pairsOf(mask.stops)).toEqual([[0.6, 1], [0.3, 0]]);
    expect(pairsOf(mask.resolved)).toEqual([[0.6, 1], [0.6, 0]]);
  });

  it('moves the third of three up to the second', () => {
    const mask = new LinearMask(90, stops([0, 1], [0.7, 0.5], [0.4, 0]));
    expect(pairsOf(mask.resolved)).toEqual([[0, 1], [0.7, 0.5], [0.7, 0]]);
  });

  it('keeps repeated offsets and offsets outside 0..1', () => {
    expect(pairsOf(new LinearMask(90, stops([0.5, 1], [0.5, 0.5], [0.5, 0])).resolved)).toEqual([[0.5, 1], [0.5, 0.5], [0.5, 0]]);
    expect(pairsOf(new RadialMask(new Point(0.5, 0.5), new RadialExtent(), stops([-0.5, 1], [1.5, 0])).resolved)).toEqual([[-0.5, 1], [1.5, 0]]);
  });

  it('is one stop, as written, for one stop', () => {
    expect(pairsOf(new LinearMask(90, stops([0.3, 0.5])).resolved)).toEqual([[0.3, 0.5]]);
  });

  it('copies the list it is given', () => {
    const given = stops([0, 1], [1, 0]);
    const mask = new LinearMask(90, given);
    given.push(new MaskStop(2, 1));
    expect(mask.stops).toHaveLength(2);
    expect(mask.resolved).toHaveLength(2);
  });
});

describe('a radial extent', () => {
  it('is the farthest corner by default, as the C# default is', () => {
    expect(new RadialExtent().kind).toBe('farthestCorner');
    expect(RadialExtent.farthestCorner.kind).toBe('farthestCorner');
  });

  it('carries its kind and its radii', () => {
    expect(RadialExtent.closestSide.kind).toBe('closestSide');
    expect(RadialExtent.farthestSide.kind).toBe('farthestSide');
    expect(RadialExtent.closestCorner.kind).toBe('closestCorner');
    const fraction = RadialExtent.fraction(0.7, 0.4);
    expect([fraction.kind, fraction.x, fraction.y]).toEqual(['fraction', Math.fround(0.7), Math.fround(0.4)]);
    const dp = RadialExtent.dp(50, 30);
    expect([dp.kind, dp.x, dp.y]).toEqual(['dp', 50, 30]);
  });
});
