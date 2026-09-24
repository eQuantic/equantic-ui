/**
 * The code editor ON THE WEB, through the REAL transpiled modules.
 *
 * This is the write-once proof for the editor: the document, the tokenizers, the undo history, the
 * controller and the KEYMAP are all eqc output from the same C# the macOS host runs, and this drives
 * them the way a browser does. The keyboard types through the surface's INPUT, a real textarea: a
 * keydown for what the keymap claims, a `beforeinput` for the text the platform decided on, the
 * composition events of an input method, the clipboard's own events. The pointer presses the
 * surface. Nothing here reimplements a single editor behaviour, which is the whole point: the
 * moment it had to, the two targets would have started to drift.
 */

import { describe, expect, it, vi } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { setPhotonTheme } from './photon-context';
import { effectiveStyle } from './style-atomizer';
import { CodeSurface, Text, type VisualNode } from './vocabulary';
import { Point, Size } from './value-types';
import { CodeEditorController } from './components/CodeEditorController';
import { CodeGrid } from './components/CodeGrid';
import { CodePosition } from './components/CodePosition';
import { CodeRange } from './components/CodeRange';
import { CodeLanguages } from './components/CodeLanguages';
import { CodeDocument } from './components/CodeDocument';
import type { HtmlNode } from '../core/types';
import { Reconciler } from '../dom/reconciler';
import { activeShortcuts, commitShortcuts, resetShortcuts } from '../dom/shortcuts';

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

/** The surface's INPUT — the textarea the keyboard types through. */
function inputOf(lowered: HtmlNode): HtmlNode {
  return lowered.children.find((c) => c.tag === 'textarea')!;
}

/** Fires an event on the surface's input, and answers whether it was prevented. */
function fire(lowered: HtmlNode, type: string, event: Record<string, unknown>): boolean {
  let prevented = false;
  const full = {
    ...event,
    target: { value: '' },
    preventDefault: () => {
      prevented = true;
    },
  };
  (inputOf(lowered).events[type] as unknown as (e: unknown) => void)(full);
  return prevented;
}

/** A key the way the browser reports one to the input, and whether the editor CLAIMED it. */
function press(
  lowered: HtmlNode,
  key: string,
  modifiers: { shift?: boolean; alt?: boolean; meta?: boolean; ctrl?: boolean; composing?: boolean } = {},
) {
  return fire(lowered, 'keydown', {
    key,
    shiftKey: modifiers.shift === true,
    altKey: modifiers.alt === true,
    metaKey: modifiers.meta === true,
    ctrlKey: modifiers.ctrl === true,
    isComposing: modifiers.composing === true,
    keyCode: modifiers.composing === true ? 229 : 0,
  });
}

/** Text the platform decided the user typed — what `beforeinput` carries, never the keydown. */
function type(lowered: HtmlNode, text: string) {
  for (const c of text) fire(lowered, 'beforeinput', { inputType: 'insertText', data: c });
}

