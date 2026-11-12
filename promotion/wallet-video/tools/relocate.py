"""relocate.py <effectcraft folder>: after EffectCraft's File > Dependencies > Collect Files, makes the collected
project open from any checkout.

Collect Files copies the media into "(Footage)" beside the project and writes every path into it
ABSOLUTE, so the project only opens where it was collected. This moves the media into `footage/`
(recordings, stills, the score) and `models/` (the device bodies), rewrites each path relative to the
project, which EffectCraft resolves against the project's own folder, and points the render queue
at `out/`, which git ignores. It fails when an absolute path is left, rather than leave a project that
opens on one machine only.
"""
import json
import os
import re
import shutil
import sys

folder = os.path.abspath(sys.argv[1])
project = os.path.join(folder, "wallet.ecproj")
collected = os.path.join(folder, "(Footage)")

text = open(project).read()

# The media, each to its folder.
if os.path.isdir(collected):
    for name in sorted(os.listdir(collected)):
        target = "models" if name.endswith(".gltf") else "footage"
        os.makedirs(os.path.join(folder, target), exist_ok=True)
        shutil.move(os.path.join(collected, name), os.path.join(folder, target, name))
        text = text.replace(json.dumps(os.path.join(collected, name))[1:-1], f"{target}/{name}")
    os.rmdir(collected)

# The render queue: whatever folder it last wrote to, the renders go to out/ beside the project.
text = re.sub(r'"(/[^"]*/)(equantic-ui-[\w-]+\.mp4)"', lambda m: f'"out/{m.group(2)}"', text)

json.loads(text)  # still a project
left = sorted(set(re.findall(r'"(/(?:Volumes|Users|private|tmp|home)/[^"]*)"', text)))
if left:
    sys.exit("absolute paths left in the project:\n  " + "\n  ".join(left))
open(project, "w").write(text)
print(project, "relative:", sorted(set(re.findall(r'"((?:footage|models|out)/[^"]+)"', text))))
