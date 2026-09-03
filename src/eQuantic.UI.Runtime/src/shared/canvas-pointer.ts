/**
 * A pointer event in a canvas's own coordinates — the C# `CanvasPointer` twin.
 *
 * Its own module, and that is the point: `vocabulary` imports `lowering`, so `lowering` must never
 * import `vocabulary` (a cycle that has bitten this runtime twice). The class both of them need
 * therefore lives in a LEAF neither of them owns.
 */
export class CanvasPointer {
  readonly x: number;
  readonly y: number;
  readonly pressed: boolean;
  readonly modifiers: number;

  // The trailing config is eqc's object-initializer protocol, and every vocabulary twin accepts it
  // (a guard spec says so): the C# record struct has only positional members today, but a twin that
  // silently dropped a config would fail the day one is added, in the browser rather than the build.
  constructor(x: number, y: number, pressed: boolean, modifiers = 0, config?: Record<string, unknown>) {
    this.x = x;
    this.y = y;
    this.pressed = pressed;
    this.modifiers = modifiers;
    if (config) Object.assign(this, config);
  }

  /** Radians clockwise from three o'clock — the convention `fillAnnularSector` uses, so a
   * sunburst's hit test is a comparison rather than a conversion. */
  angleFrom(centerX: number, centerY: number): number {
    return Math.atan2(this.y - centerY, this.x - centerX);
  }

  /** The other half of a polar hit test. */
  distanceFrom(centerX: number, centerY: number): number {
    const dx = this.x - centerX;
    const dy = this.y - centerY;
    return Math.sqrt(dx * dx + dy * dy);
  }
}
