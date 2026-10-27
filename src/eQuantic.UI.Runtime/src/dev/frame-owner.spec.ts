/**
 * Whose code each frame ran, named in the developer's terms (#1108): the URL the browser ran it from
 * decides the owner, the map the place. No frame is named by a JavaScript file's line and column.
 */

import { describe, expect, it } from 'vitest';
import { describeFrame, nameFrames } from './frame-owner';
import type { RemappedFrame } from './stack-remapper';

const ORIGIN = 'http://localhost:5000';
const RUNTIME = `${ORIGIN}/_equantic/runtime.js?v=638950`;

const frame = (generatedUrl: string, patch: Partial<RemappedFrame> = {}): RemappedFrame => ({
  func: 'onPressed',
  file: generatedUrl,
  line: 16,
  column: 63983,
  mapped: false,
  sourceContent: null,
  generatedUrl,
  ...patch,
});

describe('nameFrames', () => {
  it("names a frame of the runtime that maps to the SDK's C# by that C#", () => {
    const [named] = nameFrames(
      [
        frame(RUNTIME, {
          mapped: true,
          file: 'eQuantic.UI.Sheet/Editing/SheetController.cs',
          line: 412,
          column: 8,
        }),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named).toMatchObject({
      owner: 'sdk',
      place: 'eQuantic.UI.Sheet/Editing/SheetController.cs',
      line: 412,
      csharp: true,
    });
    expect(describeFrame(named)).toBe(
      'onPressed — eQuantic.UI.Sheet/Editing/SheetController.cs:412',
    );
  });

  it("names a frame of the hand-written runtime by the runtime's module, its line for the report alone", () => {
    const [named] = nameFrames(
      [
        frame(RUNTIME, {
          func: 'notTheSheets',
          mapped: true,
          file: 'eQuantic.UI.Runtime/src/shared/lowering.ts',
          line: 1693,
        }),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named).toMatchObject({
      owner: 'sdk',
      place: 'eQuantic.UI runtime › shared/lowering',
      csharp: false,
    });
    expect(describeFrame(named)).toBe('notTheSheets — eQuantic.UI runtime › shared/lowering');
    expect(describeFrame(named, true)).toBe(
      'notTheSheets — eQuantic.UI runtime › shared/lowering:1693',
    );
  });

  it("names a frame of the SDK's boot, and of a transpiled module with no map of its own by its C# type", () => {
    const named = nameFrames(
      [
        frame(RUNTIME, { mapped: true, file: 'eQuantic.UI.Sdk/Resources/boot.ts' }),
        frame(RUNTIME, {
          mapped: true,
          file: 'eQuantic.UI.Runtime/src/shared/components/CodeBlock$CodeMetrics.ts',
        }),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named.map((each) => each.place)).toEqual([
      'eQuantic.UI runtime › boot',
      'eQuantic.UI runtime › CodeBlock.CodeMetrics',
    ]);
  });

  it('names a frame of the runtime no map led anywhere as the runtime, with no position', () => {
    // The query differs from the runtime's own: a frame is the runtime's by its file, whatever build
    // id the import map handed it.
    const [named] = nameFrames([frame(`${ORIGIN}/_equantic/runtime.js?v=other`)], RUNTIME, ORIGIN);

    expect(named).toMatchObject({ owner: 'sdk', place: 'eQuantic.UI runtime', line: null });
    expect(describeFrame(named, true)).not.toMatch(/\.js|63983/);
  });

  it('names an app frame by its C#, with the C# text for the source context', () => {
    const [named] = nameFrames(
      [
        frame(`${ORIGIN}/_equantic/SheetScreen.js?v=1`, {
          mapped: true,
          file: 'Screens/SheetScreen.cs',
          line: 12,
          sourceContent: 'class SheetScreen {}',
        }),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named).toMatchObject({
      owner: 'app',
      place: 'Screens/SheetScreen.cs',
      line: 12,
      csharp: true,
      sourceContent: 'class SheetScreen {}',
    });
  });

  it("names an app frame's C# by the project's own path, and a type bun renamed by its C# name", () => {
    const named = nameFrames(
      [
        frame(`${ORIGIN}/_equantic/Home.js`, {
          func: 'Home2.onPressed',
          mapped: true,
          file: '../../Screens/Home.cs',
          line: 4,
        }),
        frame(RUNTIME, {
          func: 'SheetController3.moveTo',
          mapped: true,
          file: 'eQuantic.UI.Sheet/Editing/SheetController.cs',
          line: 220,
        }),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named.map((each) => describeFrame(each))).toEqual([
      'Home.onPressed — Screens/Home.cs:4',
      'SheetController.moveTo — eQuantic.UI.Sheet/Editing/SheetController.cs:220',
    ]);
  });

  it('names an app frame with no map by its module, a chunk by the module it is named after', () => {
    const named = nameFrames(
      [
        frame(`${ORIGIN}/_equantic/Dashboard.js?v=1`),
        frame(`${ORIGIN}/_equantic/NotFoundScreen-a3bmepcm.js`),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named.map((each) => [each.owner, each.place, each.line])).toEqual([
      ['app', 'Dashboard', null],
      ['app', 'NotFoundScreen', null],
    ]);
  });

  it('names an app frame its map left in a TS intermediate by the module', () => {
    const [named] = nameFrames(
      [
        frame(`${ORIGIN}/_equantic/Dashboard.js`, {
          mapped: true,
          file: '../../obj/eQuantic/ts/Dashboard.ts',
          line: 3,
        }),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named).toMatchObject({ owner: 'app', place: 'Dashboard', line: null, csharp: false });
  });

  it('names a frame of a script the page did not write by its host, outside the app', () => {
    const named = nameFrames(
      [
        frame('https://www.googletagmanager.com/gtm.js?id=GTM-1'),
        frame(`${ORIGIN}/_framework/aspnetcore-browser-refresh.js`),
      ],
      RUNTIME,
      ORIGIN,
    );

    expect(named.map((each) => [each.owner, each.place])).toEqual([
      ['external', 'www.googletagmanager.com'],
      ['external', 'localhost:5000'],
    ]);
  });
});
