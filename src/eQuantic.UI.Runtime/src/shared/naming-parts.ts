/**
 * Which nameable each label part and description part reaches, settled as the browser's lowering unwinds (#1916; C#
 * twin: `NamingParts`, whose summary says why the rule is what it is; Photon's twin: `NamedByParts`).
 *
 * A part reaches the first ancestor that IS a nameable, or that HOLDS a nameable control no nearer part already
 * reached. The walk keeps two lists in reading order, the parts that reached nothing yet and the nameable controls it
 * met, and each node takes a mark of both before its subtree is lowered: what lies past the mark when it returns is its
 * subtree's. Two candidate controls are refused rather than guessed.
 *
 * A part takes its id only when it reaches a nameable, and the nameable lists the ids in `aria-labelledby` or
 * `aria-describedby`, after any it already lists, so the browser computes the name from what the parts say.
 */
import type { HtmlNode } from '../core/types';

/** A part that reached nothing yet. */
interface Pending {
  readonly describes: boolean;
  readonly host: HtmlNode;
  readonly id: string;
}

/** A nameable control the walk met, and whether a nearer part already reached it. */
interface Candidate {
  readonly target: HtmlNode;
  claimed: boolean;
}

/** Where a node's subtree starts in the two lists. */
export interface PartsMark {
  readonly parts: number;
  readonly controls: number;
}

let pending: Pending[] = [];
let controls: Candidate[] = [];

/** A walk starts with nothing waiting, whatever an earlier walk that failed left behind. */
export function resetParts(): void {
  pending = [];
  controls = [];
}

/** The lists' ends, before a node's subtree is lowered. */
export function beginParts(): PartsMark {
  return { parts: pending.length, controls: controls.length };
}

/** A part lowered to `host`: it waits for the nameable it reaches, and a control inside it is no candidate. */
export function partLowered(at: PartsMark, describes: boolean, host: HtmlNode, id: string): void {
  controls.length = at.controls;
  pending.push({ describes, host, id });
}

/** A container that names itself: every part past the mark reaches `target`, and what it holds is no candidate outside. */
export function containerLowered(at: PartsMark, target: HtmlNode): void {
  reach(at, target);
  controls.length = at.controls;
}

/** A nameable control: a part inside it reaches it, and it is then a candidate, claimed when one inside reached it. */
export function controlLowered(at: PartsMark, target: HtmlNode): void {
  const claimed = pending.length > at.parts;
  reach(at, target);
  controls.length = at.controls;
  controls.push({ target, claimed });
}

/** A layer that is no dialog: a part inside it names nothing, and what it holds is no candidate outside it. */
export function boundaryLowered(at: PartsMark): void {
  pending.length = at.parts;
  controls.length = at.controls;
}

/** Any other node: the parts past the mark reach the one candidate past it, which is then claimed; two are refused. */
export function otherLowered(at: PartsMark, kind: string): void {
  if (pending.length === at.parts) return;
  let only: Candidate | null = null;
  let count = 0;
  for (let i = at.controls; i < controls.length; i++) {
    if (controls[i].claimed) continue;
    only ??= controls[i];
    count++;
  }
  if (count === 0) return;
  if (count > 1) throw new Error(refusal(pending[at.parts].describes, kind, count));
  reach(at, only!.target);
  only!.claimed = true;
}

/** The words every producer refuses two candidates with (C# twin: `NamingParts.Refusal`). */
export function refusal(describes: boolean, kind: string, count: number): string {
  return (
    `A ${describes ? 'DescriptionPart' : 'LabelPart'} reaches a ${kind} holding ${count} controls it could ` +
    `${describes ? 'describe' : 'name'}, and it ${describes ? 'describes' : 'names'} none of them: give each ` +
    'field its own Column or Group, with its label and its control inside (#1916).'
  );
}

/** Every part past the mark reaches `target`, in reading order, and leaves the list. */
function reach(at: PartsMark, target: HtmlNode): void {
  for (let i = at.parts; i < pending.length; i++) {
    const part = pending[i];
    part.host.attributes['id'] = part.id;
    const attribute = part.describes ? 'aria-describedby' : 'aria-labelledby';
    const listed = target.attributes[attribute];
    target.attributes[attribute] = listed ? `${listed} ${part.id}` : part.id;
  }
  pending.length = at.parts;
}
