# Demo script

The demo video runs **2:45 or less**, which is our working cap until the portal's real limit is confirmed (`docs/research/hackathon.md` §1). It shows exactly the six required beats of `definition-of-done.md` §6, and everything else is b-roll or README. The hero user is an older adult at home, and the hero object is a **TV remote** (backups: moka pot, thermostat). We never use medication.

Rule for recording: record a beat only after 10 consecutive clean runs of all six required beats (no crash, no hang, no manual restart). Log every failure in the [rehearsal log](#rehearsal-log).

## Setup (before any take)

- **Room.** A lit living room, Space Setup done in *this* room, and a sofa or table that Nubi has to walk around. The floor must be clear for about 3 m.
- **Headset.** Charged above 80 %, release APK installed (`tools/device/install-run.sh artifacts/Nubi.apk`), and `secrets.json` pushed (`tools/device/push-secrets.sh`).
- **Reboot the headset (ADR-0034)** before the session; the measured post-reboot runs idled 20 min after it. Stale frames follow the headset's uptime, not the build: after 4 days of uptime idle dropped 5.0 % of frames, after a reboot 0.23–0.32 % (`docs/perf/selftest-report.md` "Stale frames are device state…").
- **Warm-up turn.** After launch, say one throwaway greeting ("Hi Nubi, who are you?"), then Settings → Clear memory. The first turn after launch opens cold ASR and TTS connections, and a greeting is the slowest turn type anyway (a full spoken answer): to first audio it took 3.0–3.3 s in the three `f311081` runs and 2.4–4.7 s in the three `36778c1` runs, against 1.0–3.5 s for the other `f311081` turns, and greetings were p50 2,362 ms even mid-session in the 20-turn run (`751acba`). Beat 1 must be neither the first turn nor a greeting.
- **App state.** Settings → Clear memory. Persona: Companion. Caption size: largest. Check the usage meter and write down its starting value.
- **Capture.** The capture method was decided in the R1 check (Quest system recorder, or scrcpy / MQDH cast as the fallback): ________. Record the passthrough and app layers together, plus a separate room-mic track for the voice-over.
- **Network.** Home Wi-Fi, with the headset's quick-settings Wi-Fi toggle within reach for beat 5.
- **Props.** The TV remote on the sofa arm, and the backup object nearby.

## Shot list

| t | Beat | Shot | You say / do | Nubi does (what must be visible) | Meta tooling on screen |
|---|---|---|---|---|---|
| 0:00–0:10 | Cold open | Wide MR shot of the room. Nubi idles on the floor, blinks, and turns its head toward you. | Title card: **"Nubi: a companion that can see your world."** | Idle, blink, look-at | — |
| 0:10–0:40 | **1. Come here** | First-person view. Pinch-hold. | "Nubi, come here." Release. | The live caption reads "Nubi", not "newbie". An acknowledgement sound plays. The walk starts at once: the local guard calls `come_here` before the model (ADR-0036; it matches "Hi Nubi, come here." too). The spoken answer follows with lip-sync and a caption in about 2 s (2.1–2.2 s in the `36778c1` runs). Nubi walks *around* the furniture and stops about 1 m in front of you. | Muse Voice Transcribe (live partials), Spark badge, `come_here` badge, latency HUD |
| 0:40–1:15 | **2. What is this?** | Pick up the remote, hold it at chest height, point at it with the other hand. Pinch-hold. | "What is this, and which button turns it on?" | The mask outline appears on the real remote within 2 s and the answer is spoken as it streams. Then `highlight_part("power button")` outlines the actual button on the same capture. Nubi looks at the remote. Two SAM prompt types on screen: a pointed object, then a text-prompted part. | `capture_and_identify` + `highlight_part` badges, **SAM 3.1** + **Spark** badges, mask outlines |
| 1:15–1:35 | **3. Follow me / Stop** | Wider shot. Walk about 3 m. | "Follow me." Walk. Then: "Stop." | Nubi follows at 1–1.5 m, then stops *immediately* on "Stop" (handled on the device, before any model call). | `follow_user` badge, `stop` badge |
| 1:35–1:55 | **4. Barge-in** | Medium shot. | "Tell me everything about this remote." Pinch in the middle of a sentence. | The audio fades out in ≤ 150 ms and Nubi switches to listening (self-test: silent in 28–55 ms over the `751acba`, `f311081` and `36778c1` runs). | Latency HUD shows the barge-in |
| 1:55–2:22 | **5. Offline** | Show the Wi-Fi toggle going off in quick settings. Pinch. | "Are you there?" *Optional, only once a wearer has confirmed it on the headset:* hold an open palm toward Nubi. Turn Wi-Fi back on and pinch: "Thanks, Nubi!" | An offline line plays from the baked clip with lip-sync. Idle animations carry on. (Optional: the palm triggers a greeting line.) After Wi-Fi returns, the next turn is online. | Status indicator: Offline → Online |
| 2:22–2:34 | **6. Cost** | Open Settings → usage. Cut to `docs/api-spend.md` on a monitor. | VO: "Every call is metered against a hard cap. This session cost ___." | The usage meter matches the ledger within 10 %. | Usage meter |
| 2:34–2:45 | Close | Architecture card (README voice-turn diagram), repo URL. | VO: "Open source. OpenXR. Built on Muse Spark, SAM 3.1 and Muse Voice Transcribe." | Nubi waves | — |

### Lines (voice-over, optional)

- 0:00: "Lots of people live alone and get stuck on everyday things. A new remote, a coffee machine, a thermostat."
- 0:40: "Point at something and ask. SAM 3.1 outlines exactly what you mean, while Muse Spark explains it, both on the same frame."
- 1:15: "Spark calls tools that move Nubi through *your* room, on a NavMesh built from your scanned floor."
- 1:55: "When the network drops, Nubi doesn't go silent."

### Optional beats (should tier)

Use at most two, and only if the tier has shipped and passed 10 clean runs. Each one replaces the matching amount of time from beats 3–5, never from 1 or 2.

- "Nubi, where are my glasses?" → `find_object` highlights every pair in view, and Nubi looks at the nearest one (it does not walk). Device: the model chose `find_object` for "where is my mug? Show me." in 3 of 3 `36778c1` runs (2 of 3 on `f311081`), but twice it added a `look_at` round, so the answer came 3.0–4.9 s after the tool start; all on the bundled still image, never in a lit room. Rehearse it before using it.
- "Come here," then pinch while Nubi is walking: the talking stops and the walk carries on (a barge-in never cancels locomotion; not yet seen on a scanned floor).
- "Remember this place as the sofa." Walk away, then "Walk to the sofa."
- "Switch to Guru." → Nubi greets you at once in the new voice and style.

## Fallback per beat

| Beat | Failure | Fallback during recording | Fallback live |
|---|---|---|---|
| 1 | ASR mishears, or the reply is slower than 2 s | Retake. Check the Wi-Fi band (use 5 GHz) and read the latency HUD | Say it again. "Come here" also works as "Nubi, come to me" |
| 1 | Nubi doesn't walk (no NavMesh) | Re-run Space Setup, relaunch, wait for the floor plane | Skip the walk and continue with beat 2 |
| 2 | No mask, or the mask is on the hand | Hold the remote further from the body and point with the other hand. If R1 failed, use the centre-point prompt: hold the remote in the centre of view | The answer still streams without a mask (SAM no-match is not an error); say "Let's look at it again" and repeat once |
| 2 | Vision timeout ("hold it closer" line) | Move to brighter light and retake | Hold it closer, as Nubi asks |
| 2 | No `highlight_part` call, or the part mask is wrong | Ask the follow-up on its own: "Which button turns it on?" | Skip the part highlight; the object mask has already landed |
| 3 | Nubi loses you while following | Walk slower and stay in view | "Come here" re-targets Nubi |
| 4 | Audio doesn't stop | Retake, and file a bug (the barge-in gate is a must) | Pinch again; the second pinch also cancels |
| 5 | The app doesn't notice going offline | Wait 2 s, then pinch (the reachability poll runs every 1 s) | Toggle airplane mode instead of Wi-Fi |
| 5 | Still offline after Wi-Fi returns | Wait until the status indicator shows Online. The health probe backs off from 5 s up to 30 s, so keep the Wi-Fi-off segment short | Talk about the offline behaviour until the indicator turns Online, then pinch |
| 6 | The meter is not visible | Screen-capture Settings separately and cut it in | Read the number from Settings |

## Live finals runbook (Nov 9)

The finals format (length, Q&A, in-headset or cast) is **UNVERIFIED** (`docs/research/hackathon.md` §1). Prepare for a cast demo of about 3 minutes plus Q&A.

### The day before
- [ ] Charge the headset, the phone hotspot and a USB-C power bank.
- [ ] Build the release APK from the tagged commit, install it, and push the secrets. Check the usage meter headroom against the device cap and `docs/api-spend.md`.
- [ ] Run 10 consecutive clean runs of beats 1–5 in a room *other* than the home room (new Space Setup), to rehearse the venue.
- [ ] Pack: headset, charger, USB-C cable (data), laptop with adb + scrcpy, the TV remote, backup objects (moka pot), a printed copy of this runbook, and the demo video on the laptop and on a USB stick.

### At the venue (30 min before)
- [ ] Reboot the headset (ADR-0034) now, before launching the app (the measured post-reboot runs idled 20 min after it).
- [ ] Wi-Fi: join the venue network, run a speed test, and fall back to the phone hotspot if latency looks poor. Keep the hotspot on and paired as a second network.
- [ ] Space Setup in the demo area. Clear 3 m of floor. Make sure there is enough light for the passthrough camera (the camera frames were black in a dark room).
- [ ] Casting works (MQDH cast or scrcpy) and shows the passthrough *and* Nubi.
- [ ] Launch the app and do one greeting turn ("Hi Nubi, who are you?") and one capture of the remote. This warms the ASR and TTS connections, and the slow greeting answer is spent before beat 1.
- [ ] Settings → Clear memory, Companion persona, large captions.
- [ ] Turn on Do Not Disturb, and check there is no pending system update.

### During the demo
- [ ] Open with the problem in one sentence. Beat 2 (the mask on the remote) is the moment to land, so don't rush it.
- [ ] If any beat fails, use its live fallback once, then move on. Never debug on stage.
- [ ] If the network is down entirely, show beat 5 first ("this is what happens offline"), then play the recorded video for beats 1–4.

### Likely Q&A, with short answers
- *"Why not contributor models?"* ToS §6.2 forbids personal data on them. Camera frames of your home are personal data, so `MetaModelResolver` throws if a contributor model is used on a live-user request.
- *"Does it store camera images?"* No. One frame is kept in RAM for one turn and is sent only when a vision tool runs. `store:false` is set on every request.
- *"What does SAM do vs Spark?"* SAM outlines the object you point at, using your hand ray as the prompt and your hand joints as negatives. Spark names it and explains it. Both run in parallel on the same frame.
- *"How do you interrupt it?"* With a pinch. That cancels one token tree: the LLM stream, TTS and tools stop, the audio fades in 20 ms, and walking continues.
- *"Glasses?"* The same brain behind interfaces. The glasses camera path is roadmap and unverified.

## Rehearsal log

Record every run of the required beats here, with date, build commit, room, result per beat (✓ / ✗ + note) and the run's cost.

| # | Date | Commit | Room | 1 | 2 | 3 | 4 | 5 | 6 | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
