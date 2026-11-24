import { afterEach, describe, expect, it } from 'vitest';
import { CultureCalendar } from './culture-calendar';
import { CalendarNames } from './calendar-names';
import { INVARIANT_FORMAT, installCulture, type CultureFormat } from '../utils/culture';
import { dateOnly } from '../utils/datetime';
import { Calendar } from './components/Calendar';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { measurePhotonText, setPhotonTheme } from './photon-context';
import type { HtmlNode } from '../core/types';

/**
 * What the twin counts with (#1617). Its answers in each culture are measured against .NET on both sides
 * by `CultureCalendarTwinTests`; these pin where its calendar comes from: the culture's shipped data, and
 * the Gregorian calendar where none travelled, whose month names `CalendarNames`' fallback then gives.
 */
const january17 = dateOnly.fromDayNumber(738_901);

/** th-TH's data as far as its calendar goes: the Gregorian months, 543 years ahead. */
const thai: CultureFormat = {
  ...INVARIANT_FORMAT,
  dateTimeFormat: {
    ...INVARIANT_FORMAT.dateTimeFormat,
    calendar: {
      name: 'System.Globalization.ThaiBuddhistCalendar',
      twoDigitYearMax: 2572,
      yearOffset: 543,
    },
  },
};

describe('CultureCalendar: the calendar it counts with', () => {
  afterEach(() => installCulture('en-US', 'en-US', {}));

  it('counts the calendar the culture’s data carries', () => {
    installCulture('th-TH', 'th-TH', {}, thai);
    expect([january17.year, january17.month, january17.day]).toEqual([2024, 1, 17]);
    expect(CultureCalendar.yearOf(january17)).toBe(2567);
    expect(CultureCalendar.monthOf(january17)).toBe(1);
    expect(CultureCalendar.dayOf(january17)).toBe(17);
    expect(CultureCalendar.addYears(january17, 1).dayNumber).toBe(january17.dayNumber + 366);
  });

  it('counts the Gregorian calendar where no data travelled, and the fallback names its months', () => {
    installCulture('fa-IR', 'fa-IR', {});
    expect(CultureCalendar.yearOf(january17)).toBe(2024);
    expect(CultureCalendar.firstOfMonth(january17).dayNumber).toBe(january17.dayNumber - 16);
    const gregorian = new Intl.DateTimeFormat('fa-IR', {
      month: 'long',
      timeZone: 'UTC',
      calendar: 'gregory',
    });
    expect(CalendarNames.monthNames[0]).toBe(gregorian.format(new Date(Date.UTC(2026, 0, 15))));
  });
});

/** The grid as the browser lowers it: the transpiled `Calendar`, counting through this twin. */
function lower(node: unknown): HtmlNode {
  setPhotonTheme(photonTheme);
  return lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1, measureText: measurePhotonText },
  });
}

function walk(node: HtmlNode): HtmlNode[] {
  return [node, ...node.children.flatMap(walk)];
}

function text(node: HtmlNode): string {
  return node.textContent ?? node.children.map(text).join('');
}

describe('Calendar in the browser: the grid counts in the culture’s calendar (#1617)', () => {
  afterEach(() => installCulture('en-US', 'en-US', {}));

  it('titles, fills and pages the month of the shipped calendar', () => {
    installCulture('th-TH', 'th-TH', {}, thai);
    const calendar = new Calendar(january17);

    const before = lower(calendar);
    const grid = walk(before).find((node) => node.attributes.role === 'grid');
    expect(grid?.attributes['aria-label']).toBe('January 2567');
    const cells = walk(before).filter((node) => node.attributes.role === 'gridcell');
    expect(cells.map(text)).toEqual(Array.from({ length: 31 }, (_, day) => String(day + 1)));
    expect(cells[16].attributes['aria-label']).toBe('Wednesday, 17 January 2567');

    // The next-month chevron: the second press in tree order, after the previous one.
    const presses = walk(before).flatMap((node) => (node.events.click ? [node.events.click] : []));
    presses[1]();
    const after = walk(lower(calendar)).find((node) => node.attributes.role === 'grid');
    expect(after?.attributes['aria-label']).toBe('February 2567');
  });
});
