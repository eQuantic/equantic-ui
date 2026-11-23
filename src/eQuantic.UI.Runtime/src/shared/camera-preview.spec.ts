import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { WebCamera, type CameraSessionValue } from './devices/camera';
import { afterWrite } from './after-write';
import { CameraPreview } from './vocabulary';
import type { VisualNodeValue } from './nodes';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { CameraPreviewNode } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** The live node's web half — the C# WebRealizer twin pins the same two shapes. */
describe('cameraPreview lowering (C# cross-pin)', () => {
  it('without a session it is the placeholder div — which is every SSR', () => {
    const node: CameraPreviewNode = {
      nodeKind: 'cameraPreview',
      session: null,
      width: 320,
      height: 240,
    };
    const html = lowerVisualNode(node, ctx);
    expect(html.tag).toBe('div');
  });

  it('with one, a muted autoplaying video carrying the session id', () => {
    const node: CameraPreviewNode = {
      nodeKind: 'cameraPreview',
      session: { id: 'cam-1' },
      width: 320,
      height: 240,
    };
    const html = lowerVisualNode(node, ctx);
    expect(html.tag).toBe('video');
    expect(html.attributes['data-eq-camera']).toBe('cam-1');
    expect(html.attributes['autoplay']).toBe('');
    expect(html.attributes['muted']).toBe('');
  });
});

/**
 * A preview a page's render shows gets its stream in that render (#1830). The streams attached at the end of a
 * pass, before the render manager wrote the tree it produced, so the preview's <video> was not in the document
 * yet and played nothing until some later pass came. They now attach in the microtask after the write, with the
 * focus traps (shared/after-write.ts).
 */
describe('a camera preview a render shows', () => {
  const nextFrame = () =>
    new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

  class Viewfinder extends StatefulComponent {
    session: CameraSessionValue | null = null;

    show(session: CameraSessionValue | null): void {
      this.setState(() => (this.session = session));
    }

    build(): VisualNodeValue {
      return new CameraPreview(this.session, 320, 240) as unknown as VisualNodeValue;
    }
  }

  beforeEach(() => {
    document.body.innerHTML = '';
  });

  // jsdom checks a srcObject against a MediaStream it does not implement: the property is a plain slot here.
  const srcObject = Object.getOwnPropertyDescriptor(HTMLMediaElement.prototype, 'srcObject');
  const hadOwn = srcObject !== undefined;
  const slots = new WeakMap<HTMLMediaElement, unknown>();

  beforeEach(() => {
    Object.defineProperty(HTMLMediaElement.prototype, 'srcObject', {
      configurable: true,
      get(this: HTMLMediaElement) {
        return slots.get(this) ?? null;
      },
      set(this: HTMLMediaElement, value: unknown) {
        slots.set(this, value);
      },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    if (hadOwn) Object.defineProperty(HTMLMediaElement.prototype, 'srcObject', srcObject!);
    else delete (HTMLMediaElement.prototype as unknown as { srcObject?: unknown }).srcObject;
  });

  it('plays its stream with no later pass', async () => {
    const stream = { getTracks: () => [] } as unknown as MediaStream;
    vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: () => Promise.resolve(stream) } });
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(() => Promise.resolve());
    const host = document.createElement('div');
    document.body.appendChild(host);
    const page = new Viewfinder();
    page.mount(host);
    await nextFrame();

    const session = await new WebCamera().startPreviewAsync();
    page.show(session);
    await nextFrame();

    const video = host.querySelector('video[data-eq-camera]') as HTMLVideoElement | null;
    expect(video, 'the render wrote the preview').not.toBeNull();
    expect(video!.srcObject, 'the stream attached to the video the render wrote').toBe(stream);
    session!.dispose();
  });
});

describe('the moment after the write', () => {
  // The stub goes whatever the case does, so a later case reports its errors where a window does.
  afterEach(() => vi.unstubAllGlobals());

  it('runs every task queued, though one throws', async () => {
    const reported: unknown[] = [];
    vi.stubGlobal('reportError', (error: unknown) => reported.push(error));
    const ran: string[] = [];
    afterWrite(() => {
      ran.push('first');
      throw new Error('a task failed');
    });
    afterWrite(() => ran.push('second'));
    await Promise.resolve();

    expect(ran).toEqual(['first', 'second']);
    expect(reported).toHaveLength(1);
  });
});
