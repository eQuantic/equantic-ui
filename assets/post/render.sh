#!/usr/bin/env bash
# Renders hero.html to hero.png. Edit the HTML, run this, commit both.
#
# Chromium's --window-size counts window chrome, so the real Chrome binary lays
# the page out in an 813px viewport when asked for 900 and silently clips the
# footer. headless_shell has no chrome to subtract, so it gives a true 900.
set -euo pipefail
cd "$(dirname "$0")"

SHELL_BIN="${CHROME:-}"
if [[ -z "$SHELL_BIN" ]]; then
  SHELL_BIN=$(ls -d /opt/pw-browsers/chromium_headless_shell-*/chrome-linux/headless_shell 2>/dev/null | head -1 || true)
fi
[[ -x "${SHELL_BIN:-}" ]] || { echo "no headless_shell found; set CHROME=/path/to/headless_shell (or Chrome's --headless=old equivalent)"; exit 1; }

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

# the logo travels inside the file: a file:// page cannot be published with a relative asset
python3 - "$TMP/render.html" <<'PY'
import base64, sys
logo = base64.b64encode(open('../Icon.png', 'rb').read()).decode()
open(sys.argv[1], 'w').write(open('hero.html').read().replace('LOGO_B64', logo))
PY

"$SHELL_BIN" --no-sandbox --disable-gpu --hide-scrollbars \
  --force-device-scale-factor=2 --window-size=1600,900 --virtual-time-budget=1500 \
  --screenshot=hero.png "file://$TMP/render.html"

echo "hero.png written ($(du -h hero.png | cut -f1), 3200x1800)"
