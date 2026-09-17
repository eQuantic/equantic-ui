/**
 * The arm a closed union makes unreachable.
 *
 * Its job is done at COMPILE time: a `switch` over a union that ends in `assertNever(value)` stops
 * compiling the moment the union grows a member with no case, because only `never` is assignable to
 * `never`. That is how the browser gets the exhaustiveness the C# side gets from a visitor — the
 * client dispatches on a string (class names do not survive bundling), and a string alone has no
 * such check.
 *
 * At RUNTIME it can only be reached by a value that lied about its type: a node from a bundle this
 * one has never heard of, or an object hand-built past the types. It throws rather than returning,
 * because the alternative is a silently missing piece of a page — the exact defect the union was
 * added to prevent, arriving by the one door left open.
 */
export function assertNever(value: never, what = 'value'): never {
  throw new Error(`Unhandled ${what}: ${JSON.stringify(value)}`);
}
