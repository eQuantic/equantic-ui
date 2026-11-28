/**
 * The focus traversal, written once (#1897): every case of `focus-traversal.fixture.json` runs here against the
 * transpiled twin of the C# `FocusManager`, which the browser runs, as `FocusTraversalFixtureTests` runs it against the
 * C# Photon runs. `FocusProbe` is the transpiled twin of the code an app writes around a node, a handler added to
 * `Changed` that removes another and an `onFocusChange`: so this also executes C# event dispatch as eqc emits it
 * (`+=` and `-=` of a delegate) against the same sequence as .NET.
 */
import { describe, expect, it } from 'vitest';
import fixture from './focus-traversal.fixture.json';
import { FocusManager } from './components/FocusManager';
import { FocusNode } from './components/FocusNode';
import { FocusScopeNode } from './components/FocusScopeNode';
import { FocusAttachment } from './components/FocusAttachment';
import { TraversalStop } from './components/TraversalStop';
import type { FocusMove } from './components/FocusMove';
import { FocusProbe } from './__fixtures__/FocusProbe';
import { FocusSearch } from './__fixtures__/FocusSearch';
import type { Column, Focus, FocusScope } from './vocabulary';
import type { $TraversalEdgeBehaviorValue } from './enums.generated';

interface HandlerSpec {
  name: string;
  removes?: string;
  adds?: string;
}

interface NodeSpec {
  scope?: boolean;
  edge?: $TraversalEdgeBehaviorValue;
  canRequestFocus?: boolean;
  skipTraversal?: boolean;
  handlers?: HandlerSpec[];
}

interface FrameSpec {
  stops: string[][];
  modal?: string;
  modalAt?: string;
  autofocus?: string[];
}

interface StepSpec {
  attach?: FrameSpec;
  tab?: boolean;
  shiftTab?: boolean;
  focused?: string | null;
  request?: string;
  unfocus?: string;
  next?: string;
  previous?: string;
  apply?: boolean;
  add?: string;
  handler?: string;
  removes?: string;
  adds?: string;
  remove?: string;
  knob?: string;
  canRequestFocus?: boolean;
  skipTraversal?: boolean;
  move?: string;
  returns?: boolean;
  heard?: string[];
  focus?: string | null;
  hasFocus?: string[];
  primary?: string | null;
}

interface CaseSpec {
  name: string;
  view?: string;
  nodes: Record<string, NodeSpec>;
  frame?: FrameSpec;
  steps: StepSpec[];
}

/** A place the focus can stand, named as the fixture names it. */
class Spot {
  constructor(readonly name: string) {}
}

const edges: Record<string, $TraversalEdgeBehaviorValue> = {
  closedLoop: 'closedLoop',
  parentScope: 'parentScope',
  stop: 'stop',
};

/** One case: the nodes, the manager, the frame it was last given and what was heard. */
class Run {
  readonly heard: string[] = [];
  readonly manager: FocusManager;
  private readonly nodes = new Map<string, FocusNode>();
  private readonly probes = new Map<string, FocusProbe>();
  private readonly spots = new Map<string, Spot>();
  private move: string | null = null;
  private returns: boolean | null = null;
  private focus: string | null = null;
  private hasFocus: string[] = [];
  private primary: string | null = null;

  constructor(testCase: CaseSpec) {
    this.manager = new FocusManager(testCase.view === 'leaves');
    this.manager.requestAsked = () => this.heard.push('asked');
    for (const [name, spec] of Object.entries(testCase.nodes)) {
      let made: FocusNode;
      if (spec.scope) {
        const scope = new FocusScopeNode();
        scope.traversalEdgeBehavior = edges[spec.edge ?? 'closedLoop'];
        made = scope;
      } else made = new FocusNode();
      if (spec.canRequestFocus !== undefined) made.canRequestFocus = spec.canRequestFocus;
      if (spec.skipTraversal !== undefined) made.skipTraversal = spec.skipTraversal;
      const probe = new FocusProbe(name, this.heard, made);
      if (spec.handlers) for (const handler of spec.handlers) probe.add(handler.name, handler.removes ?? null, handler.adds ?? null);
      else probe.watch();
      this.nodes.set(name, made);
      this.probes.set(name, probe);
    }
    if (testCase.frame) this.attach(testCase.frame);
  }

  step(step: StepSpec): void {
    this.heard.length = 0;
    this.move = null;
    this.returns = null;
    if (step.attach) this.attach(step.attach);
    else if (step.tab) this.land(this.manager.traverse(true));
    else if (step.shiftTab) this.land(this.manager.traverse(false));
    else if (step.focused !== undefined) this.manager.focused(step.focused === null ? null : this.spot(step.focused));
    else if (step.request) this.node(step.request).requestFocus();
    else if (step.unfocus) this.node(step.unfocus).unfocus();
    else if (step.next) this.returns = this.node(step.next).nextFocus();
    else if (step.previous) this.returns = this.node(step.previous).previousFocus();
    else if (step.apply) this.applied(this.manager.takeRequest());
    else if (step.add) this.probe(step.add).add(step.handler!, step.removes ?? null, step.adds ?? null);
    else if (step.remove) this.probe(step.remove).remove(step.handler!);
    else if (step.knob) {
      const node = this.node(step.knob);
      if (step.canRequestFocus !== undefined) node.canRequestFocus = step.canRequestFocus;
      if (step.skipTraversal !== undefined) node.skipTraversal = step.skipTraversal;
    } else throw new Error(`A step the runner does not know: ${JSON.stringify(step)}`);
  }

