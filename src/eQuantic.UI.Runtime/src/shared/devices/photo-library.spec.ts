import { describe, it, expect, vi, afterEach } from 'vitest';
import { WebPhotoLibrary } from './photo-library';

/**
 * The browser's realization of the same contract the native shells satisfy. What matters here is
 * the CONTRACT, not the input element: a page written once must get the same answers — null when
 * nothing was chosen, a limit that is honoured, bytes that are the file's own.
 */
describe('WebPhotoLibrary', () => {
  afterEach(() => vi.restoreAllMocks());

  /** Stands in for the user: the input is clicked, and this decides what came back. */
  function whenTheUserPicks(files: File[]) {
    const original = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const element = original(tag) as HTMLInputElement;
      if (tag !== 'input') return element;
      element.click = () => {
        Object.defineProperty(element, 'files', { value: files, configurable: true });
        element.dispatchEvent(new Event('change'));
      };
      return element;
    });
    // The browser's decoder is not in jsdom; dimensions are its job, and zero is the honest answer.
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
  }

  const png = (name: string) =>
    new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], name, { type: 'image/png' });

  it('hands back the file the user chose', async () => {
    whenTheUserPicks([png('holiday.png')]);

    const picked = await new WebPhotoLibrary().pickImageAsync();

    expect(picked).not.toBeNull();
    expect(picked!.name).toBe('holiday.png');
    expect(picked!.mimeType).toBe('image/png');
    expect(Array.from(picked!.bytes.slice(0, 2))).toEqual([0x89, 0x50]);
  });

  it('answers null when nothing was chosen — cancelling is not an error', async () => {
    whenTheUserPicks([]);
    await expect(new WebPhotoLibrary().pickImageAsync()).resolves.toBeNull();
  });

  it('honours a limit', async () => {
    whenTheUserPicks([png('a.png'), png('b.png'), png('c.png')]);

    const library = new WebPhotoLibrary();
    await expect(library.pickImagesAsync(2)).resolves.toHaveLength(2);
    await expect(library.pickImagesAsync()).resolves.toHaveLength(3);
  });

  it('hands the picture over even when the decoder never answers', async () => {
    // jsdom has no decoder, which is exactly the case that used to hang: no onload, no onerror,
    // and a promise pending for ever — the chosen picture never arriving at all.
    whenTheUserPicks([png('mystery.png')]);

    const picked = await new WebPhotoLibrary().pickImageAsync();

    expect(picked).not.toBeNull();
    expect(picked!.width).toBe(0);
    expect(picked!.bytes.length).toBeGreaterThan(0);
  });

  it('needs no permission — the input IS the grant', async () => {
    // Given per file, at the moment the user picks: stronger than a permission, and no prompt.
    await expect(new WebPhotoLibrary().getPermissionAsync()).resolves.toBe('granted');
  });
});