describe('code surface (web)', () => {
  it('types through an input of its own, which says what it is', () => {
    const { lowered } = surfaceFor('var x = 1;');
    const input = inputOf(lowered);

    // A real textarea: an input method opens its window at it, a phone raises its keyboard for
    // it, and a screen reader meets a multi-line text field.
    expect(input.tag).toBe('textarea');
    expect(input.attributes['aria-multiline']).toBe('true');
    // No role description: a string the runtime writes is English for every reader, and the name
    // it announces is the app's own label, in the app's language.
    expect(input.attributes['aria-roledescription']).toBeUndefined();
    expect(input.attributes['spellcheck']).toBe('false');
    // The surface around it is not a second tab stop.
    expect(lowered.attributes['tabindex']).toBeUndefined();
  });

  it('keeps its input at the caret, so a candidate window opens where the text goes', () => {
    const { editor, lowered } = surfaceFor('one\ntwo');
    editor.selection = new CodeRange(new CodePosition(1, 2));
    const redrawn = lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), editor) as never,
      { textPrimary: photonTheme.textPrimary, componentContext: { theme: photonTheme, typeScale: 1 } },
    );

    expect(inputOf(redrawn).attributes['style']).toContain(`left:${12 + 2 * COLUMN}px`);
    expect(inputOf(redrawn).attributes['style']).toContain(`top:${12 + 1 * LINE}px`);
    expect(lowered).toBeDefined();
  });

  // In the shared px() spelling (C# TokenCss.Px), the one the server writes the same caret and input
  // in (SurfaceSsrTests.ItsCaretAndItsInputAreWrittenWhereTheClientWritesThem).
  it('spells its caret and its input the way the server spells them', () => {
    const { editor } = surfaceFor('one\ntwo');
    editor.selection = new CodeRange(new CodePosition(1, 2));
    const node = lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), editor) as never,
      { textPrimary: photonTheme.textPrimary, componentContext: { theme: photonTheme, typeScale: 1 } },
    );

    const caret = node.children.find((c) => c.attributes['class'] === 'eq-code-caret')!;
    expect(caret.attributes['style']).toContain('position:absolute;left:28px;top:30px;width:2px;height:18px;');
    expect(inputOf(node).attributes['style']).toBe('left:28px;top:30px;height:18px;');
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

  it('answers ONE BAND PER LINE of a selection, never one rectangle over the range', () => {
    const { editor } = surfaceFor('first line\nsecond\nthird line');
    editor.selection = new CodeRange(new CodePosition(0, 2), new CodePosition(2, 3));
    editor.grid = new CodeGrid(new Point(0, 0), new Size(COLUMN, LINE));

    // The ENGINE's answer, which the component draws in the code's own layers (see the component
    // case below). The first starts at its column, the middle one is the whole line, and the last
    // starts at the line's start.
    const bands = editor.selectionBandsIn(0, 2);
    expect(bands).toHaveLength(3);
    expect(bands[0].x).toBe(2 * COLUMN);
    expect(bands[2].x).toBe(0);
    // …and only for the lines it is asked about: a view asks for the lines it builds.
    expect(editor.selectionBandsIn(1, 1)).toHaveLength(1);
  });

  it('draws no selection of its own: the component draws it, under the text', () => {
    const { editor } = surfaceFor('one\ntwo');
    editor.selectAll();
    const node = lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), editor) as never,
      { textPrimary: photonTheme.textPrimary, componentContext: { theme: photonTheme, typeScale: 1 } },
    );

    expect(node.children.some((c) => c.attributes['class'] === 'eq-code-selection')).toBe(false);
  });
});

describe('code surface keyboard (the SAME keymap the native host calls)', () => {
  it('types characters through the controller, pairs and all', () => {
    const { editor, lowered } = surfaceFor('var x = ');
    editor.selection = { anchor: editor.document.end, focus: editor.document.end } as never;

    type(lowered, '(');

    expect(editor.document.text).toBe('var x = ()');
    expect(editor.caret.column).toBe(9);
  });

  it('takes text from the input, never from the key that produced it', () => {
    const { editor, lowered } = surfaceFor('');

    // The keydown of a printable key is not the editor's: what it produces is the platform's
    // business (a dead key, an input method, "á" from three events), and it arrives as input.
    expect(press(lowered, 'a')).toBe(false);
    expect(editor.document.text).toBe('');

    type(lowered, 'a');
    expect(editor.document.text).toBe('a');
  });

  // AltGr IS Ctrl+Alt on Windows, so `{ [ @` on a European layout arrived as a Ctrl chord and the
  // old keydown path, which typed only without Ctrl, dropped them. The chord is not the editor's,
  // and the character arrives as input like any other.
  it('takes what AltGr types, which arrives as a Ctrl+Alt chord', () => {
    const { editor, lowered } = surfaceFor('');

    expect(press(lowered, '{', { ctrl: true, alt: true })).toBe(false);
    type(lowered, '{');

    expect(editor.document.text.startsWith('{')).toBe(true);
  });

  it("takes a soft keyboard's Enter and Backspace, which arrive as input rather than as keys", () => {
    const { editor, lowered } = surfaceFor('ab');
    editor.selection = new CodeRange(new CodePosition(0, 2));

    fire(lowered, 'beforeinput', { inputType: 'deleteContentBackward' });
    expect(editor.document.text).toBe('a');

    fire(lowered, 'beforeinput', { inputType: 'insertLineBreak' });
    expect(editor.document.lines).toEqual(['a', '']);
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
    type(lowered, 'hello');
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
    type(lowered, 'abc');

    expect(changed()).toBe(3);
  });

  it('does not claim a key that is not its own', () => {
    const { lowered } = surfaceFor('code');
    expect(press(lowered, 'F5')).toBe(false);
    expect(press(lowered, 'Escape')).toBe(false);
  });

  // An editor TAKES Tab, and a keyboard user must still be able to leave it: Escape releases the
  // trap, so the next Tab moves on instead of indenting, and any other key sets it again.
  it('lets Escape release Tab, so a keyboard user can leave', () => {
    const { editor, lowered } = surfaceFor('one');

    press(lowered, 'Escape');
    expect(press(lowered, 'Tab')).toBe(false);
    expect(editor.document.text).toBe('one');

    press(lowered, 'ArrowRight');
    expect(press(lowered, 'Tab')).toBe(true);
  });

  it('leaves the keys an input method is using alone', () => {
    const { editor, lowered } = surfaceFor('one');

    // Enter picks a candidate while a composition is open; it must not also break the line.
    expect(press(lowered, 'Enter', { composing: true })).toBe(false);
    expect(editor.document.text).toBe('one');
  });
});

