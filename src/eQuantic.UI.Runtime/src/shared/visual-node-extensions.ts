import { SizeValue } from './value-types';
import { Row, type VisualNode } from './vocabulary';

/** Anything a child can be added to — what C# constrains with `where T : FlexNode`. */
type Container = VisualNode & { add(child: never): void };

/**
 * The twin of C# `eQuantic.UI.Primitives.VisualNodeExtensions`, as the STATIC home every other
 * extension lowers to: JavaScript has no extension methods, so `node.Centered()` compiles to
 * `VisualNodeExtensions.centered(node)` and the receiver is the first argument.
 *
 * It used to be mirrored as an INSTANCE method instead — on `VisualNode` here and on `Component`
 * in `core/types`, with a `setCenterWrapper` seam between them to break the import cycle that
 * arrangement created. Three artefacts, and the collision in #245: a component written
 * `class StatTile(string label, bool centered = false)` lowered its captured parameter to a field
 * named `centered`, which shadowed the method, and the page failed only in the browser —
 * `AppUI.statTile(...).centered is not a function`. A static cannot be shadowed by a field, and
 * every extension added later is out of the collision set with it.
 */
export class VisualNodeExtensions {
  /**
   * This node in the MIDDLE of whatever contains it. A Box has no alignment of its own and
   * centring needs SLACK, so the wrapper fills both axes and then centres — the whole rule, in
   * one place, instead of four lines repeated wherever a glyph would otherwise sit in a corner.
   */
  static centered(node: VisualNode): VisualNode {
    const row = new Row(0, {
      width: SizeValue.fill,
      height: SizeValue.fill,
      main: 'center',
      cross: 'center',
    });
    row.add(node as never);
    return row;
  }

  /** Add a child and hand the container back — the fluent form of `add`. */
  static with<T extends Container>(node: T, child: VisualNode): T {
    node.add(child as never);
    return node;
  }
}
