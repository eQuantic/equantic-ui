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
import { CodeSurface, Text, type VisualNode } from './vocabulary';
import { Point, Size } from './value-types';
import { CodeEditorController } from './components/CodeEditorController';
import { CodeGrid } from './components/CodeGrid';
import { CodePosition } from './components/CodePosition';
import { CodeRange } from './components/CodeRange';
import { CodeLanguages } from './components/CodeLanguages';
import { CodeDocument } from './components/CodeDocument';
import type { HtmlNode } from '../core/types';

setPhotonTheme(photonTheme);

const LINE = 18;
const COLUMN = 8;

function surfaceFor(code: string) {
  const editor = new CodeEditorController(code, CodeLanguages.for('csharp'));
  // The grid belongs to the ENGINE now: it is the only thing that turns a position into a point.
  editor.grid = new CodeGrid(new Point(12, 12), new Size(COLUMN, LINE));
  let changes = 0;
  const node = new CodeSurface(new Text('', 'labelSmall'), editor, {
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

  /**
   * …and it says WHICH surface it is, across rebuilds.
   *
   * Every keystroke hands over a new element, so anything that has to find the surface AFTER the
   * render — bringing the caret back into view is the one that needs it — has to resolve by this
   * and not by the reference the handler ran on. Holding the element instead looked implemented
   * and did nothing: scrollIntoView on a detached caret succeeds in silence.
   */
  it('carries its path, so it can be found again after the render that replaced it', () => {
    const { lowered } = surfaceFor('var x = 1;');

    expect(lowered.attributes['data-eq-code']).toBeTruthy();
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
      new CodeSurface(new Text('', 'labelSmall'), redrawn.editor) as never,
      {
        textPrimary: photonTheme.textPrimary,
        componentContext: { theme: photonTheme, typeScale: 1 },
      },
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

    editor.grid = new CodeGrid(new Point(0, 0), new Size(COLUMN, LINE));
    const node = lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), editor) as never,
      {
        textPrimary: photonTheme.textPrimary,
        componentContext: { theme: photonTheme, typeScale: 1 },
      },
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

/**
 * The POINTER, through the same model the native host drives. Drag selection and shift-click did
 * not exist on this side at all: the surface set the caret on the press and listened to nothing
 * after it, while the window drew a selection on the drag — two copies of what a click means, and
 * one of them short. The model owns the meaning now; this only forwards the press, the moves and
 * the release.
 */
function pointer(
  lowered: HtmlNode,
  type: 'pointerdown' | 'pointermove' | 'pointerup' | 'mousedown',
  x: number,
  y: number,
  options: { detail?: number; shift?: boolean; buttons?: number; pointerType?: string } = {},
) {
  const target = {
    getBoundingClientRect: () => ({ left: 0, top: 0 }),
    setPointerCapture: () => {},
    releasePointerCapture: () => {},
    focus: () => {},
  };
  const event = {
    clientX: x,
    clientY: y,
    currentTarget: target,
    pointerId: 1,
    pointerType: options.pointerType ?? 'mouse',
    detail: options.detail ?? 1,
    buttons: options.buttons ?? (type === 'pointerup' ? 0 : 1),
    shiftKey: options.shift === true,
    altKey: false,
    metaKey: false,
    ctrlKey: false,
  };
  (lowered.events[type] as unknown as (e: unknown) => void)(event);
}

/**
 * A MOUSE press the way a browser delivers one: `pointerdown` first (Chrome reports `detail: 0`
 * there, measured), then `mousedown`, which carries the platform's click count.
 */
function mousePress(
  lowered: HtmlNode,
  x: number,
  y: number,
  options: { clicks?: number; shift?: boolean } = {},
) {
  pointer(lowered, 'pointerdown', x, y, { detail: 0, shift: options.shift });
  pointer(lowered, 'mousedown', x, y, { detail: options.clicks ?? 1, shift: options.shift });
}

/** The client point at the centre of a character cell, on the 12/12 grid `surfaceFor` sets. */
const cell = (line: number, column: number): [number, number] => [
  12 + column * COLUMN,
  12 + line * LINE + LINE / 2,
];

function pressAt(
  lowered: HtmlNode,
  at: [number, number],
  options: { clicks?: number; shift?: boolean } = {},
) {
  mousePress(lowered, at[0], at[1], options);
}

describe('code surface pointer (the SAME model the native host drives)', () => {
  it('a press puts the caret where it was aimed', () => {
    const { editor, lowered } = surfaceFor('one two three\nfour');
    pressAt(lowered, cell(1, 2));
    expect(editor.caret).toEqual({ line: 1, column: 2 });
  });

  it('a drag draws a selection from the press to the pointer', () => {
    const { editor, lowered, changed } = surfaceFor('one two three');
    pressAt(lowered, cell(0, 0));
    pointer(lowered, 'pointermove', ...(cell(0, 7) as [number, number]));
    pointer(lowered, 'pointerup', ...(cell(0, 7) as [number, number]));

    expect(editor.document.textIn(editor.selection)).toBe('one two');
    expect(changed()).toBeGreaterThanOrEqual(2);
  });

  it('a hover with nothing pressed moves nothing', () => {
    const { editor, lowered } = surfaceFor('one two three');
    pressAt(lowered, cell(0, 0));
    pointer(lowered, 'pointerup', ...(cell(0, 0) as [number, number]));
    pointer(lowered, 'pointermove', ...(cell(0, 7) as [number, number]), { buttons: 0 });

    expect(editor.selection.isEmpty).toBe(true);
  });

  it('shift-click extends the selection from where it was', () => {
    const { editor, lowered } = surfaceFor('one two three');
    pressAt(lowered, cell(0, 4));
    pointer(lowered, 'pointerup', ...(cell(0, 4) as [number, number]));
    pressAt(lowered, cell(0, 13), { shift: true });

    expect(editor.document.textIn(editor.selection)).toBe('two three');
  });

  it('two presses take the word, three the line — counted by the mousedown', () => {
    const { editor, lowered } = surfaceFor('one two three\nfour');
    pressAt(lowered, cell(0, 5), { clicks: 2 });
    expect(editor.document.textIn(editor.selection)).toBe('two');

    pressAt(lowered, cell(0, 5), { clicks: 3 });
    expect(editor.document.textIn(editor.selection)).toBe('one two three\n');
  });

  it('a finger presses on the pointerdown, and its late compatibility mousedown is not a second press', () => {
    const { editor, lowered } = surfaceFor('one two three');
    pointer(lowered, 'pointerdown', ...(cell(0, 5) as [number, number]), { pointerType: 'touch', detail: 0 });
    expect(editor.caret).toEqual({ line: 0, column: 5 });

    // A browser follows a tap with emulated mouse events once the touch has ENDED; the second of
    // them must not re-press somewhere else (and a count of 2 must not select a word).
    pointer(lowered, 'mousedown', ...(cell(0, 1) as [number, number]), { detail: 2 });
    expect(editor.caret).toEqual({ line: 0, column: 5 });
    expect(editor.selection.isEmpty).toBe(true);
  });
});

describe('the tokenizers, running in the browser', () => {
  it('colour C# the same way they colour it natively', () => {
    const tokens: unknown[] = [];
    CodeLanguages.cSharp.tokenize('public static string Name() => "hi";', 0, tokens as never);

    const kinds = tokens as { start: number; end: number; kind: string }[];
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

/**
 * The COMPONENT, not just its parts: CodeEditor.build hands its live document and resolved
 * language to a CodeBlock, and the transpiled twin has ONE constructor whose positional body is
 * the string shape. The C# overload that took (document, language) positionally compiled fine,
 * passed every parts-level test here, and died in the first browser that mounted an editor —
 * so the whole component now builds and lowers in this suite.
 */
describe('code editor (the component, end to end)', () => {
  it('builds and lowers with a real document and language', async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const { setPhotonTheme } = await import('./photon-context');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    // The SAME theme path boot runs: the wire fixture through materializeTheme, installed as the
    // active theme (lowering reads the singleton, not the context passed here).
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const editor = new CodeEditor('var x = 1;', 'csharp');
    const context = {
      theme,
      density: 'comfortable',
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const tree = editor.build(context as never);
    const node = lowerVisualNode(tree as never, context as never);
    expect(node.tag).toBeTruthy();
    expect(editor.editor.document.text).toBe('var x = 1;');
    expect(editor.editor.highlighter.language.name).toBe('C#');
  });
});

/**
 * A COMPONENT centres like any other node. In C# `Centered()` is an extension on VisualNode, and
 * a component is one — so `Card(…).Centered()` compiled there and called nothing here: the page
 * mounted with "centered is not a function" and the frame went blank.
 *
 * The answer was an instance method mirrored on `Component` AND on `VisualNode`, and it cost the
 * collision in #245: a component written `class StatTile(string label, bool centered = false)`
 * lowered its captured parameter to a field of that name, which shadowed the method, and the page
 * failed only in the browser again. The extension lowers to its STATIC home now, which a field
 * cannot shadow — so what this case pins is the same behaviour reached the way JavaScript reaches
 * it.
 */
describe('components centre like nodes', () => {
  it('a component centres through the extension home, as any node does', async () => {
    const { Card } = await import('./components/Card');
    const { Text } = await import('./vocabulary');
    const { VisualNodeExtensions } = await import('./visual-node-extensions');
    // A Row, which the vocabulary types as a VisualNode — so this reads the two fields a Row
    // carries and the base does not; going through `unknown` is the honest way to say "the
    // concrete node, not the base".
    // No cast on the ARGUMENT: a component is a legal receiver in C# and has to be one here,
    // and `as never` would hide exactly the mismatch this case exists to catch.
    const centred = VisualNodeExtensions.centered(
      new Card(new Text('hi') as never),
    ) as unknown as {
      nodeKind: string;
      children: unknown[];
    };
    expect(centred.nodeKind).toBe('row');
    expect(centred.children).toHaveLength(1);
  });

  it('a stateful component IS a VisualNode, the way C# says it is', async () => {
    const { TimePicker } = await import('./components/TimePicker');

    // A TYPE-level pin, and the assertion below is almost beside the point: what this case guards
    // is that the LINE COMPILES. In C# `UiComponent` derives from `VisualNode`, so eqc emits
    // `let x: VisualNode = new SomeComponent()` verbatim, and that emission has to typecheck here.
    // The failure comes from `tsc`, never from a run, which is why it went unnoticed until the
    // first component was assigned to one.
    const node: VisualNode = new TimePicker();
    expect(node.nodeKind).toBe('component');
  });

  it('a STATELESS component is deliberately not one — the absence is the signal', async () => {
    const { Card } = await import('./components/Card');
    const { Text } = await import('./vocabulary');

    // Not an oversight, and worth pinning so nobody "fixes" it by moving nodeKind onto the base:
    // the lowering's MIXING SEAM recognises a web component by its LACK of a nodeKind, embedding the
    // HtmlNode it renders for itself. Give every component the field and that routing changes for
    // the whole library — a runtime decision, not a typing one. The seam used to be the lowering's
    // default arm and is now the check ahead of its switch, since `nodeKind` became a closed union
    // and the default arm became the compiler's (node-kinds.spec.ts walks it from the other side).
    const card = new Card(new Text('hi') as never) as unknown as { nodeKind?: string };
    expect(card.nodeKind).toBeUndefined();
  });
});

/**
 * A caret you cannot see is a caret you do not have. Both marks were rendered with the right
 * geometry and filled with NOTHING — the class they carry was never defined anywhere, so the DOM
 * looked correct and the screen said nothing about where you were typing.
 */
describe('the marks are painted, not merely placed', () => {
  const surfaceWith = (caretColor?: unknown, selectionColor?: unknown) => {
    const editor = new CodeEditorController('let x = 1;\nlet y = 2;', CodeLanguages.for('csharp'));
    editor.grid = new CodeGrid(new Point(12, 12), new Size(COLUMN, LINE));
    editor.selectAll();
    return lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), editor, {
        caretColor,
        selectionColor,
      }) as never,
      {
        textPrimary: photonTheme.textPrimary,
        componentContext: { theme: photonTheme, typeScale: 1 },
      },
    );
  };

  const styleOf = (node: HtmlNode, className: string) =>
    node.children.find((c) => c.attributes['class'] === className)!.attributes['style']!;

  it('paints the caret with the theme ink when the node names none', () => {
    expect(styleOf(surfaceWith(), 'eq-code-caret')).toContain('background-color:');
  });

  it('paints the caret with the NODE ink — an inverse slab writes with its own', () => {
    const ink = {
      light: { r: 0xc9, g: 0xd4, b: 0xde, a: 255 },
      dark: { r: 0xc9, g: 0xd4, b: 0xde, a: 255 },
    };

    // A page theme's TextPrimary is nearly black; on the playground's dark slab that caret is the
    // slab. The colour has to ride the node, which is what makes it right on both targets.
    expect(styleOf(surfaceWith(ink), 'eq-code-caret')).toContain('background-color:#c9d4de');
  });

  it('washes the selection band to 28% — the C# SelectionAlpha twin', () => {
    const blue = {
      light: { r: 0x00, g: 0x50, b: 0xa0, a: 255 },
      dark: { r: 0x00, g: 0x50, b: 0xa0, a: 255 },
    };

    // 255 × 0.28 = 71.4 → 71 = 0x47. The text reads THROUGH the band; an opaque one hides the line
    // you just selected.
    expect(styleOf(surfaceWith(undefined, blue), 'eq-code-selection')).toContain(
      'background-color:#0050a047',
    );
  });
});

describe('the code surface goes through the atomizer, like every other node', () => {
  // It carried a literal `style` string, under a comment reasoning that "there is no C# twin to
  // agree with — the web realizer has no CodeSurface arm". That is STILL TRUE: the arm was tried
  // and taken back out, because the client appends a caret and a server tree without one is a
  // failed adoption. The string went anyway, because the reasoning was never worth leaving
  // standing — the day an arm arrives, a client string beside a server class is the hydration
  // mismatch the atomizer exists to prevent, and that day should not also be the day somebody has
  // to remember this. Going through the shared atomizer costs nothing and removes the trap.
  it('emits classes and no inline style', () => {
    const { lowered } = surfaceFor('let x = 1;');

    expect(lowered.attributes['style']).toBeUndefined();
    expect(lowered.attributes['class'] ?? '').toContain('eq-code-surface');
  });
});

describe('a value the engine builds with no arguments is its zeros, as C# builds it', () => {
  // C# `new CodeGrid()` is a zeroed Point and a zeroed Size. The twin assigned null to both, so
  // the first pointOf on a grid built that way threw where the C# answered the origin.
  it('a default grid answers the origin for every position', () => {
    const grid = new CodeGrid();

    expect(grid.pointOf(3, 4)).toEqual(new Point(0, 0));
    expect(grid.origin).toEqual(Point.zero);
    expect(grid.cell).toEqual(Size.zero);
  });

  it('a default range is empty, at the first position of the document', () => {
    const range = new CodeRange();

    expect(range.isEmpty).toBe(true);
    expect(range.start).toEqual(new CodePosition(0, 0));
  });
});
