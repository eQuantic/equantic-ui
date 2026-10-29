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

import { afterEach, describe, expect, it, vi } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { setPhotonTheme } from './photon-context';
import { effectiveStyle } from './style-atomizer';
import { CodeSurface, Positioned, Text, type VisualNode } from './vocabulary';
import { Point, Size, SizeValue } from './value-types';
import { CodeEditorController } from './components/CodeEditorController';
import { CodeGrid } from './components/CodeGrid';
import { CodePosition } from './components/CodePosition';
import { CodeRange } from './components/CodeRange';
import { CodeLanguages } from './components/CodeLanguages';
import { CodeDocument } from './components/CodeDocument';
import { CodeLocation } from './components/CodeLocation';
import type { HtmlNode } from '../core/types';
import { Reconciler } from '../dom/reconciler';
import { activeShortcuts, commitShortcuts, resetShortcuts } from '../dom/shortcuts';
import { commitFocusTraps, resetFocusTraps } from '../dom/focus-trap';

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
  it('moves a line on ⌥↑ through its keydown, and ⌘Z takes the move back in one step (#1031)', () => {
    const { editor, lowered } = surfaceFor('one\ntwo\nthree');
    editor.selection = new CodeRange(new CodePosition(1, 1));
    const heard: string[] = [];
    const listeners = editor.changed;
    editor.changed = (change) => {
      listeners?.(change);
      heard.push(change.edits.map((edit) => `${edit.removedText}>${edit.insertedText}`).join(', '));
    };

    expect(press(lowered, 'ArrowUp', { alt: true }), 'the editor claims ⌥↑').toBe(true);
    expect(editor.document.text).toBe('two\none\nthree');
    expect(press(lowered, 'z', { meta: true })).toBe(true);

    expect(editor.document.text, 'one undo takes the whole move back').toBe('one\ntwo\nthree');
    expect([editor.caret.line, editor.caret.column]).toEqual([1, 1]);
    expect(heard, 'the move and its undo, one change of two replacements each').toEqual(['>one\n, one\n>', '>one\n, one\n>']);
  });

  it('adds the next occurrence on ⌘D, types at both carets, and draws a caret at each (#1012)', () => {
    const { editor, lowered } = surfaceFor('count = count + counter;');
    editor.selection = new CodeRange(new CodePosition(0, 2));

    expect(press(lowered, 'd', { meta: true }), 'the editor claims ⌘D').toBe(true);
    expect(press(lowered, 'd', { meta: true })).toBe(true);
    expect(editor.selections.length, 'two whole occurrences, counter left out').toBe(2);
    type(lowered, 'n');

    expect(editor.document.text).toBe('n = n + counter;');
    const node = lowerVisualNode(new CodeSurface(new Text('', 'labelSmall'), editor) as never, {
      textPrimary: photonTheme.textPrimary,
      componentContext: { theme: photonTheme, typeScale: 1 },
    });
    expect(node.children.filter((c) => c.attributes['class'] === 'eq-code-caret').length).toBe(2);
  });

  it('keeps its input when the carets change in number, so the keyboard stays in the code (#1012)', () => {
    const editor = new CodeEditorController('count = count + count;\nx', CodeLanguages.for('csharp'));
    editor.grid = new CodeGrid(new Point(12, 12), new Size(COLUMN, LINE));
    editor.selection = new CodeRange(new CodePosition(0, 2));
    const lower = (notes: VisualNode[]) =>
      lowerVisualNode(new CodeSurface(new Text('', 'labelSmall'), editor, { notes }) as never, {
        textPrimary: photonTheme.textPrimary,
        componentContext: { theme: photonTheme, typeScale: 1 },
      });
    const parent = document.createElement('div');
    document.body.appendChild(parent);
    const reconciler = new Reconciler();
    const first = lower([]);
    reconciler.reconcile(parent, null, first);
    const input = parent.querySelector('textarea')!;
    input.focus();

    // Two more carets, and a note over the code at the same time, as a list opening as they type.
    editor.addNextOccurrence();
    editor.addNextOccurrence();
    editor.addNextOccurrence();
    reconciler.reconcile(parent, first, lower([new Positioned(new Text('a note', 'labelSmall'))]), 0);

    expect(parent.querySelectorAll('.eq-code-caret').length).toBe(3);
    expect(parent.querySelector('textarea'), 'the same input, never one made anew').toBe(input);
    expect(document.activeElement, 'the keyboard is still in the code').toBe(input);
    parent.remove();
  });

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

  it('cuts only what reached the clipboard: a write that fails deletes nothing', () => {
    const { editor, lowered } = surfaceFor('one\ntwo');
    editor.selection = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 2));

    expect(() =>
      fire(lowered, 'cut', {
        clipboardData: {
          setData: () => {
            throw new Error('the clipboard refused the write');
          },
        },
      }),
    ).toThrow();

    expect(editor.document.lines).toEqual(['one', 'two']);
  });

  it('cuts only what reached the clipboard: an event that carries none deletes nothing', () => {
    const { editor, lowered } = surfaceFor('one\ntwo');
    editor.selection = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 2));

    fire(lowered, 'cut', {});
    expect(editor.document.lines).toEqual(['one', 'two']);

    expect(clip(lowered, 'cut')).toBe('on');
    expect(editor.document.lines).toEqual(['e', 'two']);
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
  type: 'pointerdown' | 'pointermove' | 'pointerup' | 'pointerleave' | 'mousedown',
  x: number,
  y: number,
  options: { detail?: number; shift?: boolean; buttons?: number; pointerType?: string } = {},
) {
  const target = {
    getBoundingClientRect: () => ({ left: 0, top: 0, width: 4096, height: 4096 }),
    // The pointer's look, which a hover sets from the model's answer (#1084).
    style: {} as Record<string, string>,
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

describe('the link ⌘ shows over a name (C# twin: PhotonHost, #1084)', () => {
  /** Two lines, `total` and `count`, each a name a provider says is declared where it stands. */
  function linked() {
    const surface = surfaceFor('total = 1;\ncount = 2;');
    surface.editor.definitions.providers.push({
      definitionsAsync: async () => [new CodeLocation(new CodeRange(new CodePosition(0, 0), new CodePosition(0, 5)))],
    });
    return surface;
  }

  /** The provider's answer, which arrives on a later turn, as on the web. */
  const answered = () => new Promise((resolve) => setTimeout(resolve, 0));

  /** The surface's element under a pointer: its box, which a scroll moves, and the look the pointer takes. */
  function element() {
    return { box: { left: 0, top: 0, width: 4096, height: 4096 }, getBoundingClientRect() { return this.box; }, style: {} as Record<string, string> };
  }

  /** A pointer passing over the surface with nothing pressed, ⌘ held or not. */
  function hover(lowered: HtmlNode, target: ReturnType<typeof element>, at: [number, number], meta: boolean) {
    const event = { clientX: at[0], clientY: at[1], currentTarget: target, buttons: 0, pointerType: 'mouse',
      shiftKey: false, altKey: false, metaKey: meta, ctrlKey: false };
    (lowered.events['pointermove'] as unknown as (e: unknown) => void)(event);
  }

  /** A modifier key pressed or released, wherever the keyboard is: the page hears it. */
  function modifier(type: 'keydown' | 'keyup', meta: boolean) {
    document.dispatchEvent(new KeyboardEvent(type, { key: 'Meta', metaKey: meta, ctrlKey: false }));
  }

  const total = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 5));
  const count = new CodeRange(new CodePosition(1, 0), new CodePosition(1, 5));

  it('underlines the name ⌘ rests on, and the pointer is a hand over it', async () => {
    const { editor, lowered } = linked();
    const target = element();
    hover(lowered, target, cell(0, 2), true);
    await answered();

    expect(editor.definitions.link).toEqual(total);
    hover(lowered, target, cell(0, 3), true);
    expect(target.style.cursor).toBe('pointer');
    hover(lowered, target, cell(0, 8), true);
    expect(target.style.cursor).toBe('text');
  });

  it('takes the link away as ⌘ is released with the pointer at rest, and shows it again as ⌘ is pressed, wherever the keyboard is', async () => {
    const { editor, lowered } = linked();
    hover(lowered, element(), cell(0, 2), true);
    await answered();
    expect(editor.definitions.link).toEqual(total);

    modifier('keyup', false);
    expect(editor.definitions.link).toBeNull();

    modifier('keydown', true);
    await answered();
    expect(editor.definitions.link).toEqual(total);
  });

  it('links the name the code scrolled under a pointer at rest, not the one it rested on', async () => {
    const { editor, lowered } = linked();
    const target = element();
    hover(lowered, target, cell(0, 2), false);
    // The code scrolls a line up under the pointer, which stays where it was on the page.
    target.box = { left: 0, top: -LINE, width: 4096, height: 4096 };

    modifier('keydown', true);
    await answered();

    expect(editor.definitions.link).toEqual(count);
  });

  it('rests where a click released it: ⌘ pressed next, with no move, links the name there', async () => {
    const { editor, lowered } = linked();
    pressAt(lowered, cell(1, 2));
    pointer(lowered, 'pointerup', ...cell(1, 2));

    modifier('keydown', true);
    await answered();

    expect(editor.definitions.link).toEqual(count);
    modifier('keyup', false);
  });

  it('follows no link when ⌘ is pressed during a press', async () => {
    const { editor, lowered } = linked();
    hover(lowered, element(), cell(0, 2), false);
    pressAt(lowered, cell(0, 2));

    modifier('keydown', true);
    await answered();

    expect(editor.definitions.link).toBeNull();
    pointer(lowered, 'pointerup', ...cell(0, 2));
  });
});

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

  it('leaves a press on a pressable drawn inside it to the pressable, as Photon does', () => {
    const { editor, lowered, changed } = surfaceFor('one\ntwo');
    editor.selection = new CodeRange(new CodePosition(1, 2));
    let captured = false;
    const button = { tag: 'button' };
    const surfaceElement = {
      getBoundingClientRect: () => ({ left: 0, top: 0, width: 4096, height: 4096 }),
      style: {} as Record<string, string>,
      setPointerCapture: () => {
        captured = true;
      },
      querySelector: () => ({ focus: () => {} }),
      contains: (element: unknown) => element === button,
    };
    const event = {
      clientX: 20,
      clientY: 14,
      currentTarget: surfaceElement,
      target: { closest: () => button },
      preventDefault: () => {},
      pointerId: 1,
      pointerType: 'mouse',
      detail: 1,
      buttons: 1,
      shiftKey: false,
      altKey: false,
      metaKey: false,
      ctrlKey: false,
    };
    (lowered.events['pointerdown'] as unknown as (e: unknown) => void)(event);
    (lowered.events['mousedown'] as unknown as (e: unknown) => void)(event);

    expect(captured, 'the release has to reach the pressable, so the surface must not capture it').toBe(false);
    expect(editor.caret.line, 'the caret stays: the press was the pressable’s').toBe(1);
    expect(changed()).toBe(0);
  });

  it('leaves the pointer over a pressable drawn inside it to the pressable, and the code hears it left', () => {
    const editor = new CodeEditorController('one two', CodeLanguages.for('csharp'));
    editor.grid = new CodeGrid(new Point(12, 12), new Size(COLUMN, LINE));
    const phases: string[] = [];
    const model = new Proxy(editor, {
      get(target, property, receiver) {
        if (property === 'handlePointer')
          return (phase: string, ...rest: unknown[]) => {
            phases.push(phase);
            return (target.handlePointer as (...args: unknown[]) => boolean).call(target, phase, ...rest);
          };
        return Reflect.get(target, property, receiver);
      },
    });
    const lowered = lowerVisualNode(new CodeSurface(new Text('', 'labelSmall'), model) as never, {
      textPrimary: photonTheme.textPrimary,
      componentContext: { theme: photonTheme, typeScale: 1 },
    });
    const button = { tag: 'button' };
    const event = {
      clientX: 20,
      clientY: 14,
      currentTarget: {
        getBoundingClientRect: () => ({ left: 0, top: 0, width: 4096, height: 4096 }),
        style: {} as Record<string, string>,
        contains: (element: unknown) => element === button,
      },
      target: { closest: () => button },
      pointerType: 'mouse',
      buttons: 0,
      shiftKey: false,
      altKey: false,
      metaKey: false,
      ctrlKey: false,
    };

    (lowered.events['pointermove'] as unknown as (e: unknown) => void)(event);

    // Found by Copilot reviewing #1008: a move over the button was a hover of the code hidden under it.
    expect(phases).toEqual(['exit']);
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
 * A language that relies on its interface's default `Rules` keeps them in the browser (#414).
 * `PlainTextLanguage` declares none, its twin had no `rules`, and on 0.2.0-preview.58 every
 * plain-text block threw on `indentWidth` as it built, since the block reads its tab stop from the
 * language: the documentation site's `plaintext` fences each became "This section could not be
 * displayed". The C# and XML blocks, whose languages declare their own rules, were never touched.
 */
describe('a plain-text block', () => {
  it('builds and lowers with the rules its language takes from the interface', async () => {
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
    const block = new CodeBlock('a\tb', 'text');
    const node = lowerVisualNode(block.build(context as never) as never, context as never);
    expect(node.tag).toBeTruthy();
    // CodeLanguageRules.Default, which the interface's default hands to a language with no rules.
    expect(CodeLanguages.plainText.rules.indentWidth).toBe(4);
    setPhotonTheme(photonTheme);
  });

  it('edits with them too', () => {
    // The editor read the same member for its indentation, and was broken on plain text since .57.
    const editor = new CodeEditorController('x', CodeLanguages.plainText);
    expect(editor.rules.indentWidth).toBe(4);
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

  it("washes each band in the theme's Selection (the focus colour at 28%), one per line of the selection", async () => {
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

  // A theme may draw its selection opaque and name the colour selected text takes (#1038): the
  // selected glyphs take it, or they would vanish into the band; the rest keep their syntax colours.
  it("sets the selected glyphs in the theme's OnSelection, and only them", async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const magenta: [number, number, number, number] = [255, 0, 255, 255];
    const theme = materializeTheme({ ...(photonData as never as object), onSelection: [magenta, magenta] } as never);
    setPhotonTheme(theme);
    const component = new CodeEditor('one two', 'csharp');
    const context = {
      theme,
      density: 'comfortable',
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    component.build(context as never);
    component.editor.selection = new CodeRange(new CodePosition(0, 4), new CodePosition(0, 7));
    const tree = component.build(context as never);

    const inMagenta: string[] = [];
    walk(tree, context, (n) => {
      const color = n.color as { light?: { r?: number; g?: number; b?: number } } | null | undefined;
      if (n instanceof Text && color?.light?.r === 255 && color.light.g === 0 && color.light.b === 255)
        inMagenta.push((n as unknown as { content: string }).content);
    });
    expect(inMagenta.join('')).toBe('two');
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

  /** C# twins: CodeEditorFinishTests.Escape_ClosesTheFindBarOfTheEditorTheKeyboardIsIn and
   * Escape_ClosesTheBarBeforeADialogAroundTheEditor (#457). Page-wide, Escape closed the last bar
   * mounted wherever the keyboard was, and inside a dialog the dialog's Escape answered first. */
  it('closes the bar of the editor the keyboard is in, before a dialog around it', async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const { ComponentInstanceStore, enterPass, exitPass } = await import('./instance-store');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const editors = [new CodeEditor('var first = 1;', 'csharp'), new CodeEditor('var second = 2;', 'csharp')];
    const open = () => editors.map((editor) => (editor as unknown as { _findOpen: boolean })._findOpen);
    let dialogClosed = 0;
    // One tree in ONE pass, as a page renders: each editor's scope is named by its own path, and a
    // component inside an editor joins the pass instead of committing the bindings declared before it.
    const render = () => {
      enterPass(new ComponentInstanceStore(), null);
      try {
        const page = { nodeKind: 'column', children: editors.map((editor) => editor.build(context as never)) };
        const dialog = { nodeKind: 'shortcut', child: page, chord: { key: 'Escape' }, onPressed: () => dialogClosed++ };
        return lowerVisualNode(dialog as never, context as never);
      } finally {
        exitPass();
      }
    };
    const parent = document.createElement('div');
    document.body.appendChild(parent);
    const reconciler = new Reconciler();
    let mounted: HtmlNode | null = null;
    const mount = () => {
      const next = render();
      reconciler.reconcile(parent, mounted, next);
      mounted = next;
    };
    // The FIRST editor, so the bar that answers is not the last one mounted.
    const focusFirst = () =>
      [...parent.querySelectorAll('[data-eq-focus-scope]')]
        .find((scope) => scope.textContent?.includes('first') && scope.querySelector('textarea'))!
        .querySelector('textarea')!
        .focus();
    const escape = () => window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));

    try {
      resetShortcuts();
      mount();
      expect(activeShortcuts().filter((binding) => binding.chord === 'escape'), 'a closed bar binds nothing').toHaveLength(1);
      for (const find of activeShortcuts().filter((binding) => binding.chord === 'command+f')) find.handler();
      mount();
      focusFirst();

      escape();
      expect(open(), 'the bar of the editor the keyboard is in').toEqual([false, true]);
      expect(dialogClosed).toBe(0);

      mount();
      focusFirst();
      escape();
      expect(dialogClosed, 'with its bar closed, the editor leaves Escape to the dialog').toBe(1);
      expect(open()).toEqual([false, true]);
    } finally {
      parent.remove();
      resetShortcuts();
    }
  });
});

/**
 * A code editor at either end of a dialog keeps its Tab (#598). The dialog's focus trap heard Tab in
 * the capture phase, before the editor's input: with the editor last, Tab moved the focus to the
 * first control and the line was never indented, and Shift+Tab did the same with the editor first.
 * The trap now hears the key after the control that has the focus, as Photon's host asks its code
 * target before it cycles.
 */
describe('a code editor at either end of a dialog', () => {
  const lowerEditor = (editor: CodeEditorController) =>
    lowerVisualNode(new CodeSurface(new Text('', 'labelSmall'), editor) as never, {
      textPrimary: photonTheme.textPrimary,
      componentContext: { theme: photonTheme, typeScale: 1 },
    });

  // The trap is the page's: a layer left marked would pull the focus into itself in every test that
  // runs after these, the completion list's included.
  afterEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
  });

  /** A modal layer as the lowerings mark one, the editor at one end and a button at the other, with
   * the editor's input holding the focus. */
  function inDialog(code: string, editorFirst: boolean) {
    document.body.innerHTML = '';
    resetFocusTraps();
    const editor = new CodeEditorController(code, CodeLanguages.for('csharp'));
    const layer = document.createElement('div');
    layer.setAttribute('role', 'dialog');
    layer.setAttribute('aria-modal', 'true');
    layer.setAttribute('tabindex', '-1');
    layer.setAttribute('data-eq-trap', '');
    const host = document.createElement('div');
    const button = document.createElement('button');
    button.textContent = 'Close';
    if (editorFirst) layer.append(host, button);
    else layer.append(button, host);
    document.body.appendChild(layer);
    new Reconciler().reconcile(host, null, lowerEditor(editor));
    commitFocusTraps();
    const input = host.querySelector('textarea')!;
    input.focus();
    return { editor, input, button };
  }

  const key = (target: HTMLElement, name: string, shift = false) =>
    target.dispatchEvent(
      new KeyboardEvent('keydown', { key: name, shiftKey: shift, bubbles: true, cancelable: true }),
    );

  it('indents on Tab when it is the last control', () => {
    const { editor, input } = inDialog('one', false);

    key(input, 'Tab');

    expect(document.activeElement, 'the editor keeps the keyboard').toBe(input);
    expect(editor.document.text).toBe('    one');
  });

  it('outdents on Shift+Tab when it is the first control', () => {
    const { editor, input } = inDialog('    one', true);

    key(input, 'Tab', true);

    expect(document.activeElement, 'the editor keeps the keyboard').toBe(input);
    expect(editor.document.text).toBe('one');
  });

  it('lets Escape release Tab, and the trap cycles it then', () => {
    const last = inDialog('one', false);
    key(last.input, 'Escape');
    key(last.input, 'Tab');
    expect(document.activeElement, 'Tab from the last control wraps to the first').toBe(last.button);
    expect(last.editor.document.text).toBe('one');

    const first = inDialog('one', true);
    key(first.input, 'Escape');
    key(first.input, 'Tab', true);
    expect(document.activeElement, 'Shift+Tab from the first control wraps to the last').toBe(first.button);
  });
});

/** Every node under `node` that `test` accepts, in tree order. */
function nodesWhere(node: HtmlNode, test: (candidate: HtmlNode) => boolean): HtmlNode[] {
  const found: HtmlNode[] = test(node) ? [node] : [];
  for (const child of node.children) found.push(...nodesWhere(child, test));
  return found;
}

/**
 * The completion list (#297) on the web, through the transpiled CodeEditor; the C# twins are
 * CodeEditorCompletionTests, on Photon. The list is the code input's listbox, its rows are options
 * the keyboard never lands on, and a press on one accepts it while the input keeps the keyboard.
 */
describe('the completion list', () => {
  /** An editor holding `code` and nineteen empty lines under it (room for a page of rows under
   * its second line), completing from the document's words, with `typed` typed on the second line
   * as a browser types it: a `beforeinput` per character on the input. */
  async function typedInto(code: string, typed: string) {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const { CodeWordCompletionProvider } = await import('./components/CodeWordCompletionProvider');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      typeScale: 1,
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const component = new CodeEditor(code + '\n'.repeat(19), 'csharp');
    component.completions = [new CodeWordCompletionProvider()];
    const lower = () => lowerVisualNode(component.build(context as never) as never, context as never);
    const editor = component.editor;
    editor.selection = new CodeRange(new CodePosition(1, 0));
    const input = nodesWhere(lower(), (node) => node.tag === 'textarea')[0];
    for (const c of typed) {
      (input.events['beforeinput'] as unknown as (event: unknown) => void)({
        inputType: 'insertText',
        data: c,
        target: { value: '' },
        preventDefault: () => {},
      });
    }
    // The providers answer through a promise, as a language server does.
    await new Promise((resolve) => setTimeout(resolve, 0));
    return { component, editor, lower };
  }

  const listOf = (lowered: HtmlNode) => nodesWhere(lowered, (node) => node.attributes['role'] === 'listbox')[0];
  const optionsOf = (lowered: HtmlNode) => nodesWhere(lowered, (node) => node.attributes['role'] === 'option');
  const inputIn = (lowered: HtmlNode) => nodesWhere(lowered, (node) => node.tag === 'textarea')[0];

  it("is the code input's listbox, and the input points at the selected option", async () => {
    const { lower } = await typedInto('var Column = 1; var ColorToken = 2;', 'Co');

    const lowered = lower();
    const list = listOf(lowered);
    expect(list, 'a word typed opens the list').toBeDefined();
    const options = optionsOf(list);
    expect(options.map((option) => option.attributes['aria-label'])).toEqual(['ColorToken', 'Column']);
    const input = inputIn(lowered);
    expect(input.attributes['aria-autocomplete']).toBe('list');
    expect(input.attributes['aria-expanded'], 'ARIA allows it on a combobox, not on a textbox').toBeUndefined();
    expect(input.attributes['aria-controls']).toBe(list.attributes['id']);
    expect(input.attributes['aria-activedescendant']).toBe(options[0].attributes['id']);
    expect(options[0].attributes['aria-selected']).toBe('true');
    expect(options[1].attributes['aria-selected']).toBe('false');
    expect(options.every((option) => option.attributes['tabindex'] === '-1'), 'Tab never lands on a row').toBe(true);
  });

  it('follows the arrows: the input points at the option the keyboard is on', async () => {
    const { lower } = await typedInto('var Column = 1; var ColorToken = 2;', 'Co');

    let prevented = false;
    (inputIn(lower()).events['keydown'] as unknown as (event: unknown) => void)({
      key: 'ArrowDown',
      shiftKey: false,
      altKey: false,
      metaKey: false,
      ctrlKey: false,
      isComposing: false,
      keyCode: 0,
      target: { value: '' },
      preventDefault: () => {
        prevented = true;
      },
    });

    expect(prevented, 'the list claims the arrow, so the caret does not move').toBe(true);
    const lowered = lower();
    expect(inputIn(lowered).attributes['aria-activedescendant']).toBe(optionsOf(lowered)[1].attributes['id']);
  });

  it('a press on a row accepts it, and the input keeps the keyboard', async () => {
    const { editor, lower } = await typedInto('var Column = 1; var ColorToken = 2;', 'Co');
    const parent = document.createElement('div');
    document.body.appendChild(parent);
    try {
      new Reconciler().reconcile(parent, null, lower());
      const input = parent.querySelector('textarea')!;
      input.focus();
      const row = parent.querySelectorAll('[role="option"]')[1] as HTMLElement;

      const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
      row.dispatchEvent(down);
      expect(down.defaultPrevented, 'the browser would move the focus to the row').toBe(true);
      expect(document.activeElement).toBe(input);
      expect(editor.caret, 'the code under the list was not pressed').toEqual({ line: 1, column: 2 });

      row.click();

      expect(editor.document.line(1)).toBe('Column');
      expect(editor.caret).toEqual({ line: 1, column: 6 });
      expect(document.activeElement).toBe(input);
    } finally {
      parent.remove();
    }
  });

  it('a press on the list that no row takes stays the list’s, and moves no caret', async () => {
    const { editor, lower } = await typedInto('var Column = 1; var ColorToken = 2;', 'Co');

    const list = listOf(lower());
    let stopped = false;
    let prevented = false;
    const event = {
      type: 'mousedown',
      stopPropagation: () => {
        stopped = true;
      },
      preventDefault: () => {
        prevented = true;
      },
    };
    (list.events['pointerdown'] as unknown as (event: unknown) => void)({ ...event, type: 'pointerdown' });
    (list.events['mousedown'] as unknown as (event: unknown) => void)(event);

    expect(stopped, 'the surface around the list never hears it').toBe(true);
    expect(prevented, 'and the input keeps the keyboard').toBe(true);
    expect(editor.caret).toEqual({ line: 1, column: 2 });
  });
});

/**
 * A press under a bounded editor's code (#599), on the web; the C# twins are in
 * CodeEditorComponentTests, on Photon. The code's box was as tall as the file, so the room under a
 * short file was the scroll view's and a press there moved nothing.
 */
describe('a press under the code', () => {
  it('is the end of the document, wherever across the line it lands', () => {
    const { editor, lowered } = surfaceFor('one\ntwo three');

    pressAt(lowered, [12 + COLUMN, 12 + 10 * LINE]);

    expect(editor.caret).toEqual({ line: 1, column: 9 });
  });

  /**
   * The code fills its viewport by LAYOUT (#1143): both scroll views fill their viewports and the row of
   * gutter and code stretches, so the code is as tall as the sideways scroll view's content box, which
   * leaves its scrollbar out. A number reported from layout made the code the vertical viewport's height,
   * and where a scrollbar takes space the sideways one stood under that, so a short file scrolled by it.
   */
  it("lands in the code, since a bounded editor's code fills its viewport", async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      typeScale: 1,
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const component = new CodeEditor('one\ntwo three', 'csharp');
    component.height = SizeValue.fill;
    component.build(context as never);
    // What the vertical scroll view reports once it is laid out: the pane is 400 tall.
    (component as unknown as { _viewport: number })._viewport = 400;

    const lowered = lowerVisualNode(component.build(context as never) as never, context as never);

    expect(
      nodesWhere(lowered, (node) => effectiveStyle(node).includes('min-height: 400px')),
      'no height reported from layout reaches the code',
    ).toHaveLength(0);
    const [vertical] = nodesWhere(lowered, (node) => effectiveStyle(node).includes('overflow-y: auto'));
    expect(effectiveStyle(vertical)).toContain('display: flex');
    expect(effectiveStyle(vertical)).toContain('flex-direction: column');
    const row = effectiveStyle(vertical.children[0]);
    expect(row, 'the row grows to the viewport and never shrinks under its code').toContain('flex-grow: 1');
    expect(row).toContain('flex-shrink: 0');
    expect(row, 'and stretches the gutter and the sideways scroll view to it').toContain('align-items: stretch');
    const [sideways] = nodesWhere(lowered, (node) => effectiveStyle(node).includes('overflow-x: auto'));
    expect(effectiveStyle(sideways)).toContain('display: flex');
    expect(effectiveStyle(sideways)).toContain('flex-direction: row');
    const surface = sideways.children[0];
    expect(surface.attributes['class']).toContain('eq-code-surface');
    expect(effectiveStyle(surface), 'the code fills the sideways viewport').toContain('flex-grow: 1');
    expect(effectiveStyle(surface)).toContain('flex-shrink: 0');
  });

  it('has the row of gutter and code with no gutter too, so the code fills its viewport the same way', async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      typeScale: 1,
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const component = new CodeEditor('one\ntwo three', 'csharp');
    component.height = SizeValue.fill;
    component.showLineNumbers = false;

    const lowered = lowerVisualNode(component.build(context as never) as never, context as never);

    // A scroll view straight inside the vertical one took `height: 100%` of it: exactly the viewport, so a
    // file longer than the pane was cut at its bottom instead of scrolling.
    const [vertical] = nodesWhere(lowered, (node) => effectiveStyle(node).includes('overflow-y: auto'));
    expect(effectiveStyle(vertical.children[0])).toContain('display: flex');
    expect(effectiveStyle(vertical.children[0])).toContain('flex-direction: row');
    expect(effectiveStyle(vertical.children[0])).not.toContain('overflow-x: auto');
  });
});

/**
 * The problems and the card that explains the code (#299, #1005) on the web, through the transpiled
 * CodeEditor and its engine; the C# twins are CodeEditorProblemsTests, on Photon. A problem is a wave
 * under its range and a mark in the gutter; the pointer resting on a word reaches the model through
 * the surface's pointermove, and the card comes back as a note over the code, which takes the pointer.
 */
describe('the problems and the card', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  /** The editor holding twenty lines, `var nameL = L;` on line L, as a person's browser builds it. */
  async function editorWith(setup: (component: Record<string, unknown>) => void) {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      typeScale: 1,
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const code = Array.from({ length: 20 }, (_, line) => `var name${line} = ${line};`).join('\n');
    const component = new CodeEditor(code, 'csharp');
    setup(component as unknown as Record<string, unknown>);
    const lower = () => lowerVisualNode(component.build(context as never) as never, context as never);
    // The first build measures the grid every later one places the code on.
    lower();
    return { editor: component.editor, lower };
  }

  const problem = async (line: number, from: number, to: number, message: string, severity = 'error') => {
    const { CodeDiagnostic } = await import('./components/CodeDiagnostic');
    const found = new CodeDiagnostic(
      new CodeRange(new CodePosition(line, from), new CodePosition(line, to)),
      message,
      severity,
    );
    found.code = 'CS0103';
    found.source = 'csharp';
    return found;
  };

  /** Explains every word it is asked about, at once. */
  const explainer = async () => {
    const { CodeHover } = await import('./components/CodeHover');
    return {
      hoverAsync: (document: CodeDocument, position: CodePosition) => {
        const word = document.wordAt(position);
        const answer = new CodeHover(word, `About ${document.textIn(word)}.`);
        answer.signature = `int ${document.textIn(word)}`;
        return Promise.resolve(answer);
      },
    };
  };

  const surfaceIn = (lowered: HtmlNode) =>
    nodesWhere(lowered, (node) => node.attributes['data-eq-code'] !== undefined)[0];
  const notesIn = (lowered: HtmlNode) =>
    nodesWhere(lowered, (node) => (node.attributes['class'] ?? '').split(' ').includes('eq-code-note'));
  const waves = (lowered: HtmlNode) =>
    nodesWhere(
      lowered,
      (node) => node.tag === 'svg' && node.children.some((path) => (path.attributes['d'] ?? '').startsWith('M0 2 L2 0 L4 2')),
    );
  const textOf = (node: HtmlNode): string =>
    (node.textContent ?? '') + node.children.map((child) => textOf(child)).join('');

  /** The pointer passing over the middle of the character at `line`, `column`, with nothing pressed. */
  function hover(lowered: HtmlNode, editor: CodeEditorController, line: number, column: number) {
    const at = editor.caretRect(new CodePosition(line, column));
    pointer(surfaceIn(lowered), 'pointermove', at.x + editor.grid.cell.width / 2, at.y + at.height / 2, { buttons: 0 });
  }

  it('draws each problem as a wave under its range, in the colour of its severity', async () => {
    const error = await problem(3, 4, 9, 'Unused.');
    const warning = await problem(5, 4, 9, 'Could be a constant.', 'warning');
    const { lower } = await editorWith((component) => {
      component.diagnostics = [error, warning];
    });

    const drawn = waves(lower());

    expect(drawn, 'one wave a problem').toHaveLength(2);
    const colour = (wave: HtmlNode) => /(?:^|; )color: ([^;]+)/.exec(effectiveStyle(wave))?.[1];
    expect(colour(drawn[0])).toBeDefined();
    expect(colour(drawn[0]), "an error's wave is not a warning's").not.toBe(colour(drawn[1]));
  });

  it("marks each line in the gutter with its worst problem, and a hint's line with none", async () => {
    const found = [
      await problem(1, 0, 3, 'An error first.'),
      await problem(1, 4, 9, 'A warning after it.', 'warning'),
      await problem(4, 4, 9, 'Only a warning here.', 'warning'),
      await problem(6, 4, 9, 'Could be a constant.', 'hint'),
    ];
    const { lower } = await editorWith((component) => {
      component.diagnostics = found;
    });

    // A marker is a 7dp dot; the declarations come sorted, so each is looked for on its own.
    const marks = nodesWhere(lower(), (node) => {
      const style = effectiveStyle(node);
      return /(^|; )width: 7px(;|$)/.test(style) && /(^|; )height: 7px(;|$)/.test(style);
    });

    expect(marks, 'lines 1 and 4: the hint marks nothing').toHaveLength(2);
    const colour = (mark: HtmlNode) => /background-color: ([^;]+)/.exec(effectiveStyle(mark))?.[1];
    // Line 4 holds a warning alone, so its colour is the warning's.
    expect(colour(marks[0]), "line 1's error outweighs its warning").not.toBe(colour(marks[1]));
  });

  it('shows the card over the code when the pointer rests on a word, and closes it when the pointer leaves', async () => {
    vi.useFakeTimers();
    const provider = await explainer();
    const { editor, lower } = await editorWith((component) => {
      component.hoverProviders = [provider];
    });

    hover(lower(), editor, 10, 6);
    expect(editor.hover.isOpen, 'a pointer passing over the code asks nothing yet').toBe(false);
    await vi.advanceTimersByTimeAsync(300);

    expect(editor.hover.isOpen, 'resting, it shows the card').toBe(true);
    const shown = lower();
    const notes = notesIn(shown);
    expect(notes, 'the card is a note over the code').toHaveLength(1);
    expect(textOf(notes[0])).toContain('int name10');
    expect(textOf(notes[0])).toContain('About name10.');
    expect(nodesWhere(notes[0], (node) => node.attributes['aria-live'] !== undefined),
      'a pointer resting asks nobody to speak').toHaveLength(0);

    pointer(surfaceIn(shown), 'pointerleave', 0, 0, { buttons: 0 });

    expect(editor.hover.isOpen).toBe(false);
    expect(notesIn(lower())).toHaveLength(0);
  });

  it('asks nothing while a pointer moves over the code with another button held', async () => {
    vi.useFakeTimers();
    const provider = await explainer();
    const { editor, lower } = await editorWith((component) => {
      component.hoverProviders = [provider];
    });
    const at = editor.caretRect(new CodePosition(10, 6));

    // The right button held: a drag of its own, which rests on nothing (found by Copilot reviewing #1008).
    pointer(surfaceIn(lower()), 'pointermove', at.x + editor.grid.cell.width / 2, at.y + at.height / 2, { buttons: 2 });
    await vi.advanceTimersByTimeAsync(300);

    expect(editor.hover.isOpen).toBe(false);
    expect(editor.selection.isEmpty, 'and it selects nothing either').toBe(true);
  });

  it('keeps the card while the pointer is on it, and a press there moves no caret', async () => {
    vi.useFakeTimers();
    const provider = await explainer();
    const { editor, lower } = await editorWith((component) => {
      component.hoverProviders = [provider];
    });
    hover(lower(), editor, 10, 6);
    await vi.advanceTimersByTimeAsync(300);
    const note = notesIn(lower())[0];

    let stopped = 0;
    let prevented = false;
    const event = (type: string) => ({
      type,
      buttons: 0,
      stopPropagation: () => {
        stopped++;
      },
      preventDefault: () => {
        prevented = true;
      },
    });
    for (const type of ['pointermove', 'pointerdown', 'mousedown'])
      (note.events[type] as unknown as (e: unknown) => void)(event(type));

    expect(stopped, 'the surface under the card hears none of them').toBe(3);
    expect(prevented, 'and the input keeps the keyboard').toBe(true);
    expect(editor.caret).toEqual({ line: 0, column: 0 });
    expect(editor.hover.isOpen).toBe(true);
  });

  it('goes to the next problem on F8, and says it in a polite live region', async () => {
    const found = await problem(8, 4, 9, "The name 'name8' is unused.");
    const { editor, lower } = await editorWith((component) => {
      component.diagnostics = [found];
    });
    const input = nodesWhere(lower(), (node) => node.tag === 'textarea')[0];
    let prevented = false;
    (input.events['keydown'] as unknown as (event: unknown) => void)({
      key: 'F8',
      shiftKey: false,
      altKey: false,
      metaKey: false,
      ctrlKey: false,
      isComposing: false,
      keyCode: 0,
      target: { value: '' },
      preventDefault: () => {
        prevented = true;
      },
    });

    expect(prevented, 'F8 is claimed while there is a problem to go to').toBe(true);
    expect(editor.caret).toEqual({ line: 8, column: 4 });
    const notes = notesIn(lower());
    expect(notes).toHaveLength(1);
    const region = nodesWhere(notes[0], (node) => node.attributes['aria-live'] === 'polite');
    expect(region, 'a card a key brought up is read out').toHaveLength(1);
    expect(textOf(region[0])).toContain("The name 'name8' is unused.  csharp CS0103");
  });

  /** The white-space the browser applies to the first node `target` accepts: its own, or the nearest
   * ancestor's that declares one. */
  const appliedWhiteSpace = (root: HtmlNode, target: (node: HtmlNode) => boolean): string | undefined => {
    const walk = (node: HtmlNode, chain: HtmlNode[]): HtmlNode[] | null => {
      const here = [...chain, node];
      if (target(node)) return here;
      for (const child of node.children) {
        const found = walk(child, here);
        if (found) return found;
      }
      return null;
    };
    for (const node of (walk(root, []) ?? []).reverse()) {
      const declared = /(?:^|; )white-space: ([^;]+)/.exec(effectiveStyle(node))?.[1];
      if (declared !== undefined) return declared;
    }
    return undefined;
  };

  // A card's message and the list's documentation inherited the surface's `pre`, which keeps the code's
  // token runs on their line, so a text of many lines never wrapped: on a phone the message ran past the
  // card's edge (found by Copilot reviewing #1008).
  it("wraps a note's prose and the list's, and keeps the code on its line", () => {
    const prose = 'Prefer DateTimeOffset: a DateTime carries no offset, and a ledger is read in every time zone.';
    const editor = new CodeEditorController('let x = 1;', CodeLanguages.for('csharp'));
    editor.grid = new CodeGrid(new Point(12, 12), new Size(COLUMN, LINE));
    const surface = new CodeSurface(new Text('let x = 1;', 'labelSmall'), editor, {
      notes: [new Positioned(new Text(prose, 'labelSmall', null, 4), 0, null, null, 0)],
      options: new Text(`${prose} The list's.`, 'labelSmall', null, 4),
    });
    const lowered = lowerVisualNode(surface as never, {
      textPrimary: photonTheme.textPrimary,
      componentContext: { theme: photonTheme, typeScale: 1 },
    } as never);
    const holding = (text: string) => (node: HtmlNode) => node.textContent === text;

    expect(appliedWhiteSpace(lowered, holding(prose)), "the card's message").toBe('normal');
    expect(appliedWhiteSpace(lowered, holding(`${prose} The list's.`)), "the list's documentation").toBe('normal');
    expect(appliedWhiteSpace(lowered, holding('let x = 1;')), 'the code').toBe('pre');
  });
});

