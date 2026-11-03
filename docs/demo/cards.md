# Cards, lower thirds and upload text

Times are positions in the final cut ([`../demo-script.md`](../demo-script.md) shot list). Style: sans serif (Inter or SF Pro), white on 60 % black for lower thirds, bottom-left, never over the in-app captions or the mask outline. Every number on screen comes from the chosen take's logcat (or its latency HUD, if stats were turned on for it), written in the rehearsal log; `___` marks a number to fill from the take.

## Title card (0:17–0:23, over the first-person Meet shot: the character notices you and says hello)

| | Nubi cut | Jolly cut |
|---|---|---|
| Title | **Nubi** | **Nubi** (small type under the subtitle: "shown in a fan-made Jolly-style skin") |
| Subtitle | A companion that can see your world. | A companion that can see your world. |

The Jolly cut keeps "Nubi" as the product name: the project, the repo and the build are Nubi, and the Jolly look-alike is a local skin (README "Character and the Jolly question").

## Corner tag (all first-person footage)

> Quest 3 · real time · one take per beat

## Lower thirds

| # | In–out | Beat | Text (Nubi cut; the Jolly cut replaces "Nubi" with "Jolly" where it appears) |
|---|---|---|---|
| L1 | 0:24–0:28 | 1 | **Muse Voice Transcribe** · live captions as you speak |
| L2 | 0:30–0:35 | 1 | Matched on the device → `come_here` · reply in ___ ms · walks your scanned floor |
| L3 | 0:50–0:54 | 2 | **SAM 3.1** · your pointing hand is the prompt · outline in ___ ms |
| L4 | 0:55–0:59 | 2 | **Muse Spark** vision · same frame, in parallel · answer streams as it speaks |
| L5 | 1:11–1:15 | 2 | **Muse Spark** calls `highlight_part` → **SAM 3.1** text prompt "power button" · same frame |
| L6 | 1:33–1:37 | 3 | **Muse Spark** tool call: `follow_user` · "Stop" handled on the device |
| L7 | 1:55–1:59 | 4 | Interrupted with a pinch · silent in ___ ms |
| L8 | 2:08–2:12 | 5 | Offline: baked lines, still lip-synced, still idling |
| L9 | 2:18–2:22 | 6 (over the usage-meter picture-in-picture) | This take: $___ · total $___ of a $20 hard cap |

Nine lower thirds in 2:37, at most one on screen at a time. L2, L3 and L7 carry the take's own numbers: `evt=turn.latency` (release → first audio), `evt=vision.timing` (tool start → mask shown), and the barge-in silence from the same lines. If a number is missing for that take, drop the number, keep the text. L9: the in-app meter is lifetime, so "this take" is after − before from the rehearsal log. L3–L5 name SAM 3.1 and Spark vision; the app also shows them as model badges ("SAM 3.1", "Muse Spark · vision") since the vision path raises its own model calls. Keep the lower thirds anyway: they are easier to read than a badge.

## End card (2:24–2:37)

Nubi cut:

> **Nubi**
> For anyone at home with nobody to ask.
>
> Muse Spark · SAM 3.1 · Muse Voice Transcribe · Wit.ai
> Unity 6 · OpenXR · Quest 3
>
> Open source (MIT) · github.com/___/___
> Meta AI Developer Hackathon 2026 · Indie track

Jolly cut: the same, plus a last line in small type:

> The Jolly-style skin is a fan-made look-alike shown with ___ (permission); the open-source release ships Nubi.

Background: the README voice-turn diagram, dimmed to 30 %, with the character's wave on the right if the take has one.

## YouTube

**Title (≤ 70 characters):** `Nubi: an MR companion that sees what you hold (Meta Quest 3)`

**Description:**

```
Nubi is a mixed-reality companion for Meta Quest 3. It lives in your real room, comes when you call it,
outlines the object you point at and tells you how to use it, and keeps you company when the Wi-Fi drops.

In the US, one in four adults aged 65 and older lives alone (Pew Research Center, 2023). Nubi is built
for the moment a new remote or coffee machine gets the better of someone, with nobody in the room to ask.

Every beat is one continuous take on a Quest 3, in real time. Uncut run: <second video link>

Built on Meta's Muse Spark (Responses API with tool calling and vision), SAM 3.1 (hand-pointed and
text-prompted segmentation) and Muse Voice Transcribe (realtime), with Wit.ai speech.
Open source (MIT), Unity 6 + OpenXR: <repo URL>

Built for the Meta AI Developer Hackathon 2026, Indie track.
"Meta", "Muse", "Quest" and "SAM" are Meta's marks; this project is not affiliated with or endorsed by Meta.

Music: <track title> by <artist>, <URL>, <licence, e.g. CC0>.

0:00 The problem
0:23 "Nubi, come here."
0:42 "What is this?"
1:30 Follow me / Stop
1:47 Interrupting
2:02 Offline
2:24 How it's built
```

**Thumbnail:** the beat 2 frame with the power-button outline on the real remote, the character in the corner, and three words: "It sees this."
