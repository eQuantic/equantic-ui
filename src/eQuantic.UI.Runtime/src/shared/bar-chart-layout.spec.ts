/**
 * The bar chart LAYOUT parity proof, client half. The C# suite (BarChartTests.cs) solves the same
 * scenarios through BarChartLayout and dumps them to `__fixtures__/bar-chart-layout.txt`; this spec
 * solves them through the transpiled twin and asserts the same text. One dumper, mirrored line for
 * line: a bar the two compilations place a tenth of a dp apart fails here, which is the promise a
 * chart that fills whatever width it is given has to keep on every target.
 */
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { BarChartLayout } from './__transpiled__/BarChartLayout';
import { CategoryAxis } from './__transpiled__/CategoryAxis';
import { ChartSeries } from './__transpiled__/ChartSeries';
import { ValueAxis } from './__transpiled__/ValueAxis';
import { ValueScale } from './__transpiled__/ValueScale';
import type { BarChartGeometry } from './__transpiled__/BarChartGeometry';

// Resolved from the package root (vitest's cwd) — import.meta.url is http-schemed under the
// happy-dom environment, so a URL-relative read has no file to open.
const fixture = readFileSync('src/shared/__fixtures__/bar-chart-layout.txt', 'utf8');

/** The canonical data both sides solve: a negative value, a zero, and an uneven tail. */
const SERIES = [
  new ChartSeries('Alpha', [12, 18, 9, 4]),
  new ChartSeries('Beta', [15, 21, 14, 8]),
  new ChartSeries('Gamma', [-3, 6, 0, 10]),
];
const CATEGORIES = new CategoryAxis(['Q1', 'Q2', 'Q3', 'Q4']);
const ALL = [true, true, true];
const WITHOUT_BETA = [true, false, true];

/** Three decimals, floored at the half — the SAME arithmetic the C# dumper applies to the same
 * float, so a value formats identically on both sides; a negative zero prints as zero. */
function fmt(v: number): string {
  let d = Math.floor(v * 1000 + 0.5) / 1000;
  if (d === 0) d = 0;
  return String(d);
}

function dump(g: BarChartGeometry): string {
  const lines = [
    `size ${fmt(g.width)}x${fmt(g.height)} ${g.orientation}`,
    `ticks ${fmt(g.ticks.min)} ${fmt(g.ticks.max)} step ${fmt(g.ticks.step)} count ${g.ticks.count}`,
    `baseline ${fmt(g.baseline)}`,
  ];
  for (const b of g.bars) {
    lines.push(
      `bar c${b.category} s${b.series} ${fmt(b.x)},${fmt(b.y)} ${fmt(b.width)}x${fmt(b.height)}` +
        `${b.negative ? ' neg' : ''}${b.dataEnd ? ' end' : ''}`,
    );
  }
  return lines.join('\n');
}

function scenario(
  name: string,
  layout: string,
  orientation: string,
  visible: boolean[],
  axis: ValueAxis,
  width: number,
  height: number,
): string {
  const g = BarChartLayout.solve(
    SERIES,
    visible,
    CATEGORIES.categories.length,
    layout,
    orientation,
    axis,
    width,
    height,
  );
  return `== ${name} ==\n${dump(g)}`;
}

describe('bar chart layout parity (C# BarChartTests cross-pin)', () => {
  it('places every bar on the numbers the C# solver produced', () => {
    const actual =
      [
        scenario('grouped-vertical', 'grouped', 'vertical', ALL, new ValueAxis(), 320, 200),
        scenario('stacked-vertical', 'stacked', 'vertical', ALL, new ValueAxis(), 320, 200),
        scenario('grouped-horizontal', 'grouped', 'horizontal', ALL, new ValueAxis(), 320, 200),
        scenario(
          'stacked-horizontal-hidden',
          'stacked',
          'horizontal',
          WITHOUT_BETA,
          new ValueAxis(),
          320,
          200,
        ),
        scenario(
          'grouped-fixed-axis',
          'grouped',
          'vertical',
          ALL,
          new ValueAxis(null, 0, 40, 'N0', 5),
          250,
          100,
        ),
      ].join('\n') + '\n';
    expect(actual).toBe(fixture);
  });

  it('snaps the value axis to the same clean ticks', () => {
    const t = ValueScale.nice(0, 21, 5);
    expect([t.min, t.max, t.step, t.count]).toEqual([0, 25, 5, 6]);
    const small = ValueScale.nice(0, 0.7, 5);
    expect([small.min, small.max, fmt(small.step)]).toEqual([0, 0.8, '0.2']);
    const negative = ValueScale.nice(-3, 21, 5);
    expect([negative.min, negative.max, negative.step]).toEqual([-5, 25, 5]);
  });

  it('answers the pointer against the same rectangles', () => {
    const g = BarChartLayout.solve(
      SERIES,
      ALL,
      4,
      'grouped',
      'vertical',
      new ValueAxis(),
      320,
      200,
    );
    const first = g.bars[0];
    expect(BarChartLayout.hitTest(g, first.x + first.width / 2, first.y + first.height / 2)).toBe(
      0,
    );
    // The hit area reaches past the paint by the slack, and no further.
    expect(BarChartLayout.hitTest(g, first.x - BarChartLayout.hitSlack, first.y + 1)).toBe(0);
    expect(BarChartLayout.hitTest(g, -20, -20)).toBe(-1);
  });
});