/**
 * The panel that explains the call being written (#298) on the web, through the transpiled CodeEditor and
 * its engine; the C# twins are CodeEditorSignatureTests, on Photon. Typing a call through the input opens
 * it as a note over the code, a polite live region, the argument the caret is on marked in the label.
 */
describe('the panel over a call', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function editorWith(setup: (component: Record<string, unknown>) => void) {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      typeScale: 1,
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const code = Array.from({ length: 20 }, (_, line) => `var name${line} = ${line};`).join('\n');
    const component = new CodeEditor(code, 'csharp');
    component.completions = [];
    setup(component as unknown as Record<string, unknown>);
    const lower = () => lowerVisualNode(component.build(context as never) as never, context as never);
    lower();
    return { editor: component.editor, lower };
  }

  /** Column's signature, its active parameter the commas typed after the parenthesis. */
  const columns = async () => {
    const { CodeSignature } = await import('./components/CodeSignature');
    const { CodeSignatureParameter } = await import('./components/CodeSignatureParameter');
    const { CodeSignatureHelp } = await import('./components/CodeSignatureHelp');
    const signature = new CodeSignature('Column(float gap, MainAlign main)');
    const gap = new CodeSignatureParameter('float gap');
    gap.documentation = 'The space between two children.';
    signature.parameters = [gap, new CodeSignatureParameter('MainAlign main')];
    return {
      triggerCharacters: ['('],
      retriggerCharacters: [','],
      signatureHelpAsync: (document: CodeDocument, position: CodePosition) => {
        const line = document.line(position.line);
        const open = line.lastIndexOf('(', position.column - 1);
        const close = line.indexOf(')', open);
        if (open < 0 || (close >= 0 && close < position.column)) return Promise.resolve(null);
        const help = new CodeSignatureHelp([signature]);
        help.activeParameter = (line.slice(open + 1, position.column).match(/,/g) ?? []).length;
        return Promise.resolve(help);
      },
    };
  };

  const notesIn = (lowered: HtmlNode) =>
    nodesWhere(lowered, (node) => (node.attributes['class'] ?? '').split(' ').includes('eq-code-note'));
  const textOf = (node: HtmlNode): string =>
    (node.textContent ?? '') + node.children.map((child) => textOf(child)).join('');

  function typeInto(lowered: HtmlNode, text: string) {
    const input = nodesWhere(lowered, (node) => node.tag === 'textarea')[0];
    for (const c of text) {
      (input.events['beforeinput'] as unknown as (event: unknown) => void)({
        inputType: 'insertText',
        data: c,
        target: { value: '' },
        preventDefault: () => {},
      });
    }
  }

  it('opens over the code on an opening parenthesis, a polite live region', async () => {
    vi.useFakeTimers();
    const provider = await columns();
    const { editor, lower } = await editorWith((component) => {
      component.signatureProviders = [provider];
    });
    // The end of line 10, `var name10 = 10;`.
    editor.selection = new CodeRange(new CodePosition(10, 16));

    typeInto(lower(), ' Column(');
    await vi.advanceTimersByTimeAsync(0);

    expect(editor.signatureHelp.isOpen).toBe(true);
    const notes = notesIn(lower());
    expect(notes, 'the panel is a note over the code').toHaveLength(1);
    expect(textOf(notes[0])).toContain('Column(float gap, MainAlign main)');
    expect(textOf(notes[0])).toContain('float gap: The space between two children.');
    expect(nodesWhere(notes[0], (node) => node.attributes['aria-live'] === 'polite')).toHaveLength(1);
  });

  it('follows the arguments as they are typed', async () => {
    vi.useFakeTimers();
    const provider = await columns();
    const { editor, lower } = await editorWith((component) => {
      component.signatureProviders = [provider];
    });
    // The end of line 10, `var name10 = 10;`.
    editor.selection = new CodeRange(new CodePosition(10, 16));
    typeInto(lower(), ' Column(');
    await vi.advanceTimersByTimeAsync(0);

    // The digit asks again after the pause, and the comma, a retrigger character, at once.
    typeInto(lower(), '1, ');
    await vi.advanceTimersByTimeAsync(150);

    expect(editor.signatureHelp.activeParameter, 'the comma moved to the second argument').toBe(1);
    expect(textOf(notesIn(lower())[0])).toContain('MainAlign main');
  });
});

