/**
 * The code editor ON THE WEB, through the REAL transpiled modules.
 *
 * This is the write-once proof for the editor: the document, the tokenizers, the undo history, the
 * controller and the KEYMAP are all eqc output from the same C# the macOS host runs, and this drives
 * them the way a browser does — a keydown with modifier flags, a pointerdown with client coordinates.
 * Nothing here reimplements a single editor behaviour, which is the whole point: the moment it had
 * to, the two targets would have started to drift.
 */

import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { setPhotonTheme } from './photon-context';
import { CodeSurface } from './vocabulary';
import { CodeEditorController } from './components/CodeEditorController';
import { CodeLanguages } from './components/CodeLanguages';
import { CodeDocument } from './components/CodeDocument';
import { Text } from './vocabulary';
import type { HtmlNode } from '../core/types';

setPhotonTheme(photonTheme);

const LINE = 18;
const COLUMN = 8;

function surfaceFor(code: string) {
  const editor = new CodeEditorController(code, CodeLanguages.for('csharp'));
  let changes = 0;
  const node = new CodeSurface(new Text('', 'labelSmall'), editor, {
    contentTop: 12,
    contentLeft: 12,
    lineHeight: LINE,
    columnWidth: COLUMN,
    onChanged: () => changes++,
  });
  const lowered = lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });
  return { editor, lowered, changed: () => changes };
}

/** Fires the surface's own keydown handler the way the browser would. */
function press(
  lowered: HtmlNode,
  key: string,
  modifiers: { shift?: boolean; alt?: boolean; meta?: boolean } = {},
) {
  let prevented = false;
  const event = {
    key,
    shiftKey: modifiers.shift === true,
    altKey: modifiers.alt === true,
    metaKey: modifiers.meta === true,
    ctrlKey: false,
    preventDefault: () => {
      prevented = true;
    },
  };
  (lowered.events['keydown'] as unknown as (e: unknown) => void)(event);
  return prevented;
}

describe('code surface (web)', () => {
  it('is focusable and says what it is', () => {
    const { lowered } = surfaceFor('var x = 1;');

    expect(lowered.tag).toBe('div');
    expect(lowered.attributes['tabindex']).toBe('0');
    expect(lowered.attributes['role']).toBe('textbox');
    expect(lowered.attributes['aria-multiline']).toBe('true');
  });

  it('draws a caret where the model says it is', () => {
    const { editor, lowered } = surfaceFor('one\ntwo\nthree');
    editor.selection = { anchor: { line: 2, column: 4 }, focus: { line: 2, column: 4 } } as never;

    const redrawn = surfaceFor('one\ntwo\nthree');
    redrawn.editor.selection = {
      anchor: { line: 2, column: 4 },
      focus: { line: 2, column: 4 },
    } as never;
    const node = lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), redrawn.editor, {
        contentTop: 12,
        contentLeft: 12,
        lineHeight: LINE,
        columnWidth: COLUMN,
      }) as never,
      { textPrimary: photonTheme.textPrimary, componentContext: { theme: photonTheme, typeScale: 1 } },
    );

    const caret = node.children.find((c) => c.attributes['class'] === 'eq-code-caret')!;
    expect(caret.attributes['style']).toContain(`left:${12 + 4 * COLUMN}px`);
    expect(caret.attributes['style']).toContain(`top:${12 + 2 * LINE}px`);
    expect(lowered).toBeDefined();
  });

  it('draws ONE BAND PER LINE of a selection, never one rectangle over the range', () => {
    const { editor } = surfaceFor('first line\nsecond\nthird line');
    editor.selection = {
      anchor: { line: 0, column: 2 },
      focus: { line: 2, column: 3 },
    } as never;

    const node = lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), editor, {
        contentTop: 0,
        contentLeft: 0,
        lineHeight: LINE,
        columnWidth: COLUMN,
      }) as never,
      { textPrimary: photonTheme.textPrimary, componentContext: { theme: photonTheme, typeScale: 1 } },
    );

    const bands = node.children.filter((c) => c.attributes['class'] === 'eq-code-selection');
    expect(bands).toHaveLength(3);
    // The first starts at its column; the last ends at its own; the middle one is the whole line.
    expect(bands[0].attributes['style']).toContain(`left:${2 * COLUMN}px`);
    expect(bands[2].attributes['style']).toContain('left:0px');
  });
});

