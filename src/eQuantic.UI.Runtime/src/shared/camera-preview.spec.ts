import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { CameraPreviewNode } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** The live node's web half — the C# WebRealizer twin pins the same two shapes. */
describe('cameraPreview lowering (C# cross-pin)', () => {
  it('without a session it is the placeholder div — which is every SSR', () => {
    const node: CameraPreviewNode = {
      nodeKind: 'cameraPreview', session: null, width: 320, height: 240,
    };
    const html = lowerVisualNode(node, ctx);
    expect(html.tag).toBe('div');
  });

  it('with one, a muted autoplaying video carrying the session id', () => {
    const node: CameraPreviewNode = {
      nodeKind: 'cameraPreview', session: { id: 'cam-1' }, width: 320, height: 240,
    };
    const html = lowerVisualNode(node, ctx);
    expect(html.tag).toBe('video');
    expect(html.attributes['data-eq-camera']).toBe('cam-1');
    expect(html.attributes['autoplay']).toBe('');
    expect(html.attributes['muted']).toBe('');
  });
});