/**
 * An INPUT METHOD, the way a browser reports one: the composition grows in the document, is shown
 * underlined, and a commit lands as ONE edit however many steps the candidate window went through.
 */
describe('code surface composition (an input method, through the same model)', () => {
  it('shows the composition in the document while it grows', () => {
    const { editor, lowered } = surfaceFor('a');
    editor.selection = new CodeRange(new CodePosition(0, 1));

    fire(lowered, 'compositionupdate', { data: 'k' });
    fire(lowered, 'compositionupdate', { data: 'ka' });

    expect(editor.document.text).toBe('aka');
    expect(editor.composition).not.toBeNull();
  });

  it('commits as one edit, which one undo takes back', () => {
    const { editor, lowered } = surfaceFor('a');
    editor.selection = new CodeRange(new CodePosition(0, 1));

    fire(lowered, 'compositionupdate', { data: 'k' });
    fire(lowered, 'compositionupdate', { data: 'ka' });
    fire(lowered, 'compositionend', { data: 'か' });
    expect(editor.document.text).toBe('aか');
    expect(editor.composition).toBeNull();

    press(lowered, 'z', { meta: true });
    expect(editor.document.text).toBe('a');
  });

  it('leaves the document as it was when the composition is cancelled', () => {
    const { editor, lowered } = surfaceFor('a');
    editor.selection = new CodeRange(new CodePosition(0, 1));

    fire(lowered, 'compositionupdate', { data: 'x' });
    fire(lowered, 'compositionend', { data: '' });

    expect(editor.document.text).toBe('a');
    expect(editor.composition).toBeNull();
  });

  /**
   * A commit is taken ONCE. The spec's order ends a composition with compositionend and no text of
   * its own as input, but a browser that also sends the committed text as an insertText, before
   * compositionend or right after it, put it in twice. Found in review.
   */
  it('commits once when the browser also sends the committed text as input, right after', () => {
    const { editor, lowered } = surfaceFor('a');
    editor.selection = new CodeRange(new CodePosition(0, 1));

    fire(lowered, 'compositionupdate', { data: 'か' });
    fire(lowered, 'compositionend', { data: 'か' });
    const echo = fire(lowered, 'beforeinput', { inputType: 'insertText', data: 'か', isComposing: false });

    expect(editor.document.text).toBe('aか');
    // Cancelled, so the input the keyboard types through stays empty.
    expect(echo).toBe(true);
  });

  it('commits once when that input arrives before compositionend, whatever it says it is', () => {
    for (const isComposing of [true, false]) {
      const { editor, lowered } = surfaceFor('a');
      editor.selection = new CodeRange(new CodePosition(0, 1));

      fire(lowered, 'compositionupdate', { data: 'か' });
      fire(lowered, 'beforeinput', { inputType: 'insertText', data: 'か', isComposing });
      fire(lowered, 'compositionend', { data: 'か' });

      expect(editor.document.text).toBe('aか');
      expect(editor.composition).toBeNull();
    }
  });

  it('makes the commit an undo step of its own, joined to neither side', () => {
    const { editor, lowered } = surfaceFor('');

    type(lowered, 'a');
    fire(lowered, 'compositionupdate', { data: 'k' });
    fire(lowered, 'compositionend', { data: 'か' });
    type(lowered, 'b');

    press(lowered, 'z', { meta: true });
    expect(editor.document.text).toBe('aか');
    press(lowered, 'z', { meta: true });
    expect(editor.document.text).toBe('a');
  });

  it('takes the same text as typing when it comes on a later turn', async () => {
    const { editor, lowered } = surfaceFor('a');
    editor.selection = new CodeRange(new CodePosition(0, 1));

    fire(lowered, 'compositionupdate', { data: 'か' });
    fire(lowered, 'compositionend', { data: 'か' });
    // A person cannot type inside the task that committed: the next turn is typing again.
    await new Promise((resolve) => setTimeout(resolve, 0));
    fire(lowered, 'beforeinput', { inputType: 'insertText', data: 'か', isComposing: false });

    expect(editor.document.text).toBe('aかか');
  });

  it('starts with no composition — null, as C# starts it, never undefined', () => {
    // A nullable field with no initializer came out UNASSIGNED in the twin, which a strict tsc
    // refuses and `=== null` calls a value. It starts null now, on both sides.
    expect(new CodeEditorController('x').composition).toBeNull();
  });
});

