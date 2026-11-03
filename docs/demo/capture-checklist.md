# Capture checklist (print this)

Recording day: Tue Nov 3, 12:00–16:30 CET. Beats and fallbacks: [`../demo-script.md`](../demo-script.md). Plan: [`production-plan.md`](production-plan.md). Character for this session: ☐ Nubi ☐ Jolly (per the Jolly decision).

Build: commit `________` · APK SHA-256 `________` · capture method: Quest system recorder (passthrough verified in its recordings 2026-11-01; `adb screenrecord` and scrcpy show no passthrough)

## Pre-flight (30 min before the first take)

**Room**
- [ ] Space Setup done in this room today; 3 m of floor clear; the sofa is where Nubi walks around
- [ ] Privacy sweep: no photos, mail, addresses, screens or names in view of either camera
- [ ] Ceiling lights + sofa lamp on, curtains half drawn, no bright window behind the remote position
- [ ] Phones silent, washing machine and other noise off, windows shut
- [ ] Props: TV remote (batteries in, red power button) **tested on the TV from the hero position**; TV off; backup moka pot within reach off-frame
- [ ] Headset on the phone hotspot; the Mac and the other phone on home Wi-Fi

**Headset**
- [ ] Charged > 80 %, USB-C power bank nearby
- [ ] Rebooted (ADR-0034); then `tools/demo/record.sh props 1920x1080 30 20000000` (sets and reads back `debug.oculus.capture.*`; lost on reboot)
- [ ] Release APK installed (SHA-256 matches above), secrets pushed
- [ ] Do Not Disturb on; no pending system update
- [ ] Storage: at least 10 GB free (`adb shell df -h /sdcard`)

**App**
- [ ] Launched; `evt=character.selected id=________` in logcat matches this session's character
- [ ] Warm-up greeting said off camera ("Hi Nubi, who are you?" / "Hi Jolly, who are you?")
- [ ] One capture of the remote at the hero position: no `evt=vision.too_dark`; luma written: ____
- [ ] Settings (left palm toward you → wrist button, left ☰, or "open settings"): Clear memory · persona Companion · captions Large · latency stats off · panel closed again (X, or it closes itself after 20 s and on every talk pinch)
- [ ] Usage meter start value: $______

**Other cameras and audio**
- [ ] Phone on tripod: 4K 30 fps, exposure and white balance locked, framing per shot below, 20+ GB free
- [ ] Room recorder running (second phone or lav)
- [ ] MQDH cast on the Mac as the monitor (and recording, if it is the capture method)

## Per take

- [ ] Start headset recording **in the headset** (universal menu → Camera → Record video; adb cannot start it) → start phone → start room recorder → **one clap in view of the headset and the phone**
- [ ] Hold still 2 s, then the beat; hold still 2 s after the character finishes
- [ ] Stop all three; `tools/demo/record.sh pull` and `tools/demo/check-frame.sh artifacts/demo/raw/<file>.mp4` (FAIL = black behind the character: stop and escalate); write the take in the log below with ✓ / ✗ and the reason
- [ ] Between takes: `tools/demo/reset.sh --warmup` (force-stop, memory cleared, relaunch, waits for the warm-up), then the throwaway greeting

## Shots

