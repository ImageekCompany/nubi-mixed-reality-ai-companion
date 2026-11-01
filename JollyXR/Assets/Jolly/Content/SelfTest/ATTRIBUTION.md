# Self-test content provenance

| File | Source | Licence |
|---|---|---|
| `still_mug.bytes` | JPEG of "Coffee Mug (Unsplash).jpg" by Ryan Holloway, https://commons.wikimedia.org/wiki/File:Coffee_Mug_(Unsplash).jpg, downscaled to 640×360 (same file as `Tests/Fixtures/images/mug_640.jpg`) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| `greeting.wav`, `come_here.wav`, `what_is_this.wav`, `long_answer.wav` | Synthesised for this project with macOS `say -v Samantha -r 170 --data-format=LEI16@24000` (24 kHz mono 16-bit). Texts: "Hi Nubi, who are you?", "Nubi, come here.", "What is this, and how do I use it?", "Nubi, please tell me a long story about the sea." | Generated test audio; no third-party recording |

The still is the camera fallback of `tools/device/selftest.sh` when the room is too dark, and the Editor's camera frame.
