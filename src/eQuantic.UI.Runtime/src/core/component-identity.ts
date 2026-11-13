/**
 * The identity a component instance carries: its class's `static $typeId`, the CLR full name eqc
 * emits on every component it transpiles — the same string the server's `ComponentIdentity.Of`
 * answers for the same type. A class without one (a hand-written twin, a test's fixture) falls back
 * to its own name, which is what every identity was before this existed (#278).
 *
 * ONE function for every place the runtime NAMES a component: the walk that names one for its server
 * state, from a page's render and from the element engine's build alike. Whether a position keeps
 * its instance is the engine's to decide, by the runtime type itself, which two classes of one simple
 * name never share; a store keyed by the name once let a `B.Row` inherit an `A.Row`'s state.
 *
 * A LEAF module on purpose: `core/component` and `shared/lowering` both import it, and core may not
 * import the lowering (the cycle has bitten twice).
 */
export function componentIdentity(instance: object): string {
  const type = instance.constructor as { $typeId?: string; name?: string };
  return type.$typeId ?? type.name ?? '';
}
