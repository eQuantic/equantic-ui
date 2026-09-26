# Design

## How Flutter answers it

It has no bearing here: this is a rule about reviewing pull requests, not a mechanism of the
framework, and docs/FLUTTER-PARITY.md has no row for it.

## Decisions

- **Severity decides, and a ceiling bounds it.** Severity alone keeps a pull request with a real
  defect in every round (like #418) in the loop for as long as Copilot keeps finding them. The
  ceiling bounds that, and a defect found after it is still fixed and proved, only without asking
  Copilot again.
- **The local review is what makes the ceiling safe.** Rounds one to three carried 48% of the
  findings, so the other half has to be found another way. A full review of the diff before the
  pull request opens finds the batch at once, where Copilot reveals it a little at a time.
- **A round is requested, never implied by a push.** With `review_on_push` on, every push started a
  round, a merge from main and a one-line body edit included. With it off, the author asks for a
  round after one push that carries every fix of the previous round.
