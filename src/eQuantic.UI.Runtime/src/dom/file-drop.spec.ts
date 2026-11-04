import { describe, it, expect, afterEach } from 'vitest';
import { PickedFile } from '../shared/picked-file';
import { dropHandlers, installDropGuard, resetDropGuard } from './file-drop';

/**
 * The web half of `DropTarget`, driven with the events a browser sends while files are dragged over a
 * page: each element the pointer crosses fires its own enter and leave, and only a drag that carries
 * files is an invitation.
 */
describe('a drop area', () => {
  afterEach(() => {
    resetDropGuard();
    document.body.innerHTML = '';
  });

  /** A drag event as the browser sends it, carrying files or text. */
  function drag(type: string, carries: 'files' | 'text', files: File[] = []): Event {
    const event = new Event(type, { bubbles: true, cancelable: true });
    const transfer = { types: carries === 'files' ? ['Files'] : ['text/plain'], files, dropEffect: 'none' };
    Object.defineProperty(event, 'dataTransfer', { value: transfer });
    return event;
  }

  function area(enabled = true) {
    const log: Array<boolean | string[]> = [];
    const host = document.createElement('div');
    const label = document.createElement('span');
    const button = document.createElement('button');
    host.append(label, button);
    document.body.append(host);
    const handlers = dropHandlers(
      (files: PickedFile[]) => log.push(files.map((file) => file.name)),
      (over: boolean) => log.push(over),
      enabled,
    );
    for (const [type, handler] of Object.entries(handlers)) host.addEventListener(type, handler as EventListener);
    return { host, label, button, log };
  }

  const file = (name: string) => new File([new Uint8Array([1])], name, { type: 'model/stl' });

  it('says once when files come over it and once when they leave, however many children they cross', () => {
    const { host, label, button, log } = area();

    host.dispatchEvent(drag('dragenter', 'files'));
    label.dispatchEvent(drag('dragenter', 'files'));
    host.dispatchEvent(drag('dragleave', 'files'));
    button.dispatchEvent(drag('dragenter', 'files'));
    label.dispatchEvent(drag('dragleave', 'files'));
    button.dispatchEvent(drag('dragleave', 'files'));

    expect(log).toEqual([true, false]);
  });

  it('hands over the dropped files, and says they are no longer over it', () => {
    const { host, label, log } = area();

    host.dispatchEvent(drag('dragenter', 'files'));
    const drop = drag('drop', 'files', [file('bracket.stl'), file('lid.stl')]);
    label.dispatchEvent(drop);

    expect(log).toEqual([true, false, ['bracket.stl', 'lid.stl']]);
    expect(drop.defaultPrevented).toBe(true);
  });

  it('accepts the drag as a copy, which is what makes it a drop target at all', () => {
    const { host } = area();

    const over = drag('dragover', 'files');
    host.dispatchEvent(over);

    expect(over.defaultPrevented).toBe(true);
    expect((over as DragEvent).dataTransfer!.dropEffect).toBe('copy');
  });

  it('when disabled, refuses the drop and takes nothing, but still says files are over it', () => {
    const { host, log } = area(false);

    host.dispatchEvent(drag('dragenter', 'files'));
    const over = drag('dragover', 'files');
    host.dispatchEvent(over);
    host.dispatchEvent(drag('drop', 'files', [file('bracket.stl')]));

    expect((over as DragEvent).dataTransfer!.dropEffect).toBe('none');
    expect(log).toEqual([true, false]);
  });

  it('is no invitation to text or a link dragged across it', () => {
    const { host, log } = area();

    const enter = drag('dragenter', 'text');
    host.dispatchEvent(enter);
    host.dispatchEvent(drag('drop', 'text'));

    expect(log).toEqual([]);
    expect(enter.defaultPrevented).toBe(false);
  });

  it('forgets a child a re-render removed while the files were over it', () => {
    const { label, button, log } = area();

    label.dispatchEvent(drag('dragenter', 'files'));
    // The "over" report re-rendered the area: the label under the pointer was replaced, and the
    // browser never reports a leave for an element that is gone.
    label.remove();
    button.dispatchEvent(drag('dragenter', 'files'));
    button.dispatchEvent(drag('dragleave', 'files'));

    expect(log).toEqual([true, false]);
  });

  it('gives the files to the innermost area only', () => {
    const outer = area();
    const inner = area();
    outer.host.append(inner.host);

    inner.label.dispatchEvent(drag('dragenter', 'files'));
    inner.label.dispatchEvent(drag('drop', 'files', [file('bracket.stl')]));

    expect(inner.log).toEqual([true, false, ['bracket.stl']]);
    // The bubbled enter lit the outer area too, and the drop is the only end it gets.
    expect(outer.log).toEqual([true, false]);
  });
});

describe('the drop guard', () => {
  afterEach(() => resetDropGuard());

  function drag(type: string, carries: string[]): Event {
    const event = new Event(type, { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'dataTransfer', { value: { types: carries, files: [], dropEffect: 'copy' } });
    return event;
  }

  it('refuses a file that missed every area, which the browser would otherwise open over the page', () => {
    installDropGuard();

    const over = drag('dragover', ['Files']);
    document.body.dispatchEvent(over);
    const drop = drag('drop', ['Files']);
    document.body.dispatchEvent(drop);

    expect(over.defaultPrevented).toBe(true);
    expect((over as DragEvent).dataTransfer!.dropEffect).toBe('none');
    expect(drop.defaultPrevented).toBe(true);
  });

  it('leaves text alone, and is not there until a page shows a drop area', () => {
    const before = drag('drop', ['Files']);
    document.body.dispatchEvent(before);
    installDropGuard();
    const text = drag('drop', ['text/plain']);
    document.body.dispatchEvent(text);

    expect(before.defaultPrevented).toBe(false);
    expect(text.defaultPrevented).toBe(false);
  });
});
