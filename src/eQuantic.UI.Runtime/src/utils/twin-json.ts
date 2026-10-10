import { adoptMember, declaresMember } from './adopt-member';

/**
 * A twin's JSON, as System.Text.Json writes the C# value it stands for (#591): the twin's own
 * properties, each under its own name, except a property's STORE. A property that a derived type can
 * override, or whose accessors use `field`, keeps its value in `$name` under accessors on the
 * prototype, and `JSON.stringify` writes an object's own properties only, so a server action
 * received `$name` and bound nothing to the property. A store is written under its property's name,
 * read through the property, as the serializer reads a property through its getter. The `$` is
 * decisive, since no C# member's name can begin with one, and a `$name` with no member of that name
 * along the chain is copied as it is. Each key is defined, as every wire object the runtime builds
 * is, so a member called `__proto__` stays a member.
 *
 * A key with a `$` AFTER its name is a field that moved a case apart from a member of its class
 * (`value$` beside `value`, #396), with a `$` more for each slot an ancestor already held
 * (`value$$`): storage the serializer never writes, since it is not a public property. The member
 * whose name the field gave up, the name before every trailing `$`, is written instead, read through
 * its accessor, where the class declares one.
 *
 * Each JSON name is written ONCE: a moved field and a property's store can both stand for one
 * property (`value$` beside the `$value` of a virtual or a `field`-backed `Value`), and
 * System.Text.Json reads its getter once, where both keys read it, and a getter with an effect ran
 * twice and wrote its second answer (Copilot's second review of #696).
 *
 * What a twin that keeps a store answers `toJSON` with, so a type derived from it answers the same.
 */
export function twinJson(value: object): Record<string, unknown> {
  const json: Record<string, unknown> = {};
  const source = value as Record<string, unknown>;
  const written = new Set<string>();
  const write = (name: string): void => {
    if (written.has(name)) return;
    written.add(name);
    adoptMember(json, name, source[name]);
  };
  for (const key of Object.keys(value)) {
    if (key.endsWith('$')) {
      const member = key.replace(/\$+$/, '');
      if (declaresMember(value, member)) write(member);
      continue;
    }
    const property = key.startsWith('$') ? key.slice(1) : '';
    write(property !== '' && declaresMember(value, property) ? property : key);
  }
  return json;
}
