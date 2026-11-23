/**
 * The arena, written once (#1563): every case of `gesture-arena.fixture.json` runs here against the transpiled twin
 * of the C# `GestureRouter`, which the browser runs, as `GestureArenaFixtureTests` runs it against the C# Photon
 * runs. The members are the transpiled twins of the suite's `ArenaProbe`, a recognizer written as an app writes one,
 * and `ArenaMember`, a gesture of the SDK's: so this also executes an app's subclass of the runtime's
 * `GestureRecognizer`, call for call, against the same sequence as its C#.
 */
import { describe, expect, it } from 'vitest';
import fixture from './gesture-arena.fixture.json';
import { GestureRouter } from './components/GestureRouter';
import { GestureRecognizerFactoryWithHandlers } from './components/GestureRecognizerFactoryWithHandlers';
import type { GestureArenaEntry } from './components/GestureArenaEntry';
import type { GestureRecognizer } from './components/GestureRecognizer';
import { ArenaProbe } from './__fixtures__/ArenaProbe';
import { ArenaMember } from './__fixtures__/ArenaMember';
import { Point, PointerEvent } from './value-types';
import type { PointerPhaseValue } from './enums.generated';

type Disposition = 'accepted' | 'rejected';

/** A member of the arena: the C# interface `IGestureArenaMember` has no twin, an interface being erased. */
type IGestureArenaMember = ArenaMember | GestureRecognizer;

interface MemberSpec {
  name: string;
  plain?: boolean;
  origin?: number[];
  deadline?: number;
  atDeadline?: Disposition;
  onAdd?: Disposition;
  onMove?: Disposition;
  primaryOnly?: boolean;
}

interface StepSpec {
  heard: string[];
  next?: number | null;
  down?: number[];
  buttons?: number;
  move?: number[];
  up?: number[];
  cancel?: number[];
  tick?: number;
  rebuild?: boolean;
  stop?: string;
  resolve?: string;
  as?: Disposition;
}

interface CaseSpec {
  name: string;
  members: MemberSpec[];
  built: string[];
  steps: StepSpec[];
}

function event(phase: PointerPhaseValue, x: number, y: number, buttons: number): PointerEvent {
  return new PointerEvent(phase, new Point(x, y), 'touch', buttons, 0, 1, new Point(0, 0), 0);
}

/** One case: the members, the router and the detectors it keeps, and what was heard. */
class Run {
  readonly heard: string[] = [];
  readonly router = new GestureRouter();
  private readonly probes = new Map<string, ArenaProbe>();
  private readonly plain = new Map<string, IGestureArenaMember>();
  private readonly entries = new Map<string, GestureArenaEntry>();
  private build = 0;

  constructor(private readonly members: MemberSpec[]) {
    for (const member of members) if (member.plain) this.plain.set(member.name, new ArenaMember(member.name, this.heard));
    this.rebuild();
  }

  step(step: StepSpec): void {
    if (step.down) this.down(step.down, step.buttons ?? 1);
    else if (step.move) this.route('move', step.move, 1);
    else if (step.up) this.route('up', step.up, 0);
    else if (step.cancel) this.route('cancel', step.cancel, 0);
    else if (step.tick !== undefined) this.router.tick(step.tick);
    else if (step.rebuild) this.rebuild();
    else if (step.stop) this.probes.get(step.stop)!.stop(1);
    else if (step.resolve) {
      const probe = this.probes.get(step.resolve);
      if (probe) probe.say(step.as!);
      else this.entries.get(step.resolve)!.resolve(step.as!);
    } else throw new Error(`A step the runner does not know: ${JSON.stringify(step)}`);
  }

  /** Each detector is built again: the router keeps its recognizer and hands it this build's callback. */
  private rebuild(): void {
    const build = ++this.build;
    for (const member of this.members) {
      if (member.plain) continue;
      const factory = new GestureRecognizerFactoryWithHandlers<ArenaProbe>(
        () => {
          this.heard.push(`${member.name}.create`);
          return new ArenaProbe(
            member.name,
            this.heard,
            member.deadline ?? 0,
            member.atDeadline ?? null,
            member.onAdd ?? null,
            member.onMove ?? null,
            member.primaryOnly ?? false,
          );
        },
        (probe: ArenaProbe) => {
          probe.onDeadline = (who: string) => this.heard.push(`${who}.called by build ${build}`);
        },
      );
      this.probes.set(member.name, this.router.recognizers(member.name, [factory])[0] as ArenaProbe);
    }
  }

  private down(at: number[], buttons: number): void {
    const members: IGestureArenaMember[] = this.members.map((member) =>
      member.plain ? this.plain.get(member.name)! : this.probes.get(member.name)!,
    );
    const entries = this.router.down(event('down', at[0], at[1], buttons), members, this.inBox('down', at, buttons));
    this.members.forEach((member, i) => {
      const entry = entries[i];
      if (entry && member.plain) this.entries.set(member.name, entry);
    });
  }

  private route(phase: PointerPhaseValue, at: number[], buttons: number): void {
    this.router.route(event(phase, at[0], at[1], buttons), this.inBox(phase, at, 0));
  }

  /** The event in a detector's box: the detector is named like its probe, and its box starts at its origin. */
  private inBox(phase: PointerPhaseValue, at: number[], buttons: number): (position: unknown) => PointerEvent {
    return (position: unknown) => {
      const origin = this.members.find((member) => member.name === position)?.origin ?? [0, 0];
      return event(phase, at[0] - origin[0], at[1] - origin[1], buttons);
    };
  }
}

describe('the gesture arena, run by its transpiled twin', () => {
  const cases = (fixture as { cases: CaseSpec[] }).cases;

  it('holds the cases the spec names', () => {
    expect(cases.length).toBeGreaterThanOrEqual(14);
  });

  for (const testCase of cases) {
    it(testCase.name, () => {
      const run = new Run(testCase.members);
      expect(run.heard).toEqual(testCase.built);
      testCase.steps.forEach((step, index) => {
        run.heard.length = 0;
        run.step(step);
        expect(run.heard, `step ${index}: ${JSON.stringify(step)}`).toEqual(step.heard);
        if (step.next !== undefined) expect(run.router.nextDeadlineMs ?? null, `the next deadline after step ${index}`).toBe(step.next);
      });
    });
  }
});
