#!/usr/bin/env bash
# The release's fold of the served runtime's budget (#1489): every change's delta, in
# tests/eQuantic.UI.Server.Tests/Budgets/served-runtime/<change>.json, is added to the base the last
# release recorded in Budgets/served-runtime.json, and the delta files go. Run beside
# fold-ledger.sh and `public-api.sh ship`, in the release commit.
#
#   ./scripts/fold-runtime-budget.sh [root]
#
# A delta says what its bytes bought, and the fold prints each one, so the release notes can say what
# the runtime grew by and why, and writes each as a row of docs/SERVED-RUNTIME-GROWTH.md, under the
# release's version, with the pull request that added it, read from the commit that did (#1922). Every
# change that grows the runtime records a delta, so the page's rows are the deltas, and no two pull
# requests meet on one line of it between releases. A delta that is not one, with no `deltaBytes` or no
# `bought`, stops the fold before it writes anything.
set -euo pipefail

root="${1:-$(cd "$(dirname "$0")/.." && pwd)}"
budgets="$root/tests/eQuantic.UI.Server.Tests/Budgets"

python3 - "$budgets/served-runtime.json" "$budgets/served-runtime" "$root" <<'PY'
import json
import os
import re
import subprocess
import sys

base_path, folder, root = sys.argv[1], sys.argv[2], sys.argv[3]
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

def pull_request(change):
    """The pull request whose commit added the delta, by the `(#N)` a squash's subject ends with, or ''."""
    path = os.path.relpath(os.path.join(folder, change + ".json"), root)
    try:
        subject = subprocess.run(["git", "-C", root, "log", "--diff-filter=A", "--format=%s", "-1", "--", path],
                                 capture_output=True, text=True, check=False).stdout.strip()
    except OSError:
        return ""
    found = re.search(r"\(#(\d+)\)$", subject)
    return f"#{found.group(1)}" if found else ""

def version():
    """The version the release commit holds, from Directory.Build.props."""
    try:
        with open(os.path.join(root, "Directory.Build.props"), encoding="utf-8") as handle:
            found = re.search(r"<Version>([^<]+)</Version>", handle.read())
    except OSError:
        found = None
    return found.group(1) if found else "this release"

page = os.path.join(root, "docs", "SERVED-RUNTIME-GROWTH.md")
if os.path.isfile(page):
    rows = "".join(f"| `{change}` | {pull_request(change)} | {bytes:+,} | {bought.replace('|', chr(92) + '|')} |\n"
                   for change, bytes, bought in deltas)
    section = (f"\n## The deltas {version()} folded\n\n"
               f"The base went from {base['gzipBytes']:,} to {folded:,} bytes gzipped, every delta recorded since the last "
               f"release folded into it by `scripts/fold-runtime-budget.sh`, each a change's own growth (#1922).\n\n"
               "| Delta | Pull request | Gzip | What it bought |\n|---|---|---:|---|\n" + rows)
    with open(page, encoding="utf-8") as handle:
        text = handle.read()
    with open(page, "w", encoding="utf-8") as handle:
        handle.write(text.rstrip("\n") + "\n" + section)

with open(base_path, "w", encoding="utf-8") as handle:
    handle.write(f'{{\n  "gzipBytes": {folded},\n  "measuredWith": "{base["measuredWith"]}"\n}}\n')
for change, _, _ in deltas:
    os.remove(os.path.join(folder, change + ".json"))
PY
