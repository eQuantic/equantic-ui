/**
 * The twins of the C# layout delegates (#1556): `MultiChildLayoutDelegate`, `SingleChildLayoutDelegate` and the
 * one-child adapter every realizer lays a `CustomSingleChildLayout` out through. An app's delegate is C# that eqc
 * transpiles into a class extending these, so the steps it overrides and the calls it makes (`layoutChild`,
 * `positionChild`, `hasChild`) are these methods, and the refusals say what the C# ones say.
 */

import { BoxConstraints, Point, Size } from './value-types';
import { equals } from '../utils/equals';
import { exception } from '../utils/exceptions';

/** The children a delegate lays out, as the browser binds them for one layout (C# `ILayoutChildren`). */
export interface LayoutChildren {
  has(id: unknown): boolean;
  layout(id: unknown, constraints: BoxConstraints): Size;
  position(id: unknown, offset: Point): void;
}

function refuse(message: string): never {
  throw exception('System.InvalidOperationException', message);
}

/** Mirror of the C# `MultiChildLayoutDelegate`. */
export abstract class MultiChildLayoutDelegate {
  private boundChildren: LayoutChildren | null = null;
  private readonly laidOutIds: unknown[] = [];

  getSize(constraints: BoxConstraints): Size {
    return constraints.biggest;
  }

  performLayout(_size: Size): void {}

  layout(constraints: BoxConstraints): Size {
    const size = constraints.constrain(this.getSize(constraints));
    this.performLayout(size);
    return size;
  }

  shouldRelayout(_oldDelegate: MultiChildLayoutDelegate): boolean {
    return true;
  }

  protected hasChild(id: unknown): boolean {
    return this.bound().has(id);
  }

  protected layoutChild(id: unknown, constraints: BoxConstraints): Size {
    const children = this.bound();
    if (!children.has(id))
      refuse(`${this.delegateName} laid out a child with the id '${String(id)}', which no child carries.`);
    if (this.laidOutIds.some((done) => equals(done, id)))
      refuse(`${this.delegateName} laid out the child with the id '${String(id)}' twice: each child is laid out once.`);
    this.laidOutIds.push(id);
    return children.layout(id, constraints);
  }

  protected positionChild(id: unknown, offset: Point): void {
    const children = this.bound();
    if (!children.has(id))
      refuse(`${this.delegateName} placed a child with the id '${String(id)}', which no child carries.`);
    children.position(id, offset);
  }

  /** Runs `layout` with `children` bound for its length (C# `LayoutWith`): the browser's door. */
  layoutWith(children: LayoutChildren, room: BoxConstraints, ids: readonly unknown[]): Size {
    if (this.boundChildren)
      refuse(`${this.delegateName} is laying out already: a delegate lays out one node at a time.`);
    this.boundChildren = children;
    this.laidOutIds.length = 0;
    try {
      const size = this.layout(room);
      if (!Number.isFinite(size.width) || !Number.isFinite(size.height))
        refuse(
          `${this.delegateName} answered a size of ${size.width} by ${size.height} for a room of ${String(room)}: a custom ` +
            "layout's size is finite, so an axis the room leaves unbounded needs a size of the delegate's own.",
        );
      for (const id of ids)
        if (!this.laidOutIds.some((done) => equals(done, id)))
          refuse(`${this.delegateName} did not lay out the child with the id '${String(id)}': each child is laid out exactly once.`);
      return size;
    } finally {
      this.boundChildren = null;
      this.laidOutIds.length = 0;
    }
  }

  /** The delegate's name in what a refusal says (C# `Name`). */
  protected get delegateName(): string {
    return this.constructor.name;
  }

  private bound(): LayoutChildren {
    return (
      this.boundChildren ??
      refuse(`${this.delegateName} reached its children outside a layout: they are reached only from GetSize, PerformLayout and Layout.`)
    );
  }
}

/** Mirror of the C# `SingleChildLayoutDelegate`. */
export abstract class SingleChildLayoutDelegate {
  getSize(constraints: BoxConstraints): Size {
    return constraints.biggest;
  }

  getConstraintsForChild(constraints: BoxConstraints): BoxConstraints {
    return constraints;
  }

  getPositionForChild(_size: Size, _childSize: Size): Point {
    return Point.zero;
  }

  shouldRelayout(_oldDelegate: SingleChildLayoutDelegate): boolean {
    return true;
  }
}

/** Mirror of the C# `SingleChildLayout`: a single child's delegate laid out as a layout of one. */
export class SingleChildLayout extends MultiChildLayoutDelegate {
  static readonly childId = 'child';

  constructor(readonly single: SingleChildLayoutDelegate) {
    super();
  }

  protected override get delegateName(): string {
    return this.single?.constructor.name ?? 'SingleChildLayoutDelegate';
  }

  override layout(constraints: BoxConstraints): Size {
    const size = constraints.constrain(this.single.getSize(constraints));
    const child = this.layoutChild(SingleChildLayout.childId, this.single.getConstraintsForChild(constraints));
    this.positionChild(SingleChildLayout.childId, this.single.getPositionForChild(size, child));
    return size;
  }
}
