#!/usr/bin/env bash
# CI's OpenSpec gate: every change and every spec validates strictly, and there IS something to
# validate. `openspec validate --all --strict` answers "No items found to validate." and exits 0 on
# an empty tree, so an openspec/ that lost its specs would pass the plain command; counting what was
# validated turns that silence into a failure.
#
#   ./scripts/check-openspec.sh
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
json="$(mktemp)"
trap 'rm -f "$json"' EXIT

status=0
"$root/scripts/openspec.sh" validate --all --strict --json > "$json" || status=$?

J="$json" node <<'JS' || status=1
const report = JSON.parse(require("fs").readFileSync(process.env.J, "utf8"));
const totals = report.summary.totals;
for (const item of report.items ?? []) {
  if (!item.valid) {
    console.error(`invalid ${item.type} ${item.id}:`);
    for (const issue of item.issues ?? []) console.error(`  ${issue.level} ${issue.path ?? ""} ${issue.message}`);
  }
}
console.log(`OpenSpec: ${totals.items} item(s) validated strictly, ${totals.failed} failed`);
if (totals.items === 0) {
  console.error("nothing was validated: openspec/ has no spec and no change, which is not a state main can be in");
  process.exit(1);
}
if (totals.failed > 0) process.exit(1);
JS

if [ "$status" -ne 0 ]; then
    # The readable report, for whoever opens the log.
    "$root/scripts/openspec.sh" validate --all --strict || true
    exit 1
fi
