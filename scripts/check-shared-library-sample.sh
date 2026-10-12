#!/usr/bin/env bash
# What the shared-library sample's build WROTE (#624): a page that uses a class library the app
# references, read after `dotnet build samples/SharedLibrary/SharedLibrary.Web`.
#
# The build's exit code cannot say it. Before the fix this sample built green and its page named
# the library's types with nothing importing them: `new Cursor(0)` in a module that defined no
# Cursor, which the browser met as a ReferenceError and the build never mentioned. A step that only
# builds would pass on that again, so this reads the files: the list of projects the SDK handed the
# compiler, the module of every type the page reaches, the one the page must NOT reach, and the
# string catalogs of two projects that each have a `Strings`.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# SHARED_LIBRARY_SAMPLE points the check at another copy of the built app: a copy with one file
# taken away or changed is how each case below was shown to fail.
web="${SHARED_LIBRARY_SAMPLE:-$ROOT/samples/SharedLibrary/SharedLibrary.Web}"
modules="$web/obj/eQuantic/ts"
output="$web/wwwroot/_equantic"

status=0
fail() {
    echo "::error::$1"
    status=1
}

if [ ! -d "$modules" ] || [ ! -f "$output/HomePage.js" ]; then
    echo "::error::the sample is not built: no modules at $modules or no bundle at $output/HomePage.js. Run: dotnet build samples/SharedLibrary/SharedLibrary.Web"
    exit 1
fi

# THE SDK FOUND THE PROJECTS BY ITSELF: the library the app references and the one that library
# references, and none of the SDK's own, which the app references too beside the source tree.
projects=$(find "$web/obj" -name equantic.projects.txt | sort | head -1)
if [ -z "$projects" ]; then
    fail "the SDK wrote no equantic.projects.txt for the sample, so the compiler was handed no referenced project"
else
    for project in SharedLibrary.Common SharedLibrary.Domain; do
        grep -q "|.*/$project/$project.csproj\$" "$projects" \
            || fail "$projects does not list $project, a project the app references"
    done
    listed=$(grep -c . "$projects")
    if [ "$listed" -ne 2 ]; then
        fail "$projects lists $listed projects where the app's own are 2: the SDK's projects and icon packs are read another way"
        cat "$projects"
    fi
fi

# EVERY TYPE THE PAGE REACHES IS A MODULE, in whichever project: a static class, a record, a
# component, a record a Server Action answers, and the record of the second project inside it.
#
# Imprint is INTERNAL to the library, which opens it to the app by its ASSEMBLY's name
# (InternalsVisibleTo), and the app's project file names its assembly apart from itself. So Imprint
# is a module only when the SDK handed the compiler the name MSBuild evaluated: under the project
# file's name the friend declaration opens nothing, and the page that builds it does not compile.
for type in Palette Cursor Wordmark Receipt Money Imprint; do
    [ -f "$modules/$type.ts" ] || fail "no module was written for $type, a type of a referenced project the page uses"
done
for type in Palette Cursor Wordmark Receipt Imprint; do
    grep -q "^import { $type } from \"./$type\";" "$modules/HomePage.ts" \
        || fail "HomePage.ts does not import $type, which it names"
done
grep -q "^import { Money } from \"./Money\";" "$modules/Receipt.ts" 2>/dev/null \
    || fail "Receipt.ts does not import Money, the record of the project it references"

# AND NOTHING ELSE OF THEM: the page names ReceiptArchive only in a Server Action's body, which
# does not cross. It reads a file, so compiling it would have failed the build above.
[ ! -f "$modules/ReceiptArchive.ts" ] || fail "ReceiptArchive was compiled for the browser, and no browser code names it"
if grep -q "ReceiptArchive" "$output/HomePage.js"; then
    fail "the bundle names ReceiptArchive, a class the browser cannot run"
fi

# TWO CATALOGUES OF ONE CLASS NAME, each read by its own id, in every culture, with the keys read
# and no others: the library's catalogue has a third key that no page reads.
for culture in neutral pt-BR pt-PT; do
    catalog="$output/strings/$culture.json"
    if [ ! -f "$catalog" ]; then
        fail "no string catalog at $catalog"
        continue
    fi
    for key in "SharedLibrary.Web.Resources.Strings/Title" "SharedLibrary.Common.Resources.Strings/Title" "SharedLibrary.Common.Resources.Strings/Brand"; do
        grep -q "\"$key\":" "$catalog" || fail "$catalog does not hold $key"
    done
    if grep -q "Strings/Unused\"" "$catalog"; then
        fail "$catalog holds the library's Unused, a key no page reads"
    fi
done

if [ "$status" -eq 0 ]; then
    echo "The shared-library sample's page holds what it uses of its library, and nothing else of it."
fi
exit $status