| ☐ | Shot | Time in cut | Camera | Action / line | Must see | Takes kept |
|---|---|---|---|---|---|---|
| ☐ | C1 Problem wide | 0:05–0:17 | Phone, wide | Person on the sofa, **TV dark**, turning the remote over, unsure | Real person, real room, the dark TV, the remote | |
| ☐ | C2 Meet | 0:17–0:23 | Headset | Look at the room for 20 s, then at the character on the floor for 1.5 s: it turns and greets you | Passthrough room + character; the greeting with caption; title space at the top | |
| ☐ | B1 Come here | 0:23–0:42 | Headset (+ phone wide) | Character beyond the sofa. Pinch-hold, hand in frame: "Nubi, come here." / "Jolly, come here." Release | Caption reads the name correctly; baked reply at once; walks *around* the sofa; stops ~1 m in front; no gaze greeting mid-beat | |
| ☐ | B2 What is this (+ hook) | 0:42–1:30, hook 0:00–0:05 | Headset | Remote at chest height, 40–50 cm, point with the other hand. Pinch 1: "What is this?" Hold still to the end. Pinch 2: "Which button turns it on?" Lean in, hold 3 s. Turn to the TV, press the button | Object outline ≤ 2 s on the remote (not offset); answer streams; **power-button outline**; **TV comes on** in the same take | |
| ☐ | B2-OTS Over the shoulder | (PiP 1:20–1:28) | Phone | Same take as B2, framed on the hand, the remote and the TV | Real hand presses the real button; TV comes on | |
| ☐ | B3 Follow / Stop | 1:30–1:47 | Headset + phone wide | "Follow me." Walk ~3 m slowly, look back over the shoulder. "Stop." | Follows at 1–1.5 m; stops at once | |
| ☐ | B4 Barge-in | 1:47–2:02 | Headset | "Tell me everything about this remote." Pinch mid-sentence: "That's enough, thanks." | Audio cuts; listening; short reply | |
| ☐ | B5 Offline | 2:02–2:24 | Headset | Hold the phone in view, switch its hotspot off. Wait for the Offline pill. Pinch: "Are you there?" Hotspot on, wait for the pill to clear, pinch: "Thanks, Nubi. See you later!" / "Thanks, Jolly. See you later!" | Offline pill; baked line with lip-sync; pill gone; warm online reply (wave is a bonus) | |
| ☐ | B6 Cost | PiP 2:18–2:22 | Headset | Open the settings panel (wrist button, left ☰ or "open settings"), look down at the usage line 4 s: at 0.7 m the panel is taller than the recorded frame, so frame the bottom rows; touch a control if it takes longer than 20 s | Legible $ value; before/after values in the log | |
| ☐ | FULL-1 Uncut run | (second video) | Headset + phone | Beats 1–5 in one take, no stops | Everything above, unbroken | |
| ☐ | FULL-2 Uncut run | (spare) | Headset + phone | Same | | |
| ☐ | X1 Hotspot switch close-up | cutaway | Headset | The phone's hotspot toggle filling the view, seen through passthrough | Legible toggle | |
| ☐ | X2 Room establishing | b-roll | Phone | Slow pan of the room, empty | | |
| ☐ | Room tone | — | Room recorder | 30 s silence | | |

Keep going on each beat until 2 clean takes exist (at least 3 attempts each). Between takes: the adb reset in `../demo-script.md` Setup, then the warm-up turn.

## After the session

- [ ] Usage meter end value: $______ → `docs/api-spend.md` row and rehearsal log
- [ ] All headset files pulled to `artifacts/demo/raw/` (git-ignored; `tools/demo/record.sh list` / `pull`) and backed up to a second disk
- [ ] Logcat of the session saved next to them; per kept take: release → first audio ___ ms, mask ___ ms, barge-in silence ___ ms
- [ ] Phone and room-recorder files copied, named by take
- [ ] Capture props noted (they reset on the next reboot)

## Take log

| Take | Shot | Time | Headset file | Phone file | ✓ / ✗ | Note (failure, numbers) |
|---|---|---|---|---|---|---|
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |

## QC before upload (Wed Nov 4)

- [ ] Length ≈ 2:37, ≤ 2:45 (or the portal limit: ______)
- [ ] Hook in the first 5 s; the problem VO ends by 0:17; the hero power-button outline holds ≥ 3 s; the TV coming on is in frame
- [ ] No cut or speed change inside a beat; every on-screen number matches the take log
- [ ] Watched muted on a phone: the VO is burned in, in-app captions legible
- [ ] Watched on headphones: no clipping, no hum; loudness about −14 LUFS, true peak ≤ −1 dBTP (`ffmpeg -i final.mp4 -af loudnorm=print_format=summary -f null -`)
- [ ] SRT placeholders all replaced with what was said; captions re-timed to the cut
- [ ] No secrets, terminal windows, addresses or unconsented faces in any frame
- [ ] Character matches the Jolly decision; the Jolly cut has its end-card line
- [ ] Music licence written in the description (or no music)
- [ ] Export: H.264, 1920×1080, 30 fps, ~16 Mbps, AAC 48 kHz; `ffprobe` checked
- [ ] Uploaded unlisted, plays in HD in a private window, captions attached, thumbnail set
