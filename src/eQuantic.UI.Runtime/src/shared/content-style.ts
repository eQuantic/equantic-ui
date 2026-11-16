import type { TextAlignmentValue, TypeRoleValue } from './enums.generated';
import type { ContentStyleValue, TypeStyleValue } from './nodes';

/**
 * Mirror of the C# `ContentStyle` record (#1577): the look a box hands the text and icons inside it
 * when they name none of their own, Flutter's `DefaultTextStyle` and `IconTheme`. Every member is
 * optional and means "the one around this box".
 *
 * Built from the transpiled initializer's config object (`new ContentStyle({ role: 'label' })`);
 * `merge` also takes the plain object a hydration payload carries.
 */
export class ContentStyle implements ContentStyleValue {
  /** The role a text that names none is set in. */
  role?: TypeRoleValue | null;
  /** The type style over the role's, which reaches only a text that took this look's role. */
  type?: TypeStyleValue | null;
  /** The line limit a text that names none is cut to; 0 is no limit. */
  maxLines?: number | null;
  /** The line alignment a text that names none takes. */
  align?: TextAlignmentValue | null;
  /** The size an icon that names none takes, on the §07 whitelist (16, 20, 24 or 32). */
  iconSize?: number | null;

  constructor(config?: ContentStyleValue) {
    if (!config) return;
    Object.assign(this, config);
    const size = this.iconSize;
    if (size != null && size !== 16 && size !== 20 && size !== 24 && size !== 32) {
      throw new RangeError(`An icon's size is on the §07 whitelist (16/20/24/32), here as on Icon itself; got ${size}.`);
    }
  }

  /**
   * `over`'s members laid over `under`'s (the C# `ContentStyle.Merge`): what `over` names wins, what it
   * leaves null is `under`'s. Null when both are. A style rides the role it was written for, so an
   * inner box that names a role without a style does not inherit one cut for the outer box's role.
   */
  static merge(
    under: ContentStyleValue | null | undefined,
    over: ContentStyleValue | null | undefined,
  ): ContentStyleValue | null {
    if (under == null) return over ?? null;
    if (over == null) return under;
    return new ContentStyle({
      role: over.role ?? under.role,
      type: over.type ?? (over.role == null ? under.type : null),
      maxLines: over.maxLines ?? under.maxLines,
      align: over.align ?? under.align,
      iconSize: over.iconSize ?? under.iconSize,
    });
  }
}
