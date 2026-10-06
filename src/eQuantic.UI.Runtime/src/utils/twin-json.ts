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
 * What a twin that keeps a store answers `toJSON` with, so a type derived from it answers the same.
 */
export function twinJson(value: object): Record<string, unknown> {
  const json: Record<string, unknown> = {};
  const source = value as Record<string, unknown>;
  for (const key of Object.keys(value)) {
    const property = key.startsWith('$') ? key.slice(1) : '';
    if (property !== '' && declaresMember(value, property)) adoptMember(json, property, source[property]);
    else adoptMember(json, key, source[key]);
  }
  return json;
}
