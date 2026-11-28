/**
 * The twin of the C# `OpacityGradientVocabularyTests` for what the browser holds of a mask (#1938): the refusals of each
 * kind, naming their parameter, and the stops as CSS resolves them. The C# `OpacityGradient.Resolved` is the list the web
 * writes, so the twin's `resolved` is the same list for the same stops: a stop below an earlier one moves up to
 * it, and nothing is sorted. The geometry is the browser's own (the twin writes CSS), so none of it is here.
 */

import { describe, expect, it } from 'vitest';
import { LinearOpacityGradient, OpacityMask, OpacityStop, RadialExtent, RadialOpacityGradient, Spacer } from './vocabulary';
import { Point } from './value-types';

const ramp = [new OpacityStop(0, 1), new OpacityStop(1, 0)];

function refusal(act: () => unknown): string {
  try {
    act();
  } catch (error) {
    return String((error as Error).message);
  }
  return '';
}

describe('a gradient refuses what the C# one refuses, naming the parameter', () => {
  it('no stop', () => {
    expect(refusal(() => new LinearOpacityGradient(90, []))).toContain("(Parameter 'stops')");
    expect(refusal(() => new RadialOpacityGradient(new Point(0.5, 0.5), new RadialExtent(), []))).toContain("(Parameter 'stops')");
  });

  it.each([Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY])('an offset of %s', (offset) => {
    expect(refusal(() => new LinearOpacityGradient(90, [new OpacityStop(0, 1), new OpacityStop(offset, 0)]))).toContain(
      "(Parameter 'stops')",
    );
  });

  it.each([1.2, -0.1, Number.NaN])('an opacity of %s', (opacity) => {
    expect(refusal(() => new LinearOpacityGradient(90, [new OpacityStop(0, opacity)]))).toContain("(Parameter 'stops')");
  });

  it('a centre that is not finite', () => {
    expect(refusal(() => new RadialOpacityGradient(new Point(Number.NaN, 0.5), new RadialExtent(), ramp))).toContain(
      "(Parameter 'center')",
    );
  });

  it('an angle that is not finite', () => {
    expect(refusal(() => new LinearOpacityGradient(Number.NaN, ramp))).toContain("(Parameter 'angle')");
  });

  it.each([0, -0.5, Number.NaN, Number.POSITIVE_INFINITY])('a radius of %s', (radius) => {
    expect(refusal(() => RadialExtent.fraction(radius, 0.5))).toContain("(Parameter 'x')");
    expect(refusal(() => RadialExtent.fraction(0.5, radius))).toContain("(Parameter 'y')");
    expect(refusal(() => RadialExtent.dp(radius, 20))).toContain("(Parameter 'x')");
    expect(refusal(() => RadialExtent.dp(20, radius))).toContain("(Parameter 'y')");
  });

  it('a node without its mask', () => {
    expect(refusal(() => new OpacityMask(null as unknown as LinearOpacityGradient, new Spacer()))).toContain("(Parameter 'gradient')");
  });

  it('refuses nothing CSS accepts', () => {
    expect(() => new LinearOpacityGradient(90, [new OpacityStop(0.6, 1), new OpacityStop(0.3, 0)])).not.toThrow();
    expect(() => new LinearOpacityGradient(90, [new OpacityStop(-0.5, 1), new OpacityStop(1.5, 0)])).not.toThrow();
    expect(() => new RadialOpacityGradient(new Point(-1, 2), RadialExtent.fraction(3, 3), ramp)).not.toThrow();
  });
});

describe('a gradient resolves its stops as CSS does', () => {
  const stops = (...pairs: [number, number][]) => pairs.map(([offset, opacity]) => new OpacityStop(offset, opacity));
  const pairsOf = (list: OpacityStop[]) => list.map((stop) => [stop.offset, stop.opacity]);

  it('moves an offset below an earlier one up to it, and sorts nothing', () => {
    const gradient = new LinearOpacityGradient(90, stops([0.6, 1], [0.3, 0]));
    expect(pairsOf(gradient.stops)).toEqual([[0.6, 1], [0.3, 0]]);
    expect(pairsOf(gradient.resolved)).toEqual([[0.6, 1], [0.6, 0]]);
  });

  it('moves the third of three up to the second', () => {
    const gradient = new LinearOpacityGradient(90, stops([0, 1], [0.7, 0.5], [0.4, 0]));
    expect(pairsOf(gradient.resolved)).toEqual([[0, 1], [0.7, 0.5], [0.7, 0]]);
  });

  it('keeps repeated offsets and offsets outside 0..1', () => {
    expect(pairsOf(new LinearOpacityGradient(90, stops([0.5, 1], [0.5, 0.5], [0.5, 0])).resolved)).toEqual([[0.5, 1], [0.5, 0.5], [0.5, 0]]);
    expect(pairsOf(new RadialOpacityGradient(new Point(0.5, 0.5), new RadialExtent(), stops([-0.5, 1], [1.5, 0])).resolved)).toEqual([[-0.5, 1], [1.5, 0]]);
  });

  it('is one stop, as written, for one stop', () => {
    expect(pairsOf(new LinearOpacityGradient(90, stops([0.3, 0.5])).resolved)).toEqual([[0.3, 0.5]]);
  });

  it('copies the list it is given', () => {
    const given = stops([0, 1], [1, 0]);
    const gradient = new LinearOpacityGradient(90, given);
    given.push(new OpacityStop(2, 1));
    expect(gradient.stops).toHaveLength(2);
    expect(gradient.resolved).toHaveLength(2);
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
