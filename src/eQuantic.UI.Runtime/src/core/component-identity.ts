/**
 * The identity a component instance carries: its class's `static $typeId`, the CLR full name eqc
 * emits on every component it transpiles — the same string the server's `ComponentIdentity.Of`
 * answers for the same type. A class without one (a hand-written twin, a test's fixture) falls back
 * to its own name, which is what every identity was before this existed (#278).
 *
 * ONE function for every place the runtime asks "is this the same component": the walk that names
 * a component for its server state, and the store that decides whether a retained instance at a
 * path is the one being built there. Two readers answering that with two rules is how a `B.Row`
 * could inherit an `A.Row`'s state in one of them after the other had been fixed.
 *
 * A LEAF module on purpose: `core/component` and `shared/instance-store` both import it, and neither
 * may import the other's module (core does not import shared/lowering — the cycle has bitten twice).
 */
export function componentIdentity(instance: object): string {
  const type = instance.constructor as { $typeId?: string; name?: string };
  return type.$typeId ?? type.name ?? '';
}
