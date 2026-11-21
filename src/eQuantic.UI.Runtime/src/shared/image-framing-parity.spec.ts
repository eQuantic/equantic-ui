/**
 * A picture held by a focal point and zoomed (C# `Image.FocalPoint`, `Image.Zoom`, #1785), styled ONE
 * way. The class names are CROSS-PINNED with the C# suite
 * (`ImageRealizerTests.TheFramedPicture_CarriesTheClassesTheTwinProduces`), which asserts the same
 * strings for the same node: an element styled by classes on the server and by other classes in the
 * browser is the same element described twice.
 */
import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import { Image } from './vocabulary';
import { Point } from './value-types';

const context = {} as Parameters<typeof lowerVisualNode>[1];

const framed = (zoom: number) =>
  new Image('/portraits/francine.jpg', 220, 220, 'cover', 'Francine', {
    cornerRadius: { topLeft: 110, topRight: 110, bottomRight: 110, bottomLeft: 110 } as never,
    focalPoint: new Point(Math.fround(0.3), Math.fround(0.7)),
    zoom,
  });

describe('image framing parity (C# ImageRealizerTests cross-pin)', () => {
  it('a zoomed picture is a frame that clips, around a picture scaled about its focal point', () => {
    const frame = lowerVisualNode(framed(1.5), context);

    expect(frame.tag).toBe('span');
    expect(frame.attributes['class']).toBe('eq-12dgucd eq-1fusabo eq-elib30 eq-lc8w9s eq-mbtjfn');
    expect(frame.children).toHaveLength(1);
    const picture = frame.children[0] as { tag: string; attributes: Record<string, string> };
    expect(picture.tag).toBe('img');
    expect(picture.attributes['class']).toBe('eq-12dgucd eq-18isjz0 eq-19qisgk eq-1cnz5gh eq-1fusabo eq-racph6');
  });

  it('a held picture with no zoom stays one img, positioned', () => {
    const picture = lowerVisualNode(framed(1), context);

    expect(picture.tag).toBe('img');
    expect(picture.attributes['class']).toBeDefined();
  });

  it('refuses a focal point off the picture and a zoom below its fit, as C# does', () => {
    expect(() => new Image('/a.jpg', 10, 10, 'cover', '', { focalPoint: new Point(1.2, 0.5) })).toThrow('FocalPoint');
    expect(() => new Image('/a.jpg', 10, 10, 'cover', '', { zoom: 0.5 })).toThrow('Zoom');
  });
});
