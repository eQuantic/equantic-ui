import { describe, it, expect, vi, afterEach } from 'vitest';
import { CancellationTokenSource } from '../../utils/cancellation';
import { PickedFile } from '../picked-file';
import { FileFilter } from '../primitive-values';
import { WebFilePicker, acceptOf } from './file-picker';

/**
 * The browser's `IFilePicker`. What matters is the CONTRACT a page written once relies on: the files
 * the person chose, as PickedFiles that read their own content; null or an empty list when nothing was
 * chosen; and an input that offers what the filters name.
 */
describe('WebFilePicker', () => {
  afterEach(() => vi.restoreAllMocks());

  /** Stands in for the person: the input is clicked, and this decides what came back. */
  function whenThePersonPicks(files: File[] | 'cancel') {
    const inputs: HTMLInputElement[] = [];
    const original = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const element = original(tag) as HTMLInputElement;
      if (tag !== 'input') return element;
      inputs.push(element);
      element.click = () => {
        if (files === 'cancel') {
          element.dispatchEvent(new Event('cancel'));
          return;
        }
        Object.defineProperty(element, 'files', { value: files, configurable: true });
        element.dispatchEvent(new Event('change'));
      };
      return element;
    });
    return inputs;
  }

  const stl = (name: string, bytes: number[]) => new File([new Uint8Array(bytes)], name, { type: 'model/stl' });

  it('hands back the files the person chose, reading their own bytes', async () => {
    whenThePersonPicks([stl('bracket.stl', [1, 2, 3]), stl('lid.stl', [4, 5])]);

    const picked = await new WebFilePicker().pickFilesAsync();

    expect(picked.map((file) => file.name)).toEqual(['bracket.stl', 'lid.stl']);
    expect(picked[0]).toBeInstanceOf(PickedFile);
    expect(picked[0].mimeType).toBe('model/stl');
    expect(picked[0].size).toBe(3);
    expect(Array.from(await picked[1].readAllBytesAsync())).toEqual([4, 5]);
  });

  it('reads a text file as UTF-8', async () => {
    whenThePersonPicks([new File(['name;città\n'], 'list.csv', { type: 'text/csv' })]);

    const picked = await new WebFilePicker().pickFileAsync();

    expect(await picked!.readAllTextAsync()).toBe('name;città\n');
  });

  it('names a file the browser could not type as plain bytes', async () => {
    whenThePersonPicks([new File([new Uint8Array([7])], 'part.step')]);

    const picked = await new WebFilePicker().pickFileAsync();

    expect(picked!.mimeType).toBe('application/octet-stream');
  });

  it('answers null when the person cancels one file', async () => {
    whenThePersonPicks('cancel');

    expect(await new WebFilePicker().pickFileAsync()).toBeNull();
  });

  it('answers an empty list, never null, when the person cancels several', async () => {
    whenThePersonPicks('cancel');

    expect(await new WebFilePicker().pickFilesAsync()).toEqual([]);
  });

  it('opens one input for one file and a multiple one for several, and removes both', async () => {
    const inputs = whenThePersonPicks([stl('a.stl', [1])]);

    await new WebFilePicker().pickFileAsync();
    await new WebFilePicker().pickFilesAsync();

    expect(inputs.map((input) => input.multiple)).toEqual([false, true]);
    expect(inputs.every((input) => !input.isConnected)).toBe(true);
  });

  it('offers what the filters name', async () => {
    const inputs = whenThePersonPicks([stl('a.stl', [1])]);

    await new WebFilePicker().pickFilesAsync([new FileFilter('3D models', 'stl', 'OBJ', '.3mf')]);

    expect(inputs[0].accept).toBe('.stl,.obj,.3mf');
  });

  it('stops waiting when the page cancels, and the late answer changes nothing', async () => {
    const source = new CancellationTokenSource();
    const original = document.createElement.bind(document);
    let input: HTMLInputElement | null = null;
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const element = original(tag) as HTMLInputElement;
      if (tag === 'input') {
        input = element;
        element.click = () => {};
      }
      return element;
    });

    const asking = new WebFilePicker().pickFilesAsync(null, source.token);
    source.cancel();

    expect(await asking).toEqual([]);
    expect(input!.isConnected).toBe(false);
  });

  it('asks nothing outside a gesture, where the browser would open nothing and say nothing', async () => {
    const inputs = whenThePersonPicks([stl('a.stl', [1])]);
    Object.defineProperty(navigator, 'userActivation', { value: { isActive: false }, configurable: true });
    try {
      expect(await new WebFilePicker().pickFilesAsync()).toEqual([]);
      expect(inputs).toEqual([]);
    } finally {
      delete (navigator as { userActivation?: unknown }).userActivation;
    }
  });
});

describe('acceptOf', () => {
  it('is every extension of every filter, dotted once and lower-cased', () => {
    expect(acceptOf([new FileFilter('Images', 'png', 'JPG'), new FileFilter('Models', '.stl')])).toBe('.png,.jpg,.stl');
  });

  it('is anything for no filter, and for a filter that names no extension', () => {
    expect(acceptOf(null)).toBeUndefined();
    expect(acceptOf([])).toBeUndefined();
    expect(acceptOf([new FileFilter('Images', 'png'), new FileFilter('Anything')])).toBeUndefined();
  });
});