/** The CLIPBOARD, through the browser's own copy, cut and paste events, which carry the text. */
describe('code surface clipboard', () => {
  const clip = (lowered: HtmlNode, type: 'copy' | 'cut' | 'paste', text = '') => {
    let written = '';
    fire(lowered, type, {
      clipboardData: {
        getData: () => text,
        setData: (_: string, value: string) => {
          written = value;
        },
      },
    });
    return written;
  };

  it('copies the selection, or the whole line when nothing is selected', () => {
    const { editor, lowered } = surfaceFor('one\ntwo');
    editor.selection = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 2));
    expect(clip(lowered, 'copy')).toBe('on');

    editor.selection = new CodeRange(new CodePosition(1, 1));
    expect(clip(lowered, 'copy')).toBe('two\n');
  });

  it('pastes a whole-line copy as a line of its own, above the caret', () => {
    const { editor, lowered } = surfaceFor('one\ntwo');
    editor.selection = new CodeRange(new CodePosition(0, 1));
    const line = clip(lowered, 'copy');

    editor.selection = new CodeRange(new CodePosition(1, 2));
    clip(lowered, 'paste', line);

    expect(editor.document.lines).toEqual(['one', 'one', 'two']);
  });

  it('leaves the copy keys to those events, instead of cancelling the event that carries the text', () => {
    const { lowered } = surfaceFor('one');
    expect(press(lowered, 'c', { meta: true })).toBe(false);
    expect(press(lowered, 'v', { meta: true })).toBe(false);
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
    // The press gives the keyboard to the surface's input.
    querySelector: () => ({ focus: () => {} }),
  };
  const event = {
    clientX: x,
    clientY: y,
    currentTarget: target,
    preventDefault: () => {},
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

/**
 * The MODEL's rules, as the browser runs them: the engine the page carries is the C# one
 * transpiled, and these ask it what CodeViewModelTests and CodeEditorControllerTests ask natively.
 */
describe('the model, as the browser runs it', () => {
  const at = (text: string, line: number, column: number) => {
    const editor = new CodeEditorController(text, CodeLanguages.for('csharp'));
    editor.grid = new CodeGrid(new Point(0, 0), new Size(8, 18));
    editor.selection = new CodeRange(new CodePosition(line, column));
    return editor;
  };

  it('places a caret after a tab at its stop, and after a wide character two cells on', () => {
    expect(at('\tx', 0, 1).caretRect(new CodePosition(0, 1)).x).toBe(32);
    expect(at('\u4E2Dx', 0, 1).caretRect(new CodePosition(0, 1)).x).toBe(16);
  });

  it('steps over a whole emoji and deletes it whole', () => {
    const editor = at('a\u{1F600}b', 0, 1);

    editor.move('character', 'forward', false);
    expect(editor.caret.column).toBe(3);
    editor.deleteBackward();
    expect(editor.document.text).toBe('ab');
  });

  it('types over a selection as one undo step', () => {
    const editor = at('var name = 1;', 0, 0);
    editor.selection = new CodeRange(new CodePosition(0, 4), new CodePosition(0, 8));

    editor.type('i');
    editor.type('d');
    editor.undo();

    expect(editor.document.text).toBe('var name = 1;');
  });

  it('keeps the selection a Tab indents, each end with its line', () => {
    const editor = at('one\ntwo', 0, 0);
    editor.selection = new CodeRange(new CodePosition(1, 2), new CodePosition(0, 1));

    editor.indent();

    expect(editor.selection.anchor.line).toBe(1);
    expect(editor.selection.anchor.column).toBe(6);
    expect(editor.selection.focus.line).toBe(0);
    expect(editor.selection.focus.column).toBe(5);
  });

  it('steps a closing brace back to its block', () => {
    const editor = at('if (x) {\n        ', 1, 8);

    editor.type('}');

    expect(editor.document.line(1)).toBe('    }');
  });

  it('colours a raw string as one string, across lines', () => {
    const tokens: unknown[] = [];
    const state = CodeLanguages.cSharp.tokenize('var t = """', 0, tokens as never);
    expect(state).not.toBe(0);

    const next: unknown[] = [];
    CodeLanguages.cSharp.tokenize('    if (x) { }', state, next as never);
    expect((next as { kind: string }[]).every((t) => t.kind === 'string')).toBe(true);
  });
});

describe('the tokenizers, running in the browser', () => {
  // The registry leaned on a case-insensitive comparer the twin never had: a plain object keys
  // exactly, so 'CSharp' coloured C# natively and plain text here. CodeLanguagesTests asks the C#
  // side the same questions.
  it('are found by name in any case, as the native registry finds them', () => {
    expect(CodeLanguages.for('CSharp')).toBe(CodeLanguages.cSharp);
    expect(CodeLanguages.for('C#')).toBe(CodeLanguages.cSharp);
    expect(CodeLanguages.for('.CS')).toBe(CodeLanguages.cSharp);
    expect(CodeLanguages.for('NoSuchLanguage')).toBe(CodeLanguages.plainText);
  });

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
 * The block draws the CELLS the engine counts (defect 12 of docs/CODE-EDITOR-PLAN.md): a tab as the
 * spaces up to its stop, where the browser drew it to the next eight-column stop and the caret stood
 * one cell in, and a wide character in a box two cells wide, so a fallback font cannot move the
 * rest of the line off the grid the caret is placed on.
 */
describe('the block draws the cells the engine counts', () => {
  it('draws a tab to its stop and a wide character across two cells', async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeBlock } = await import('./components/CodeBlock');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      density: 'comfortable',
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const block = new CodeBlock('a\tb\u4E2Dc', 'csharp');
    const drawn: string[] = [];
    const visit = (node: unknown): void => {
      if (!node || typeof node !== 'object') return;
      const n = node as Record<string, unknown> & { build?: (c: unknown) => unknown };
      if (n.nodeKind === 'text') {
        drawn.push(String(n.content));
        return;
      }
      // A size crosses as its number (SizeValue passes a number through) or as { value }.
      const size = (n.style as { width?: number | { value?: number } } | undefined)?.width;
      const width = typeof size === 'number' ? size : size?.value;
      if (n.nodeKind === 'box' && typeof width === 'number' && n.child && (n.child as { nodeKind?: string }).nodeKind === 'text')
        drawn.push(`[${width}]`);
      if (typeof n.build === 'function' && n.nodeKind === 'component') visit(n.build(context));
      if (n.child) visit(n.child);
      if (Array.isArray(n.children)) for (const c of n.children) visit(c);
    };
    visit(block.build(context as never));

    const line = drawn.join('|');
    // The tab begins after `a`, at cell 1, and runs to the stop at 4: three spaces.
    expect(line).toContain('a|   |b');
    // The ideograph sits in a box two cells (2 x 7) wide, and the text around it is not moved.
    expect(line).toContain('[14]|\u4E2D|c');
    setPhotonTheme(photonTheme);
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
  const surfaceWith = (caretColor?: unknown) => {
    const editor = new CodeEditorController('let x = 1;\nlet y = 2;', CodeLanguages.for('csharp'));
    editor.grid = new CodeGrid(new Point(12, 12), new Size(COLUMN, LINE));
    editor.selectAll();
    return lowerVisualNode(
      new CodeSurface(new Text('', 'labelSmall'), editor, {
        caretColor,
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

  // A code block that carries a decoration draws it on a Stack whose layers take a z-index, and
  // those climbed over the marks painted after the block: the caret vanished at the end of every
  // line ending in `)`, `{` or `}`, because bracket matching decorates the pair there (defect 18).
  // Kept in a stacking context of its own, the code paints entirely under the marks that follow it.
  it('keeps the code in a stacking context of its own, under the marks', () => {
    const surface = surfaceWith();
    const code = surface.children[0];

    expect(effectiveStyle(code)).toContain('isolation: isolate');
    expect(surface.children.findIndex((c) => c.attributes['class'] === 'eq-code-caret')).toBeGreaterThan(0);
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

});

/**
 * The SELECTION is the component's to draw, in the code's own mark layer: the active line's wash,
 * then the matches, then the band, all under the text. Painted by the realizer on top of everything,
 * the band sat under whatever layer the block raised and vanished with the caret beside a bracket,
 * and on Photon the active line's opaque wash covered the band on the line you were on.
 */
describe('the component draws the selection, under the text', () => {
  // Down through the NODES and into every component on the way, built the way the renderer builds
  // it: the editor hands its block over as a component, and the marks live in the block's tree.
  const walk = (node: unknown, context: unknown, visit: (n: Record<string, unknown>) => void): void => {
    if (!node || typeof node !== 'object') return;
    const n = node as Record<string, unknown> & { build?: (c: unknown) => unknown };
    visit(n);
    if (typeof n.build === 'function' && !('nodeKind' in n && n.nodeKind !== 'component'))
      walk(n.build(context), context, visit);
    if (n.child) walk(n.child, context, visit);
    if (Array.isArray(n.children)) for (const c of n.children) walk(c, context, visit);
  };

  it('washes each band to 28% (CodeBlock.SelectionAlpha), one per line of the selection', async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const component = new CodeEditor('one\ntwo\nthree', 'csharp');
    const context = {
      theme,
      density: 'comfortable',
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    component.build(context as never);
    component.editor.selection = new CodeRange(new CodePosition(0, 1), new CodePosition(2, 2));
    const tree = component.build(context as never);

    // 255 × 0.28 = 71.4 → 71: the band only has to be SEEN, the text is drawn over it.
    const bands: unknown[] = [];
    walk(tree, context, (n) => {
      const style = n.style as { background?: { light?: { a?: number } } } | undefined;
      if (style?.background?.light?.a === 71) bands.push(n);
    });
    expect(bands).toHaveLength(3);
    setPhotonTheme(photonTheme);
  });

  // The test above builds twice, and the first build was a workaround: the block read the bands
  // before the build handed the engine its grid, so a selection made before the first build was
  // drawn on the default grid (at 0,0, off the code by its padding). Found in review.
  it('draws the bands on the grid its own build measured, from the first build on', async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const component = new CodeEditor('one two', 'csharp');
    component.editor.selection = new CodeRange(new CodePosition(0, 4), new CodePosition(0, 7));
    const context = {
      theme,
      density: 'comfortable',
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };

    const tree = component.build(context as never);

    let drawn: { x: number }[] = [];
    walk(tree, context, (n) => {
      if (Array.isArray(n.selectionBands) && n.selectionBands.length > 0)
        drawn = n.selectionBands as { x: number }[];
    });
    const grid = component.editor.grid;
    expect(drawn).toHaveLength(1);
    expect(drawn[0].x).toBe(grid.origin.x + 4 * grid.cell.width);
    setPhotonTheme(photonTheme);
  });
});

describe('the code surface goes through the atomizer, like every other node', () => {
  // It carried a literal `style` string, under a comment reasoning that there was no C# twin to
  // agree with, since the web realizer had no CodeSurface arm. It has one now (the code editor's SSR
  // slice), and a client string beside a server class would be exactly the hydration mismatch the
  // atomizer exists to prevent. Going through the shared atomizer costs nothing and removes the trap.
  it('emits classes and no inline style', () => {
    const { lowered } = surfaceFor('let x = 1;');

    expect(lowered.attributes['style']).toBeUndefined();
    expect(lowered.attributes['class'] ?? '').toContain('eq-code-surface');
  });

  // A drag extends the MODEL's selection. Left on, the browser swept its own highlight over the
  // same text, which painted a second selection over the band and diverged from it.
  it('keeps the browser from selecting the text a drag is already selecting', () => {
    const { lowered } = surfaceFor('let x = 1;');

    expect(effectiveStyle(lowered)).toContain('user-select: none');
  });

  // With the text no longer the browser's to select, the pointer it shows over text went with it
  // and stayed an arrow. The surface says beam, as a field does (found by hand in the dashboard).
  it('shows the beam over the code, as over any field', () => {
    const { lowered } = surfaceFor('let x = 1;');

    expect(effectiveStyle(lowered)).toContain('cursor: text');
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

/**
 * The app asks for the keyboard through the MODEL (C# twin: PhotonHost.AdoptFocusRequests): an IDE
 * after a file opens, the editor's own find bar as it closes. Remembered per model and counted from
 * 0, so a request made before the surface was first drawn is honoured when it is, and one already
 * honoured is not honoured again when the surface is drawn once more.
 */
describe('the app asks for the keyboard', () => {
  const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
  const lower = (editor: CodeEditorController) =>
    lowerVisualNode(new CodeSurface(new Text('', 'labelSmall'), editor) as never, {
      textPrimary: photonTheme.textPrimary,
      componentContext: { theme: photonTheme, typeScale: 1 },
    });

  it('gives the surface the keyboard after the render, once per request', async () => {
    const editor = new CodeEditorController('x', CodeLanguages.for('csharp'));
    editor.requestFocus();
    const parent = document.createElement('div');
    document.body.appendChild(parent);
    const reconciler = new Reconciler();
    const first = lower(editor);
    reconciler.reconcile(parent, null, first);

    await wait(80);
    expect(document.activeElement?.tagName).toBe('TEXTAREA');

    (document.activeElement as HTMLElement).blur();
    reconciler.reconcile(parent, first, lower(editor), 0);
    await wait(80);
    expect(document.activeElement?.tagName).not.toBe('TEXTAREA');
    parent.remove();
  });

  it('gives it once, and never takes it back from a control focused since', () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'requestAnimationFrame'] });
    const parent = document.createElement('div');
    const other = document.createElement('input');
    try {
      const editor = new CodeEditorController('x', CodeLanguages.for('csharp'));
      editor.requestFocus();
      document.body.append(parent, other);
      new Reconciler().reconcile(parent, null, lower(editor));

      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextFrame();
      expect(document.activeElement?.tagName).toBe('TEXTAREA');

      // The user, or the page, moves on before the timeout that backs the frames up has run.
      other.focus();
      vi.advanceTimersByTime(100);
      expect(document.activeElement).toBe(other);
    } finally {
      vi.useRealTimers();
      parent.remove();
      other.remove();
    }
  });
});

/**
 * Opening find leaves the code where it was in the tree (C# twin:
 * CodeEditorComponentTests.OpeningFindLeavesTheCodeWhereItWas). It wrapped the code in a layer it
 * did not have before, and a surface that moves is a new one: the scroll went back to the top, and
 * "next" revealed nothing, the first sight of a surface being where it opened.
 */
describe('the find bar', () => {
  it('opens over the code without moving it, and Escape is live while it is open', async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const pathOf = (node: HtmlNode): string | undefined =>
      node.attributes['data-eq-code'] ?? node.children.map(pathOf).find((path) => path !== undefined);
    const editor = new CodeEditor('var needle = 1;', 'csharp');

    resetShortcuts();
    const before = pathOf(lowerVisualNode(editor.build(context as never) as never, context as never));
    commitShortcuts();
    activeShortcuts().find((binding) => binding.chord === 'command+f')!.handler();
    const after = pathOf(lowerVisualNode(editor.build(context as never) as never, context as never));
    commitShortcuts();

    expect(after).toBe(before);
    expect(activeShortcuts().some((binding) => binding.chord === 'escape')).toBe(true);
    resetShortcuts();
  });
});