describe('code surface keyboard (the SAME keymap the native host calls)', () => {
  it('types characters through the controller, pairs and all', () => {
    const { editor, lowered } = surfaceFor('var x = ');
    editor.selection = { anchor: editor.document.end, focus: editor.document.end } as never;

    press(lowered, '(');

    expect(editor.document.text).toBe('var x = ()');
    expect(editor.caret.column).toBe(9);
  });

  it('Enter keeps the indentation and steps into a block', () => {
    const { editor, lowered } = surfaceFor('    if (x) {');
    editor.selection = { anchor: editor.document.end, focus: editor.document.end } as never;

    expect(press(lowered, 'Enter')).toBe(true);

    expect(editor.document.lines).toEqual(['    if (x) {', '        ']);
  });

  it('Tab indents rather than leaving', () => {
    const { editor, lowered } = surfaceFor('one\ntwo');
    press(lowered, 'a', { meta: true });
    press(lowered, 'Tab');

    expect(editor.document.lines).toEqual(['    one', '    two']);
  });

  it('undo takes back a RUN of typing, not one letter', () => {
    const { editor, lowered } = surfaceFor('');
    for (const c of 'hello') press(lowered, c);
    expect(editor.document.text).toBe('hello');

    press(lowered, 'z', { meta: true });
    expect(editor.document.text).toBe('');

    press(lowered, 'z', { meta: true, shift: true });
    expect(editor.document.text).toBe('hello');
  });

  it('⇧-arrows extend from the anchor', () => {
    const { editor, lowered } = surfaceFor('abcdef');
    editor.selection = { anchor: { line: 0, column: 2 }, focus: { line: 0, column: 2 } } as never;

    press(lowered, 'ArrowRight', { shift: true });
    press(lowered, 'ArrowRight', { shift: true });

    expect(editor.document.textIn(editor.selection)).toBe('cd');
  });

  it('⌘/ comments the line, and the language decides how', () => {
    const { editor, lowered } = surfaceFor('var x = 1;');
    press(lowered, '/', { meta: true });

    expect(editor.document.line(0)).toBe('// var x = 1;');
  });

  it('reports every change so the component can rebuild', () => {
    const { lowered, changed } = surfaceFor('');
    for (const c of 'abc') press(lowered, c);

    expect(changed()).toBe(3);
  });

  it('does not claim a key that is not its own', () => {
    const { lowered } = surfaceFor('code');
    expect(press(lowered, 'F5')).toBe(false);
    expect(press(lowered, 'Escape')).toBe(false);
  });
});

describe('the tokenizers, running in the browser', () => {
  it('colour C# the same way they colour it natively', () => {
    const tokens: unknown[] = [];
    CodeLanguages.cSharp.tokenize('public static string Name() => "hi";', 0, tokens as never);

    const kinds = (tokens as { start: number; end: number; kind: string }[]);
    expect(kinds.find((t) => t.start === 0)!.kind).toBe('keyword');
    expect(kinds.find((t) => t.start === 14)!.kind).toBe('type');
    expect(kinds.find((t) => t.start === 21)!.kind).toBe('function');
    expect(kinds.find((t) => t.start === 31)!.kind).toBe('string');
  });

  it('carry state across a line the way a block comment needs', () => {
    const tokens: unknown[] = [];
    const state = CodeLanguages.cSharp.tokenize('/* opened', 0, tokens as never);
    expect(state).not.toBe(0);

    const next: unknown[] = [];
    CodeLanguages.cSharp.tokenize('still a comment', state, next as never);
    expect((next as { kind: string }[])[0].kind).toBe('comment');
  });

  it('normalise whatever line endings were pasted', () => {
    expect(CodeDocument.fromText('a\r\nb\rc\nd').lines).toEqual(['a', 'b', 'c', 'd']);
  });
});
