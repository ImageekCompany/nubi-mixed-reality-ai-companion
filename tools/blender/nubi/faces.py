"""Per-clip face presets (shape-key weights 0..1).

FBX takes carry bone curves only (export.py), so the face is never driven by the Animator. Unity
plays the clip and holds its preset on the blendshapes, with lip-sync and blinks layered on top.
The same table drives the clip preview sheets and is written as JSON next to the FBX.
"""

import json
import os

FACE_PRESETS = {
    "Idle": {},
    "Walk": {"Smile": 0.3},
    "Hop": {"Smile": 0.6},
    "Talk": {},                                   # lip-sync owns the mouth
    "Listen": {"Smile": 0.35},
    "Think": {"U": 0.6},
    "Wave": {"Smile": 0.8},
    "Point": {"Smile": 0.3, "Wide_Eyes": 0.3},
    "Happy": {"Smile": 1.0, "Happy_Eyes": 1.0},
    "Nod": {"Smile": 0.45},
    "ShakeHead": {"Frown": 0.5},
    "Sad": {"Frown": 1.0, "Sad_Eyes": 1.0},
    "Surprised": {"O": 1.0, "Wide_Eyes": 1.0},
    "Shrug": {"Smile": 0.25, "Wide_Eyes": 0.35},
    "Stretch": {"O": 0.7, "Happy_Eyes": 1.0},      # a yawn
    "Dance": {"Smile": 1.0, "Happy_Eyes": 0.7},
    "Jump": {"Smile": 0.7, "Wide_Eyes": 0.4},
    "Bow": {"Smile": 0.5, "Happy_Eyes": 1.0},
}


def write_json(path, shape_keys, clips):
    """Arrays of objects rather than maps, so Unity's JsonUtility reads it at runtime and in the
    importer. `loopingClips` is the single source of the Loop Time flags the Unity importer sets."""
    unknown = {k for preset in FACE_PRESETS.values() for k in preset} - set(shape_keys)
    if unknown:
        raise ValueError(f"face presets use missing shape keys: {sorted(unknown)}")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    doc = {"weightScale": "0..1 (multiply by 100 for SkinnedMeshRenderer.SetBlendShapeWeight)",
           "loopingClips": [c.name for c in clips if c.loop],
           "presets": [{"clip": clip, "weights": [{"shape": k, "weight": w} for k, w in preset.items()]}
                       for clip, preset in FACE_PRESETS.items()]}
    with open(path, "w") as fh:
        fh.write(json.dumps(doc, indent=2) + "\n")