  /** What the step answered and what stands after it: whatever the step does not say is what the step before it said,
   * so every step asserts all of it. */
  check(step: StepSpec, because: string): void {
    if (step.focus !== undefined) this.focus = step.focus;
    if (step.hasFocus !== undefined) this.hasFocus = [...step.hasFocus].sort();
    if (step.primary !== undefined) this.primary = step.primary;
    expect(this.heard, because).toEqual(step.heard ?? []);
    expect(this.move, `${because}: ${step.move === undefined ? 'the step names no move' : 'the move'}`).toBe(step.move ?? null);
    if (step.returns !== undefined) expect(this.returns, because).toBe(step.returns);
    expect(this.name(this.manager.primaryPosition), `${because}: the position that holds the focus`).toBe(this.focus);
    const has = [...this.nodes].filter(([, node]) => node.hasFocus).map(([name]) => name).sort();
    expect(has, `${because}: the nodes that have the focus`).toEqual(this.hasFocus);
    const owner = [...this.nodes].find(([, node]) => node === this.manager.primaryFocus)?.[0] ?? null;
    expect(owner, `${because}: the node that owns it`).toBe(this.primary);
  }

  /** A frame: the stops with the nodes each stands under, as the realizer's build would write them. */
  private attach(frame: FrameSpec): void {
    const autofocus = new Set(frame.autofocus ?? []);
    const stops = frame.stops.map((parts) => {
      const chain = parts.slice(1).map((name) => new FocusAttachment(this.node(name), this.probe(name).onFocusChange, autofocus.has(name)));
      return new TraversalStop(this.spot(parts[0]), chain);
    });
    const modal = frame.modal ? (this.node(frame.modal) as FocusScopeNode) : null;
    const modalAt = frame.modalAt ? this.spot(frame.modalAt) : null;
    this.manager.attach(stops, modal, modalAt);
  }

  /** The realizer lands where the walk answered, and reports it: leaving the page leaves nothing focused. */
  private land(move: FocusMove): void {
    this.move = move.leavesView ? 'leaves' : move.to === null ? 'clear' : `to ${this.name(move.to)}`;
    this.manager.focused(move.leavesView ? null : move.to);
  }

  private applied(move: FocusMove | null): void {
    if (move) this.land(move);
    else this.move = 'none';
  }

  private node(name: string): FocusNode {
    return this.nodes.get(name)!;
  }

  private probe(name: string): FocusProbe {
    return this.probes.get(name)!;
  }

  private spot(name: string): Spot {
    let spot = this.spots.get(name);
    if (!spot) this.spots.set(name, (spot = new Spot(name)));
    return spot;
  }

  private name(position: unknown): string | null {
    return position instanceof Spot ? position.name : null;
  }
}

describe('the focus traversal, run by its transpiled twin', () => {
  const cases = (fixture as unknown as { cases: CaseSpec[] }).cases;

  it('holds the cases the spec names', () => {
    expect(cases.length).toBeGreaterThanOrEqual(40);
  });

  for (const testCase of cases) {
    it(testCase.name, () => {
      const run = new Run(testCase);
      testCase.steps.forEach((step, index) => {
        run.step(step);
        run.check(step, `step ${index} of "${testCase.name}": ${JSON.stringify(step)}`);
      });
    });
  }
});

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

describe('a component written as an app writes one around the focus (FocusSearch, eqc output)', () => {
  // The C# an app writes (a FocusNode held in a field, a lambda added to Changed, RequestFocus from a handler,
  // Focus and FocusScope in its build) is the code that runs here: AFocusNodeOfTheAppsOwnTests builds the same shape
  // as an app is built and finds eqc refuses nothing of it.
  it('builds its Focus and FocusScope around the nodes it holds, the knobs as written', () => {
    const search = new FocusSearch();
    const column = search.build(undefined as never) as unknown as Column;
    const [focus, scope] = column.children as unknown as [Focus, FocusScope];
    expect(focus.nodeKind).toBe('focus');
    expect(focus.focusNode).toBeInstanceOf(FocusNode);
    expect(focus.autofocus).toBe(true);
    expect(scope.nodeKind).toBe('focusScope');
    expect(scope.node).toBeInstanceOf(FocusScopeNode);
    expect(scope.traversalEdgeBehavior).toBe('parentScope');
  });

  it('hears its node through the lambda added to Changed, and asks the scope for the keyboard from a handler', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const search = new FocusSearch();
    search.mount(container);
    expect(container.textContent).toContain('idle');

    const column = search.build(undefined as never) as unknown as Column;
    const [focus, scope] = column.children as unknown as [Focus, FocusScope];
    const field = focus.focusNode!;
    const results = scope.node!;
    const manager = new FocusManager(false);
    const button = new Spot('button');
    const first = new Spot('result');
    const heard: boolean[] = [];
    const frame = () =>
      manager.attach(
        [
          new TraversalStop(button, [new FocusAttachment(field, null, false)]),
          new TraversalStop(first, [new FocusAttachment(results, (hasFocus: boolean) => heard.push(hasFocus), false)]),
        ],
        null,
        null,
      );
    frame();

    manager.focused(button);
    await nextFrame();
    expect(field.hasFocus).toBe(true);
    expect(container.textContent).toContain('field');

    search.openResults();
    frame();
    const move = manager.takeRequest();
    expect(move?.to).toBe(first);
    manager.focused(move!.to);
    await nextFrame();
    expect(results.hasFocus).toBe(true);
    expect(heard).toEqual([true]);
    expect(field.hasFocus).toBe(false);
    expect(container.textContent).toContain('idle');
    container.remove();
  });
});
