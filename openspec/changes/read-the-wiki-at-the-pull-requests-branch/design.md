# Design

## The listing decides, and an exact name

The script lists the wiki's branches once and compares each ref with `refs/heads/<head ref>` in the
shell. `git ls-remote <url> <pattern>` would have been shorter, and it matches a pattern against the
TAIL of a ref, so a pull request named `some-change` would have read another one's
`fix/some-change`. A `grep -q` over the listing was the other short form, and under `pipefail` it can
fail its own pipeline by stopping at the first match, which reads master by mistake.

## Fail rather than read master

When the branches cannot be listed, the checkout fails. Falling back to master would read the wrong
pages for exactly the pull requests this change exists for, and would pass or fail their guard for a
reason nobody could see. The step keeps `continue-on-error`, as before, and the guards already fail
loudly on CI when no wiki is beside the repository (`WikiExpected`), so a failed checkout is reported
by the tests that needed it.

## The self-test runs the script as CI does

Each case runs the script in a process of its own. Called as a function inside an `if`, bash turns
`set -e` off in it, and the first version of the test ran a different script from the one CI runs:
a failing clone went on to the next line. The fixture is a local repository reached through
`file://`, and the listing failure is a `git` on the PATH that refuses only `ls-remote`, so the one
case that must not fall back is exercised without a network. The job runs on Linux only, where the
fixture's paths mean what they say; the checkout itself runs on all three runners.

## The merge into master stays a step of the flow

Merging a pull request's wiki branch after the pull request merges could be automated, but pushing to
the wiki from a workflow needs a credential and a setting the repository does not have, and forgetting
the step is loud: the next run on main, and every pull request without a wiki branch of its own, reads
master without the rows and fails its guard. The step is written where the merge is, in the Workflow
section's step 4.

## One locator, and a named wiki fails rather than skips

Three test files read the wiki, in four places, each with its own copy of "beside the repository".
A variable that only some of them honoured would have been worse than none: one guard reading the
shared clone while the others read the override. So there is one locator, and `EQ_WIKI_DIR` is read
there. When the variable is set, a guard never skips: a missing directory, or one without the wiki's
`Home.md`, fails every guard, because a typo would otherwise turn each of them into a green run that
read no page. The version-mark guard used to return in silence when the clone was missing, on CI
too; through the locator it now fails on CI like its two siblings.
