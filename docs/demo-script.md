# Demo script

The demo video runs **about 2:37, cap 2:45**, which is our working cap until the portal's real limit is confirmed (`docs/research/hackathon.md` §1). It shows exactly the six required beats of `definition-of-done.md` §6, and everything else is b-roll or README. Beat 6 (cost) is a 4 s usage-meter overlay inside beat 5, and its seconds go to beat 2, the hero moment. The hero user is an older adult at home, and the hero object is a **TV remote that switches on a real TV in the shot** (red power button preferred; backups: moka pot, thermostat). We never use medication.

The video opens on its best 5 seconds (a flash-forward to the power-button outline from the beat 2 take), then the problem, then the beats. The story closes a loop: the TV is dark in the cold open and comes on at the end of beat 2, because the wearer pressed the button Nubi outlined. Why: `demo/production-plan.md` § Judge critique.

How, why and when it gets recorded, and the files for the edit (capture checklist, voice-over, captions, cards, the Jolly variant of every line), are in [`demo/production-plan.md`](demo/production-plan.md). This file stays the single source of the beats; the Jolly cut changes only the name ([`demo/lines-jolly.md`](demo/lines-jolly.md)).

Rule for recording (relaxed on Nov 1 for the deadline; the 10-run rule stays for the live final): record a beat only after **3 consecutive clean runs of that beat** on the frozen build (no crash, no hang, no manual restart), and record beats as separate takes. Log every run and its cost in the [rehearsal log](#rehearsal-log).

## Setup (before any take)

- **Room.** A lit living room, Space Setup done in *this* room, and a sofa or table that Nubi has to walk around. The floor must be clear for about 3 m.
- **Headset.** Charged above 80 %, release APK installed (`tools/device/install-run.sh artifacts/Nubi.apk`), and `secrets.json` pushed (`tools/device/push-secrets.sh`).
- **Reboot the headset (ADR-0034)** before the session; the measured post-reboot runs idled 20 min after it. Stale frames follow the headset's uptime, not the build: after 4 days of uptime idle dropped 5.0 % of frames, after a reboot 0.23–0.32 % (`docs/perf/selftest-report.md` "Stale frames are device state…").
- **Worn smoke test first (gate for everything below).** On the current APK, 10 minutes worn: passthrough visible, Nubi standing on the floor, captions, badges, HUD and status pill inside a Quest system recording, one remote capture with the outline on the remote. Pull the recording and play it on the Mac. Until this passes, nothing else in this file has been seen on a worn headset.
- **Reset between takes** (the settings panel is hidden by default once that change lands): `adb shell am force-stop com.nubi.companion && adb shell rm -rf /sdcard/Android/data/com.nubi.companion/files/memory && NUBI_SKIP_INSTALL=1 tools/device/install-run.sh`, then the warm-up turn. Caption size and persona persist in PlayerPrefs; set them once. A relaunch also puts Nubi back in front of you.
- **Warm-up turn.** After launch, say one throwaway greeting ("Hi Nubi, who are you?"), then reset memory (above, or Settings → Clear memory). The first turn after launch opens cold ASR and TTS connections, and a greeting is the slowest turn type anyway (a full spoken answer): to first audio it took 3.0–3.3 s in the three `f311081` runs and 2.4–4.7 s in the three `36778c1` runs, against 1.0–3.5 s for the other `f311081` turns, and greetings were p50 2,362 ms even mid-session in the 20-turn run (`751acba`). Beat 1 must be neither the first turn nor a greeting.
- **App state.** Settings → Clear memory. Persona: Companion. Caption size: largest. Check the usage meter and write down its starting value. Close the settings panel before the first take; it is opened on camera only for beat 6.
- **Capture.** Quest system recorder for the first-person footage, with the `debug.oculus.capture.*` props set *after* the reboot; MQDH cast recorded on the Mac as the fallback and the director's monitor; a phone on a tripod for third-person shots; a room recorder on every take, synced by one clap. **To verify on device** (`demo/production-plan.md` § Capture settings to verify): passthrough and Nubi both visible in a pulled 10 s recording, resolution, fps, audio streams, and whether the headset mic can be recorded while the app holds it. Result: ________. The voice-over is recorded separately, after the recording day.
- **Network.** The headset joins a **phone hotspot** (5 GHz if the phone offers it); for beat 5 the wearer switches the hotspot off on the phone, held in passthrough view. This keeps Quest system UI out of the shot and the app in focus (speculation: opening quick settings may unfocus or pause the app; check in W1). The Mac and other phone stay on home Wi-Fi. Fallback: the quick-settings Wi-Fi toggle.
- **Gaze greeting.** Looking at Nubi for 1.5 s makes it speak a canned greeting, at most once per 20 s, shared with the palm greeting (`GazeDwellDetector`, `InteractionTriggers`). The "Meet" shot uses this on purpose. In other takes, if a greeting fires mid-beat, retake; before beat 1 look at the room, not at Nubi, for the last 20 s.
- **Props.** The TV remote (distinct power button, ideally red, batteries in) on the sofa arm; **the TV it controls in view from the hero position, switched off**; the backup object nearby. Light: ceiling lights plus a lamp near the sofa; one capture before the take, and no `evt=vision.too_dark` in logcat.

## Shot list

| t | Beat | Shot | You say / do | Nubi does (what must be visible) | Meta tooling on screen |
|---|---|---|---|---|---|
| 0:00–0:05 | **Hook** (flash-forward) | First-person, cut from the chosen beat 2 take: the power-button outline on the real remote in the wearer's hand. Corner tag on. | — | The outline on the button, Nubi's voice naming it ("…the red one at the top"), Nubi visible in frame if the take allows. | SAM 3.1 outline (no lower third yet) |
| 0:05–0:17 | **Problem** | Third-person phone shot: a person on the sofa, the TV dark, turning the remote over, unsure. | **VO V1 (mandatory)**, the problem statement with the Pew figure. | — | — |
| 0:17–0:23 | **Meet** | First-person MR shot: the real room, Nubi idling on the real floor; look at it. Title card 0:17–0:23: **"Nubi: a companion that can see your world."** | Nothing: look at Nubi for 1.5 s. | Nubi notices you, turns its head, and greets you unprompted (gaze-dwell canned line, lip-sync, caption). | — |
| 0:23–0:42 | **1. Come here** | First-person view, Nubi on the far side of the sofa. Pinch-hold, hand in frame. | "Nubi, come here." Release. | The live caption reads "Nubi", not "newbie". The walk starts at once: the local guard calls `come_here` when the final arrives (ADR-0036; it matches "Hi Nubi, come here." too) and no model is called (ADR-0037). Nubi answers with a baked line ("On my way!", "Coming!" or "Coming right over!") with lip-sync and a caption, and no acknowledgement before it (ADR-0040). Nubi walks *around* the furniture and stops about 1 m in front of you. | Muse Voice Transcribe (live partials), `come_here` badge, latency HUD (no Spark badge: this beat makes no model call) |
| 0:42–1:30 | **2. What is this?** (hero) | Pick up the real remote, hold it at chest height 40–50 cm from the face, point at it with the other hand. **Two pinches.** Hold still through each answer (the outline is world-locked to the capture pose). Then press the button in shot. | Pinch 1: "What is this?" · Pinch 2: "Which button turns it on?" · Then press the outlined button. | Pinch 1: the mask outline appears on the real remote within 2 s and the answer is spoken as it streams; Nubi looks at the remote. Pinch 2: `highlight_part("power button")` outlines the actual button on the cached frame (no new capture); hold that shot at least 3 s. **The press: the TV in the room comes on.** Two SAM prompt types on screen: a pointed object, then a text-prompted part. `capture_and_identify` ends its turn, which is why the part is a second question. | `capture_and_identify`, `highlight_part` badges and the Spark model badge; mask outlines. **SAM 3.1** and **Muse Spark vision** named by lower thirds (no in-app badge for them unless that code lands) |
| 1:30–1:47 | **3. Follow me / Stop** | First person; walk about 3 m slowly, looking back over the shoulder at Nubi. Phone wide as a cutaway (shows the wearer walking; it cannot show Nubi). | "Follow me." Walk. Then: "Stop." | Nubi follows at 1–1.5 m, then stops *immediately* on "Stop" (handled on the device, before any model call). | `follow_user` badge. A `stop` badge appears only if the model also calls `stop`; do not promise it |
| 1:47–2:02 | **4. Barge-in** | Medium shot, Nubi in frame. | "Tell me everything about this remote." Pinch mid-sentence: "That's enough, thanks." | The audio fades out in ≤ 150 ms and Nubi switches to listening (self-test: silent in 28–55 ms over the `751acba`, `f311081` and `36778c1` runs), then answers briefly. | Lower third with this take's silence time. The latency HUD has no barge-in row and does not redraw on Listening; do not claim it |
| 2:02–2:24 | **5. Offline** | Hold the phone in view and switch its hotspot off (the headset's network). Pinch. Then hotspot on, wait for the Offline pill to clear, pinch. | "Are you there?" … "Thanks, Nubi. See you later!" *Optional, only once a wearer has confirmed it:* hold an open palm toward Nubi (look away from Nubi for 20 s first: the palm shares the gaze greeting's 20 s interval). | An offline line plays from the baked clip with lip-sync; idle animation carries on. After the network returns, the next turn is online and warm. Optional: Nubi waves goodbye (only the model's `play_gesture` can wave; keep the take that does). | Status pill "Offline" appears, then disappears (there is no "Online" state) |
| 2:18–2:22 | **6. Cost** (overlay on beat 5) | Picture-in-picture over the end of beat 5: the usage line, cut in from a separate capture. No VO; lower third L9 (`demo/cards.md`): "This take: $___ · total $___ of a $20 hard cap". | — | The meter is lifetime (persisted in `usage.json`), so "this take" is the after − before values from the rehearsal log; checked against `docs/api-spend.md` within 10 % off camera. Never delete `usage.json`: it holds the cap. | Usage meter |
| 2:24–2:37 | Close | End card over the README voice-turn diagram, repo URL (`demo/cards.md`). | VO V5. | Last frame of Nubi (the wave if the take has one) | — |

### Lines (voice-over)

VO never plays over the wearer's voice or Nubi's speech; the technical detail goes into lower thirds (`demo/cards.md`). Exact in/out times: `demo/voiceover.md`.

- 0:05.5, **mandatory** (V1): "One in four Americans 65 and older lives alone. When a new remote or a coffee machine gets the better of them, there's nobody in the room to ask." Source: 26 % of US adults 65 and older lived alone in 2023 (Pew Research Center, from Census Bureau data; the README's [problem statement](../README.md#the-problem) links it). 29 words, about 11.5 s at 150 words a minute; it must end by 0:17.
- 0:42.5, optional (V2), before the question: "Hold something up, point at it, and ask."
- 1:34, optional (V3), during the walk: "Ask it to follow, and it walks with you, around your real furniture." (The Spark tool call is a lower third, not VO.)
- 2:02.5, optional (V4), while the hotspot goes off: "When the network drops, Nubi doesn't go silent."
- 2:25, **mandatory** (V5): "For anyone at home with nobody to ask. Nubi is open source, built on Muse Spark, SAM 3.1 and Muse Voice Transcribe."

### Optional beats (should tier)

Use at most two, and only if the tier has shipped and passed 3 clean runs. Each one replaces the matching amount of time from beats 3–5, never from 1 or 2.

- "Nubi, where are my glasses?" → `find_object` highlights every pair in view, and Nubi looks at the nearest one (it does not walk). Device: the model chose `find_object` for "where is my mug? Show me." in 3 of 3 `36778c1` runs (2 of 3 on `f311081`), but twice it added a `look_at` round, so the answer came 3.0–4.9 s after the tool start; all on the bundled still image, never in a lit room. Rehearse it before using it.
- "Come here," then pinch while Nubi is walking: the talking stops and the walk carries on (a barge-in never cancels locomotion; not yet seen on a scanned floor).
- "Remember this place as the sofa." Walk away, then "Walk to the sofa."
- "Switch to Guru." → Nubi greets you at once in the new voice and style.

## Fallback per beat

| Beat | Failure | Fallback during recording | Fallback live |
|---|---|---|---|
| 1 | ASR mishears, so the guard does not match and the model answers instead (slower, ~2 s, and it may not walk) | Retake. Keep the line short: the guard takes at most 6 words of come triggers, the name, "please" or a greeting. Check the Wi-Fi band (use 5 GHz) | Say it again. "Come here" also works as "Nubi, come to me" or "come closer" |
| 1 | Nubi doesn't walk (no NavMesh) | Re-run Space Setup, relaunch, wait for the floor plane | Skip the walk and continue with beat 2 |
| 2 | No mask, or the mask is on the hand | Hold the remote further from the body and point with the other hand. If R1 failed, use the centre-point prompt: hold the remote in the centre of view | The answer still streams without a mask (SAM no-match is not an error); say "Let's look at it again" and repeat once |
| 2 | Vision timeout ("hold it closer" line) | Move to brighter light and retake | Hold it closer, as Nubi asks |
| 2 | Room too dark: Nubi says it is too dark to see and asks for a light (`too_dark`, no SAM or Spark call, ADR-0039; `evt=vision.too_dark` in the log) | Switch on a lamp near the sofa and retake | Use it as a line: "Fair enough, Nubi." Switch on a light or hold the remote toward one and ask again |
| 2 | No `highlight_part` call on pinch 2, or the part mask is wrong | Retake pinch 2 only, phrased "Show me the power button." (the frame is still cached) | Skip the part highlight; the object mask has already landed; point at the button yourself and press it |
| 2 | The outline sits off the remote (extrinsics not calibrated, R1 marker test never run) | Hold the remote still at 40–50 cm, centre of view; if the offset stays, frame wider so it reads small, and log it | Keep going: the spoken answer carries the beat |
| 2 | The TV does not come on (IR angle, batteries, TV standby) | Test the remote on the TV before every take; aim at the TV for the press | Say "There it is" and move on; the outline already landed |
| 3 | Nubi loses you while following | Walk slower and stay in view | "Come here" re-targets Nubi |
| 4 | Audio doesn't stop | Retake, and file a bug (the barge-in gate is a must) | Pinch again; the second pinch also cancels |
| 5 | The app doesn't notice going offline | Wait until the Offline pill shows (the reachability poll runs every 1 s), then pinch | Toggle airplane mode or the quick-settings Wi-Fi instead of the hotspot |
| 5 | Still offline after the network returns | Wait until the Offline pill disappears (there is no Online pill). The health probe backs off from 5 s up to 30 s, so keep the offline segment under 5 s | Talk about the offline behaviour until the Offline pill clears, then pinch |
| 6 | The overlay is not legible in 4 s | Capture Settings → usage at a larger caption size and cut it in full-frame for the same 4 s | Read the number from Settings |

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
- [ ] Space Setup in the demo area. Clear 3 m of floor. Make sure there is enough light for the passthrough camera: below mean luma 25 Nubi answers "too dark" instead of identifying (ADR-0039). Do one capture of the remote where you will stand and check the log has no `evt=vision.too_dark`.
- [ ] Casting works (MQDH cast or scrcpy) and shows the passthrough *and* Nubi.
- [ ] Launch the app and do one greeting turn ("Hi Nubi, who are you?") and one capture of the remote. This warms the ASR and TTS connections, and the slow greeting answer is spent before beat 1.
- [ ] Settings → Clear memory, Companion persona, large captions.
- [ ] Turn on Do Not Disturb, and check there is no pending system update.

### During the demo
- [ ] Open with the problem in one sentence: one in four US adults aged 65 and older lives alone (Pew, 2023). Beat 2 (the mask on the remote, then the power button) is the moment to land, so don't rush it.
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
