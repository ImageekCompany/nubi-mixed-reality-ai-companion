# Voice-over script

Times are positions in the final cut, from the shot list in [`../demo-script.md`](../demo-script.md). Durations assume a calm 150 words a minute (2.5 words a second); read each line aloud against a stopwatch once before the session and adjust the cut, not the pace.

Rule: **VO never plays over the user's voice or the character's speech.** Every VO line sits in a gap: the problem shot (after the hook, before the Meet greeting), the moment before the beat 2 question, the beat 3 walk, the beat 5 hotspot switch, and the end card. Nothing technical in VO: model and tool names live in lower thirds, except the stack list in V5.

| # | In | Out | Words | Line (Nubi cut) | Status |
|---|---|---|---|---|---|
| V1 | 0:05.5 | 0:17.0 | 29 | "One in four Americans 65 and older lives alone. When a new remote or a coffee machine gets the better of them, there's nobody in the room to ask." | **Mandatory** |
| V2 | 0:42.5 | 0:45.0 | 8 | "Hold something up, point at it, and ask." | Optional |
| V3 | 1:34.0 | 1:39.0 | 13 | "Ask it to follow, and it walks with you, around your real furniture." | Optional |
| V4 | 2:02.5 | 2:05.5 | 8 | "When the network drops, Nubi doesn't go silent." | Optional |
| V5 | 2:25.0 | 2:34.5 | 21 | "For anyone at home with nobody to ask. Nubi is open source, built on Muse Spark, SAM 3.1 and Muse Voice Transcribe." | **Mandatory** |

Jolly cut: V4 and V5 say "Jolly" in place of "Nubi" ([`lines-jolly.md`](lines-jolly.md)). V5 in the Jolly cut keeps "is open source" only if the build shown is the one in the repo, which it is not; use the Jolly V5 from `lines-jolly.md`.

Source for V1: 26 % of US adults 65 and older lived alone in 2023 (Pew Research Center, from Census Bureau data), linked from the README's problem statement. Say "one in four", not "a quarter of all seniors".

## Delivery

- Warm, unhurried, conversational; you are telling a friend about your grandmother, not selling. Smile slightly on V5.
- V1: a short pause after "lives alone." Land "nobody in the room to ask" softly; the cut goes to Nubi on the next word.
- Pronunciation: "Nubi" = NOO-bee; "SAM three point one"; "Muse" like the word; "NavMesh" = NAV-mesh.
- Three full reads of every line; mark the best take in the file name (`V1_take2_best.wav`).

## Recording setup

- After the recording day, in a soft room (a wardrobe full of clothes, or a sofa corner with cushions and a duvet over the back of the chair), phone flight mode, fridge and heating fans off if possible.
- USB microphone or a phone 15–20 cm from the mouth, slightly off-axis to avoid plosives. 48 kHz, 24-bit WAV if the app allows it (otherwise the phone's highest quality).
- Record 30 s of silence first (room tone).
- In the edit: high-pass at 80 Hz, gentle compression, normalise the VO stem to about −16 LUFS; the final mix sits around −14 LUFS.
