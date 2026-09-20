import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { Component } from './types';
import { StatefulComponent, StatelessComponent } from './component';

/**
 * WHAT A COMPONENT ALREADY HAS, read from the live runtime rather than listed.
 *
 * A C# component's members lower to JavaScript members of the same object: a primary-constructor
 * parameter, a field and an auto-property all become `this.<name>`. C# keeps them apart — one is a
 * capture, one is a field, one is a property, and the case differs — and JavaScript folds all three
 * onto one key. So a parameter named `render` REPLACES the method, and the page fails only in the
 * browser (#245).
 *
 * eqc refuses that at build time, and the list it checks against is THIS file's output: a fixture
 * the compiler embeds, written by walking the prototype chain here. Typing it by hand is how it
 * would drift — the runtime gains a method, the list does not, and the build stops refusing what it
 * exists to refuse. Regenerate with `EQ_UPDATE_RUNTIME_MEMBERS=1`; the pin fails either way round,
 * so a member added here without regenerating fails too.
 */
const FIXTURE = resolve(
  __dirname,
  '../../../eQuantic.UI.Compiler/Resources/runtime-members.txt',
);

/** Every instance member a component inherits: the prototype chain's methods and accessors, plus
 * the own keys a fresh instance carries.
 *
 * NOTHING is left out. `constructor` looks like plumbing rather than a member, but `Constructor` is
 * a legal C# name and the emitter lowers it like any other — `this.constructor = constructor` — over
 * the instance's own class, which the runtime reads `$hydration` and `name` from.
 *
 * The `_`-prefixed internals stay for a sharper reason: `IdentifierStrategy` lowers a leading
 * underscore UNCHANGED (`_count` → `this._count`), and `_`-prefixed is how component state is
 * written all over this repository. So `_mounted`, `_renderManager`, `_instances` and the rest are
 * names a C# field reaches exactly, and the one it would corrupt is the lifecycle. They were
 * excluded here on the reasoning that no C# member lowers to one, which is the opposite of true. */
function membersOf(ctor: new () => object): Set<string> {
  const found = new Set<string>();
  const instance = new ctor();
  for (const key of Object.keys(instance)) found.add(key);
  for (
    let proto: object | null = Object.getPrototypeOf(instance);
    proto && proto !== Object.prototype;
    proto = Object.getPrototypeOf(proto)
  ) {
    for (const key of Object.getOwnPropertyNames(proto)) found.add(key);
  }
  return found;
}

describe('the members a component already has', () => {
  it('are what the compiler refuses to let a C# member shadow', () => {
    // `build` is NOT filtered out of the result. Every component declares one, and it is an
    // OVERRIDE — which the compiler exempts by the `override` keyword, not by a name missing from
    // this list. Filtering it here would make the fixture lie about the runtime AND leave that
    // exemption with nothing to exercise it: removing it from the guard broke no test at all.
    class Stateless extends StatelessComponent {
      build(): never {
        throw new Error('never built');
      }
    }
    class Stateful extends StatefulComponent {
      build(): never {
        throw new Error('never built');
      }
    }
    class Bare extends Component {
      render(): never {
        throw new Error('never rendered');
      }
    }

    const members = [
      ...new Set([
        ...membersOf(Stateless as never),
        ...membersOf(Stateful as never),
        ...membersOf(Bare as never),
      ]),
    ].sort();

    const written = `${members.join('\n')}\n`;
    if (process.env.EQ_UPDATE_RUNTIME_MEMBERS === '1') {
      mkdirSync(dirname(FIXTURE), { recursive: true });
      writeFileSync(FIXTURE, written, 'utf8');
    }

    expect(existsSync(FIXTURE), `${FIXTURE} is missing — regenerate with EQ_UPDATE_RUNTIME_MEMBERS=1`)
      .toBe(true);
    expect(readFileSync(FIXTURE, 'utf8')).toBe(written);
  });
});
