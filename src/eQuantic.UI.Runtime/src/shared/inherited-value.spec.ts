/**
 * An inherited value in the browser (#1395): a component reads it through its context with the type
 * eqc hands the lookup, and a change rebuilds the components that depend on it and nothing else, as on
 * .NET (`InheritedComponentTests`).
 */

import { beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { ComponentContext } from './photon-context';
import { BuildOwner } from './components/BuildOwner';
import { photonTheme } from './design-system.generated';
import { RouteValues } from './route-values';
import { Column, InheritedComponent, Text, Theme } from './vocabulary';
import type { VisualNodeValue } from './nodes';
import type { VisualNode } from './vocabulary';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

let builds: Record<string, number> = {};
const built = (label: string) => {
  builds[label] = (builds[label] ?? 0) + 1;
};

/** An app's inherited value: a number for a subtree. */
class Score extends InheritedComponent {
  constructor(
    readonly value: number,
    child: unknown,
  ) {
    super(child as never);
  }

  updateShouldNotify(old: InheritedComponent): boolean {
    return (old as Score).value !== this.value;
  }
}

/** Depends on the score. */
class Reader extends StatefulComponent {
  build(context: unknown): VisualNodeValue {
    built('reader');
    const score = (context as ComponentContext).dependOn(Score);
    return new Text(`score ${score?.value}`, 'label') as unknown as VisualNodeValue;
  }
}

/** Reads it once. */
class Glancer extends StatefulComponent {
  build(context: unknown): VisualNodeValue {
    built('glancer');
    const score = (context as ComponentContext).find(Score);
    return new Text(`glanced ${score?.value}`, 'label') as unknown as VisualNodeValue;
  }
}

/** Reads nothing. */
class Bystander extends StatefulComponent {
  build(): VisualNodeValue {
    built('bystander');
    return new Text('bystander', 'label') as unknown as VisualNodeValue;
  }
}

/** Holds the score, over a subtree its parent handed it. */
class Holder extends StatefulComponent {
  value = 0;

  constructor(private slot: unknown) {
    super();
    holder ??= this;
  }

  adoptConfig(next: Holder): void {
    this.slot = next.slot;
  }

  bump(): void {
    this.setState(() => this.value++);
  }

  build(): VisualNodeValue {
    built('holder');
    return new Score(this.value, this.slot) as unknown as VisualNodeValue;
  }
}
let holder: Holder | null = null;

class Page extends StatefulComponent {
  build(): VisualNodeValue {
    const column = new Column(0);
    column.add(new Reader() as never);
    column.add(new Glancer() as never);
    column.add(new Bystander() as never);
    const outer = new Column(0);
    outer.add(new Holder(column) as never);
    return outer as unknown as VisualNodeValue;
  }
}

beforeEach(() => {
  builds = {};
  holder = null;
  document.body.innerHTML = '';
});

describe('an inherited value in the browser', () => {
  it('is found by its type, and a change rebuilds what depends on it and nothing else', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new Page().mount(container);
    expect(container.textContent).toContain('score 0');
    expect(container.textContent).toContain('glanced 0');
    builds = {};

    holder!.bump();
    await nextFrame();

    expect(container.textContent).toContain('score 1');
    expect(builds, 'the holder and the one that depends on its value').toEqual({ holder: 1, reader: 1 });
  });
});

describe('the route in the browser', () => {
  /** Reads the route, which the frame hands every build. */
  class RouteReader extends StatefulComponent {
    build(context: unknown): VisualNodeValue {
      built('route');
      return new Text(`slug ${(context as ComponentContext).route.param('slug')}`, 'label') as unknown as VisualNodeValue;
    }
  }

  /** Reads the density. */
  class DensityReader extends StatefulComponent {
    build(context: unknown): VisualNodeValue {
      built('density');
      return new Text(`density ${(context as ComponentContext).density}`, 'label') as unknown as VisualNodeValue;
    }
  }

  const frame = (slug: string) =>
    new ComponentContext(photonTheme, 1, 'comfortable', undefined, new RouteValues({ slug }, null));

  it('builds what read the route when the frame hands another, and nothing else (#1395)', () => {
    const owner = new BuildOwner((component, context) => component.build(context) as unknown as VisualNode);
    const column = new Column(0);
    column.add(new RouteReader() as never);
    column.add(new DensityReader() as never);
    column.add(new Bystander() as never);
    owner.build(column as unknown as VisualNode, frame('first') as never, 'compact', true);
    owner.endFrame();
    expect(builds).toEqual({ route: 1, density: 1, bystander: 1 });

    builds = {};
    owner.build(column as unknown as VisualNode, frame('second') as never, 'compact', false);
    owner.endFrame();

    expect(builds, 'the route has readers of its own').toEqual({ route: 1 });
  });
});

describe('a theme over a subtree in the browser', () => {
  /** Says which theme it draws in. */
  class ThemeReader extends StatefulComponent {
    constructor(
      private readonly name: string,
      private readonly other: object,
    ) {
      super();
    }

    build(context: unknown): VisualNodeValue {
      const theme = (context as ComponentContext).theme;
      built(`${this.name} ${theme === this.other ? 'other' : 'frame'}`);
      return new Text(this.name, 'label') as unknown as VisualNodeValue;
    }
  }

  it('is read by the components under it, and the frame’s by the ones beside it (#1395)', () => {
    const other = { ...photonTheme };
    const owner = new BuildOwner((component, context) => component.build(context) as unknown as VisualNode);
    const column = new Column(0);
    column.add(new Theme(other, new ThemeReader('inside', other) as never) as never);
    column.add(new ThemeReader('beside', other) as never);

    owner.build(column as unknown as VisualNode, new ComponentContext(photonTheme) as never, 'compact', true);
    owner.endFrame();

    expect(builds).toEqual({ 'inside other': 1, 'beside frame': 1 });
  });
});
