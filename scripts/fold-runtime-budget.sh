#!/usr/bin/env bash
# The release's fold of the served runtime's budget (#1489): every change's delta, in
# tests/eQuantic.UI.Server.Tests/Budgets/served-runtime/<change>.json, is added to the base the last
# release recorded in Budgets/served-runtime.json, and the delta files go. Run beside
# fold-ledger.sh and `public-api.sh ship`, in the release commit.
#
#   ./scripts/fold-runtime-budget.sh [root]
#
# A delta says what its bytes bought, and the fold prints each one, so the release notes can say what
# the runtime grew by and why. A delta that is not one, with no `deltaBytes` or no `bought`, stops the
# fold before it writes anything.
set -euo pipefail

root="${1:-$(cd "$(dirname "$0")/.." && pwd)}"
budgets="$root/tests/eQuantic.UI.Server.Tests/Budgets"

python3 - "$budgets/served-runtime.json" "$budgets/served-runtime" <<'PY'
import json
import os
import sys

base_path, folder = sys.argv[1], sys.argv[2]
with open(base_path, encoding="utf-8") as handle:
    base = json.load(handle)

deltas = []
if os.path.isdir(folder):
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".json"):
            continue
        with open(os.path.join(folder, name), encoding="utf-8") as handle:
            delta = json.load(handle)
        if not isinstance(delta.get("deltaBytes"), int) or not str(delta.get("bought", "")).strip():
            sys.exit(f"{name} is not a delta: it needs an integer deltaBytes and a bought that says what they bought")
        deltas.append((name[:-len(".json")], delta["deltaBytes"], delta["bought"]))

if not deltas:
    print(f"No delta to fold: the budget stays at {base['gzipBytes']:,} bytes.")
    sys.exit(0)

folded = base["gzipBytes"] + sum(bytes for _, bytes, _ in deltas)
for change, bytes, bought in deltas:
    print(f"  {bytes:+,} bytes  {change}: {bought}")
print(f"Folded {len(deltas)} delta(s): {base['gzipBytes']:,} -> {folded:,} bytes.")

with open(base_path, "w", encoding="utf-8") as handle:
    handle.write(f'{{\n  "gzipBytes": {folded},\n  "measuredWith": "{base["measuredWith"]}"\n}}\n')
for change, _, _ in deltas:
    os.remove(os.path.join(folder, change + ".json"))
PY