/**
 * An app's own key bindings on the transpiled CodeEditor (#1010): the twin of the C# keymap answers the
 * editor's keydown through the real lowering; the C# twins are CodeEditorKeyBindingsTests, on Photon.
 */
describe('the keys an app binds', () => {
  it("runs the app's binding from the editor's keydown, and claims the key", async () => {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const { CodeKeyBinding } = await import('./components/CodeKeyBinding');
    const { KeyChord } = await import('./vocabulary');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      typeScale: 1,
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const component = new CodeEditor('alpha beta\ngamma', 'csharp');
    (component as unknown as Record<string, unknown>).keyBindings = [
      new CodeKeyBinding(KeyChord.command('d'), 'selectAll'),
    ];
    const lower = () => lowerVisualNode(component.build(context as never) as never, context as never);
    lower();
    const input = nodesWhere(lower(), (node) => node.tag === 'textarea')[0];
    let prevented = false;

    (input.events['keydown'] as unknown as (event: unknown) => void)({
      key: 'd',
      shiftKey: false,
      altKey: false,
      metaKey: true,
      ctrlKey: false,
      isComposing: false,
      keyCode: 0,
      target: { value: '' },
      preventDefault: () => {
        prevented = true;
      },
    });

    expect(prevented, "the app's chord is the editor's").toBe(true);
    const selection = component.editor.selection;
    expect([selection.start.line, selection.start.column, selection.end.line, selection.end.column]).toEqual([0, 0, 1, 5]);
  });

  /** A transpiled CodeEditor lowered for the web, its keydown driven as the browser drives it. */
  async function surface(bindings: (KeyChord: { command(key: string): unknown }, Binding: new (chord: unknown, command: string) => unknown) => unknown[]) {
    const { materializeTheme } = await import('./theme-bridge');
    const photonData = (await import('./theme-bridge.photon.json')).default;
    const { CodeEditor } = await import('./components/CodeEditor');
    const { CodeKeyBinding } = await import('./components/CodeKeyBinding');
    const { KeyChord } = await import('./vocabulary');
    const theme = materializeTheme(photonData as never);
    setPhotonTheme(theme);
    const context = {
      theme,
      textPrimary: theme.textPrimary,
      density: 'comfortable',
      typeScale: 1,
      measureText: (text: string) => text.length * 7,
      monoAdvance: () => 7,
    };
    const component = new CodeEditor('alpha beta\ngamma', 'csharp');
    (component as unknown as Record<string, unknown>).keyBindings = bindings(KeyChord as never, CodeKeyBinding as never);
    const lower = () => lowerVisualNode(component.build(context as never) as never, context as never);
    lower();
    const input = nodesWhere(lower(), (node) => node.tag === 'textarea')[0];
    const press = (key: string) => {
      let prevented = false;
      (input.events['keydown'] as unknown as (event: unknown) => void)({
        key,
        shiftKey: false,
        altKey: false,
        metaKey: true,
        ctrlKey: false,
        isComposing: false,
        keyCode: 0,
        target: { value: '' },
        preventDefault: () => {
          prevented = true;
        },
      });
      return prevented;
    };
    return { component, input, press, lower };
  }

  /**
   * The browser's copy event, fired as `document.execCommand('copy')` fires it, inside the key's handler:
   * `copied` collects what the surface put on the clipboard. `refuse` fires none, as a browser that takes
   * no copy.
   */
  function copyEvents(input: { events: Record<string, unknown> }, refuse = false) {
    const copied: string[] = [];
    const saved = Object.getOwnPropertyDescriptor(document, 'execCommand');
    Object.defineProperty(document, 'execCommand', {
      configurable: true,
      value: (command: string) => {
        if (refuse || command !== 'copy') return false;
        (input.events['copy'] as unknown as (event: unknown) => void)({
          clipboardData: { setData: (_type: string, text: string) => copied.push(text) },
          preventDefault: () => {},
        });
        return true;
      },
    });
    const restore = () => {
      if (saved) Object.defineProperty(document, 'execCommand', saved);
      else delete (document as unknown as Record<string, unknown>).execCommand;
    };
    return { copied, restore };
  }

  /**
   * Copy and Cut write through the browser's copy event on whatever key they are bound to, and the command
   * decides, never the key: ⌘X bound to Copy copies and deletes nothing, and a Copy that completes ⌘K ⌘C
   * copies (Copilot's second round on #1027: the key chose the clipboard's path, so ⌘X ran the browser's cut).
   */
  it('copies and cuts through the copy event, as the command bound to the key says', async () => {
    const { component, input, press } = await surface((KeyChord, Binding) => [
      new Binding(KeyChord.command('d'), 'copy'),
      new Binding(KeyChord.command('x'), 'copy'),
      new Binding(KeyChord.command('e'), 'cut'),
      Object.assign(new Binding(KeyChord.command('c'), 'copy') as object, { first: KeyChord.command('k') }),
    ]);
    const { copied, restore } = copyEvents(input as never);
    try {
      component.editor.selectAll();

      expect(press('d'), 'a Copy bound to ⌘D').toBe(true);
      expect(press('x'), 'and to ⌘X, which the browser would cut on').toBe(true);
      expect(component.editor.document.text, 'the Copy on ⌘X deleted nothing').toBe('alpha beta\ngamma');
      expect(press('k')).toBe(true);
      expect(press('c'), 'the Copy that completes ⌘K ⌘C').toBe(true);
      expect(copied).toEqual(['alpha beta\ngamma', 'alpha beta\ngamma', 'alpha beta\ngamma']);
      expect(press('e'), 'a Cut bound to ⌘E').toBe(true);
      expect(copied).toHaveLength(4);
      expect(component.editor.document.text, 'the cut took the text it copied').toBe('');
    } finally {
      restore();
    }
  });

  /** A cut the browser took no copy for deletes nothing, and the key goes on to the browser. */
  it('deletes nothing when the browser takes no copy', async () => {
    const { component, input, press } = await surface((KeyChord, Binding) => [new Binding(KeyChord.command('e'), 'cut')]);
    const { copied, restore } = copyEvents(input as never, true);
    try {
      component.editor.selectAll();

      expect(press('e'), "the key is the browser's").toBe(false);
      expect(copied).toEqual([]);
      expect(component.editor.document.text).toBe('alpha beta\ngamma');
    } finally {
      restore();
    }
  });

  /**
   * A paste is the browser's paste event: ⌘V goes on to it, which brings the text, and a Paste bound to a
   * key the browser does not paste on finds nothing to read (#1028).
   */
  it("leaves ⌘V to the browser's paste event", async () => {
    const { component, input, press } = await surface((KeyChord, Binding) => [new Binding(KeyChord.command('j'), 'paste')]);
    component.editor.selectAll();

    expect(press('v'), "⌘V is the paste event's").toBe(false);
    (input.events['paste'] as unknown as (event: unknown) => void)({
      clipboardData: { getData: () => 'pasted' },
      preventDefault: () => {},
    });
    expect(component.editor.document.text).toBe('pasted');
    expect(press('j'), 'a Paste on ⌘J is claimed').toBe(true);
    expect(component.editor.document.text, 'and finds nothing to read').toBe('pasted');
  });

  /**
   * The bindings a component stops handing come out where it put them: the controller's own ⌘D, equal to
   * the component's, stays first (Copilot's second round on #1027).
   */
  it("withdraws the component's bindings and leaves the controller's in their order", async () => {
    const { component, press, lower } = await surface((KeyChord, Binding) => [new Binding(KeyChord.command('d'), 'selectAll')]);
    const { KeyChord } = await import('./vocabulary');
    const { CodeKeyBinding } = await import('./components/CodeKeyBinding');
    const editor = component.editor as unknown as { keyBindings: unknown[] };
    // The controller's own, before the component's block, as an IDE puts them.
    editor.keyBindings.splice(
      0,
      0,
      new CodeKeyBinding(KeyChord.command('d'), 'selectAll'),
      new CodeKeyBinding(KeyChord.command('d'), 'cursorDocumentEnd'),
    );
    (component as unknown as Record<string, unknown>).keyBindings = [];
    lower();

    expect(editor.keyBindings.map((binding) => (binding as { command: string }).command)).toEqual([
      'selectAll',
      'cursorDocumentEnd',
    ]);
    expect(press('d')).toBe(true);
    const selection = component.editor.selection;
    expect([selection.start.line, selection.start.column, selection.end.line, selection.end.column]).toEqual([0, 0, 1, 5]);
  });
});
