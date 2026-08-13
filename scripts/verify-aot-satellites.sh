#!/usr/bin/env bash
# Track L W6: publish the AOT harness and run the native binary — the proof that satellite
# assemblies and culture data survive a NativeAOT publish of the component library. Exits nonzero
# on the first untranslated answer (the harness compares against expected TRANSLATIONS, because a
# dropped satellite does not crash — it silently answers English).
set -euo pipefail

cd "$(dirname "$0")/.."

case "$(uname -sm)" in
  "Darwin arm64") RID=osx-arm64 ;;
  "Darwin x86_64") RID=osx-x64 ;;
  "Linux x86_64") RID=linux-x64 ;;
  "Linux aarch64") RID=linux-arm64 ;;
  *) echo "unsupported host: $(uname -sm)" >&2; exit 2 ;;
esac

OUT=tests/eQuantic.UI.Aot.Harness/bin/publish-$RID
dotnet publish tests/eQuantic.UI.Aot.Harness/eQuantic.UI.Aot.Harness.csproj \
  -c Release -r "$RID" -o "$OUT" --nologo -v quiet

"$OUT/eQuantic.UI.Aot.Harness"
