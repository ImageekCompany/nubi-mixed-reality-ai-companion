# Demo video production plan

Written 2026-11-01, 23:00 CET. The beats, their order and their timings live in [`../demo-script.md`](../demo-script.md), which stays the single source; this plan says why each beat is there, how it gets captured, and when. Everything that has not been seen on this headset is marked **UNVERIFIED** and has an owner.

Companion files in this folder:

| File | What |
|---|---|
| [`capture-checklist.md`](capture-checklist.md) | Printable, shot by shot, tick boxes for the recording day |
| [`voiceover.md`](voiceover.md) | VO lines with in/out times, word counts and recording notes |
| [`captions-nubi.en.srt`](captions-nubi.en.srt) | YouTube caption track for the Nubi cut (placeholders for model answers) |
| [`captions-jolly.en.srt`](captions-jolly.en.srt) | The same track for the Jolly cut |
| [`cards.md`](cards.md) | Title card, lower thirds, end card, YouTube title and description, for both cuts |
| [`lines-jolly.md`](lines-jolly.md) | Every spoken line of the demo with "Jolly" in place of "Nubi", plus what changes on the device |

## Deadlines

| | ET | UTC | CET (Warsaw, UTC+1) |
|---|---|---|---|
| Internal buffer: submitted | Wed Nov 4, 3:00 PM | Nov 4, 20:00 | **Wed Nov 4, 21:00** |
| Hard deadline | Wed Nov 4, 9:00 PM | Nov 5, 02:00 | Thu Nov 5, 03:00 |
| Live final (top 5) | Mon Nov 9 | | |

US clocks went back on Sunday Nov 1, so ET is UTC−5 from now on; CET has been UTC+1 since Oct 25. Sunset in central Europe is about 16:30 CET this week, which shapes the recording slot (see [When](#when)).

---

## What

### The one sentence judges must leave with

**"An older person points at their own TV remote, and a little character in their real living room outlines it, tells them what it is, and outlines the power button. They press it, and the TV comes on."**

Everything in the cut serves that sentence: who (someone alone at home), where (their real room, in MR, not VR), what (a body that comes when called, and eyes that see the thing in their hand), and that it holds up (one continuous take per beat, latency visible).

### Three things to remember, in rank order

1. **The hero moment (beat 2, about 1:05–1:30):** the outline lands on the real remote, then (second pinch) `highlight_part` outlines the actual power button on the same frame, then the wearer presses it and the dark TV comes on. Hold the button outline at least 3 s and push in on it in the edit. This is the frame for the thumbnail, and its first 5 s also open the video.
2. **It has a body in your room:** "Nubi, come here." and it walks around the real sofa and stops in front of you (beat 1).
3. **It's for someone real:** one in four US adults 65+ lives alone (Pew, 2023), and there's nobody in the room to ask.

### Story arc

| Act | Time | Beats | What the viewer feels |
|---|---|---|---|
| Hook | 0:00–0:05 | Flash-forward: the power-button outline on a real remote | "Wait, what was that?" |
| Problem | 0:05–0:17 | A real person in a real room, TV dark, stuck with the remote; VO | "I know someone like that." |
| Meet | 0:17–0:23 | First person: Nubi on the real floor notices you and says hello; title | "It's in the room, and it noticed me." |
| Presence | 0:23–0:42 | 1. Come here | "It comes when you call." |
| Capability (hero) | 0:42–1:30 | 2. What is this? → which button? → the TV comes on | "It sees what I'm holding, and the problem is solved." |
| Companionship | 1:30–1:47 | 3. Follow me / Stop | "It goes where I go, and it listens." |
| Trust | 1:47–2:24 | 4. Barge-in, 5. Offline (ends on a warm goodbye), 6. Cost overlay | "It doesn't fall apart, and it's cheap." |
| Close | 2:24–2:37 | End card | "Open source, Meta's models, I can try it." |

### Plan changes to `demo-script.md` (first pass)

The timings in this list are superseded by the [Judge critique](#judge-critique) pass (hook first, 0:05–0:17 problem, 2:37 total).

- **The cold open grows from 10 s to 14 s and beat 1 shrinks from 30 s to 24 s.** The mandatory problem VO is 32 words; at a calm 150 words a minute that is about 12.5 s, not the 8 s the script assumed, so it cannot end by 0:10 without rushing the one line that carries the Impact score. Beat 1 has room to spare: the guard walk answers in about 0.3 s (264 ms in the `0f2cef4` run) and a 3 m walk takes a few seconds.
- **The cold open opens on a third-person shot of the person and the room** (phone camera), then cuts to the first-person MR view of Nubi idling under the title. "Real person, real room, real problem" has to be visible before any feature.
- **VO never plays over the user's voice or Nubi's speech.** The longer technical lines become lower thirds. The beat 2 VO is cut to "Hold something up, point at it, and ask." and plays before the question.
- **The close VO starts with the impact, not the stack:** "For anyone at home with nobody to ask. Nubi is open source, built on Muse Spark, SAM 3.1 and Muse Voice Transcribe."
- **Capture method filled in:** Quest system recorder for the first-person footage, MQDH cast recorded on the Mac as the fallback, a phone on a tripod for third-person shots, and a separate room recorder; to verify on the device (below).

---

## Why

Every beat earns its seconds against a rubric line. If a beat stops proving its line on the day (for example the walk fails on the scanned floor), its fallback in `demo-script.md` applies, and the plan does not try to fake the proof.

| Beat | Seconds | Rubric line it scores | Meta AI tool it proves | Proof the viewer sees |
|---|---|---|---|---|
| Hook | 5 | **Demo 25 %** (the video is worth watching past 10 s), Meta AI tooling | SAM 3.1 | The power-button outline, real hand, real room |
| Problem + Meet | 18 | **Impact 30 %**: who benefits, how much it matters | — (gaze greeting is local) | A real person, a dark TV, the Pew figure in VO; then Nubi on the real floor greeting unprompted |
| 1. Come here | 19 | **Demo 25 %** (holds up), Impact (presence), Tech depth (on-device guard) | **Muse Voice Transcribe** realtime (live partial caption, "Nubi" not "newbie" from keyword aliases) | Live caption, instant baked reply with lip-sync, walk around real furniture on a NavMesh from the scanned floor |
| 2. What is this? | 48 | **Meta AI tooling 25 %** ("beyond a basic integration", "surfaced unexpected capabilities"), Impact | **SAM 3.1** point prompt from the hand ray with hand-joint negatives, then a text prompt for the part; **Muse Spark** vision streaming in parallel on the same frame; Spark tool calls (`capture_and_identify`, `highlight_part`) | Two outlines on a real object; the TV coming on (a real-world effect a VR scene cannot fake); tool badges and the SAM 3.1 and Muse Spark · vision model badges, also named by lower thirds; the answer streams as it is spoken |
| 3. Follow me / Stop | 17 | Meta AI tooling (Spark tools drive a body), Demo | **Muse Spark** tool calling (`follow_user`); `stop` matched on the device | Nubi follows at 1–1.5 m and stops at once |
| 4. Barge-in | 15 | **Tech depth 20 %** (streaming, cancellation), Demo | Responses streaming cancelled on the client, Wit TTS flushed | Audio stops mid-word; lower third with this take's silence time |
| 5. Offline | 22 | Tech depth, Demo (holds up live), Impact (an older person's Wi-Fi drops) | — (that is the point: the body survives without the cloud) | Phone hotspot switched off in passthrough view; Offline pill; baked line with lip-sync; pill clears; a warm goodbye |
| 6. Cost (overlay) | 4 | Impact ("could it become something people use"), Tech depth | Usage metering of Spark, SAM and ASR calls | "This session: $___" from the in-app meter |
| Close | 13 | Meta AI tooling (names the stack), Impact (restated) | Muse Spark, SAM 3.1, Muse Voice Transcribe, Wit.ai | Architecture card, repo URL |

What deliberately stays out of the video: personas, `find_object`, `remember_place`, Polish, the Psychologist idea (safety scrutiny, `hackathon.md` §2), and anything only tested in the Editor. They are in the README. The optional beats in `demo-script.md` replace time from beats 3–5 only, and only after 3 clean runs of that beat.

### Honesty rules for the cut

These protect the Demo score's "holds up" and the live final, where judges will see the real latency.

- **One continuous take per beat.** Cuts go between beats, never inside one. No speed ramps inside a beat. If the beat 3 walk is too long, cut away to the phone angle and back on the same take's timecode, never shorten the time between a question and its answer.
- **Every number on screen comes from that take**: a value read from that take's logcat (`evt=turn.latency`, `evt=vision.timing`), noted in the rehearsal log, or the latency HUD in the footage if latency stats were turned on for it (they are off by default, and a nearer Jolly can hide parts of the HUD, ADR-0050). No number from the README goes on screen as if it were this take.
- **A small corner tag on all first-person footage:** "Quest 3 · real time · one take per beat".
- **Publish one uncut full run** (beats 1–5 in one take, no edit) as a second unlisted video and link it from the README next to the main video. It costs nothing extra (it is recorded anyway, see the checklist) and answers "does it really work?" before a judge asks.
- **The wearer is whoever wears it.** The VO never claims the wearer is the person in the cold open unless that is true.

### The Jolly question (decision for the human)

The user wants the video recorded with the local "Jolly" skin: a look-alike of Meta's Jolly mascot, built from an unlicensed print, git-ignored, never committed, selected with `tools/device/build.sh --character jolly`, with its own Wit voice ("Railey") and canned lines that say "Jolly". The default, committed, open-source character is Nubi.

What binds us:
- The Official Rules make the entrant warrant that the Submission "does not violate the intellectual property or other proprietary rights (including copyright, **trademark**, …) of any other person or entity", and grant the Organizers a "perpetual, irrevocable, worldwide, royalty-free … license to use, reproduce, adapt, modify … the Submission" (`docs/research/hackathon.md` §1). The video is part of the Submission.
- Our own README says the mascot "is not in this repository, and nothing derived from it is either", because "we found no licence for that model" ("Character and the Jolly question"). `docs/art/jolly-demo.md` goes further: the skin is "never committed, pushed, published, or shipped in a build or video you distribute without permission from Meta."

| | Nubi in the submitted video | Jolly in the submitted video |
|---|---|---|
| Recognition | An unknown character; has to win on charm and capability | Meta judges recognise their own mascot instantly; probably more memorable and more shareable (community-vote and most-viral bonus pool, up to $50k) |
| Rules risk | None: original, CC0, generated by our script | Breaches warranty (c) unless Meta permits it; a Stage 1 or legal reviewer can disqualify a $25k–$150k entry on that alone. "Meta probably tolerates it at a Meta event" is speculation |
| Consistency | Video, README, repo and the build a judge installs all match | The README says the mascot was removed for IP reasons and the video shows it; the APK a judge builds from the repo shows Nubi. Both cost credibility under Tech depth and Demo |
| Live final | The same character on stage as in the video | The live build would have to be the local Jolly build again, with the same exposure in front of Meta staff |
| Work | The current plan | A second build, ASR check for "Jolly" (aliases are only "Jolie", "Jollie"; "jelly" and "Holly" are not covered), Railey voice check, its own rehearsals |

**Recommendation: submit the Nubi cut.** The upside of Jolly is recognition; the downside is a rules breach that can zero the entry, plus a visible contradiction with our own README. The proportionate path to using Jolly is permission, not hope:

1. **Today or tomorrow morning:** ask the organizers in Discord or by email, in writing, whether a fan-made look-alike of the Jolly mascot may appear in a submission video (and in the live final). Keep the answer.
2. **If a written yes arrives by Nov 3, 12:00 CET:** record the Jolly cut as the master (all the files here are ready for it), add one line to the README naming the permission, and keep the repo on Nubi.
3. **Otherwise:** submit Nubi. A Jolly cut may still be recorded after the Nubi master is done, for the user's private use only: not uploaded, not linked, not posted.

Either variant can be recorded from these files: `captions-jolly.en.srt`, `lines-jolly.md` and the Jolly rows of `cards.md` mirror the Nubi ones line for line.

---

## How

### Capture: four sources, one primary

| Source | Gives | Strengths | Risks | Use |
|---|---|---|---|---|
| **Quest system recorder** (universal menu → Camera → Record video) | First-person view with passthrough and app layers composited, app audio, optionally the headset mic | Same compositor the wearer sees; no cable; resolution and bitrate set by props | **Verified 2026-11-01** (below): passthrough, Nubi, captions, HUD and badges are in a recording of this app; default 1920×1080 HEVC 60 fps ~7.6 Mbit/s, AAC 48 kHz stereo; file in `/sdcard/Oculus/VideoShots/`. Cannot be started over adb. **UNVERIFIED:** the headset mic while the app holds it, the GPU cost (stale frames), and whether the props change the output | **Primary A-roll** |
| **MQDH cast** (Meta Quest Developer Hub → Cast) recorded on the Mac with OBS or QuickTime screen recording | The same view, on the Mac | Lets the director watch live; independent of on-device storage | Wireless compression, window-size resolution, **UNVERIFIED** whether passthrough and audio come through the cast | **Fallback A-roll**; also the director's monitor while recording with the system recorder |
| **scrcpy** / `adb shell screenrecord` over USB | The raw display buffer | Scriptable | **Verified 2026-11-01 with `screenrecord`** (the same buffer scrcpy mirrors): both lens-distorted eye buffers side by side, 4128×2208, **no passthrough** (black behind Nubi). Useless as A-roll | Debugging only (`tools/demo/record.sh screen`) |
| **Phone on a tripod** (third person, 4K 30 fps, locked exposure) | The person, the headset, the room | The only source that shows a real person in a real room | Cannot show Nubi (no mixed-reality capture rig in an OpenXR app) | Cold open, cutaways during walks, picture-in-picture during beat 2 |

Plus a **room audio recorder** (a second phone with a voice-memo app, or a lav on the wearer), running for every take as the safety track for the wearer's voice and for sync (one hand clap at the start of each take).

#### Capture settings (verified 2026-11-01, Quest 3, Horizon OS v209, metacam 209.0.0.x)

Tooling: `tools/demo/record.sh` (props, list, pull, screen), `tools/demo/check-frame.sh` (ffprobe report, frames, passthrough check), `tools/demo/reset.sh` (between takes).

- **Props.** The recorder (`com.oculus.metacam`) reads `debug.oculus.capture.width`, `.height`, `.fps`, `.bitrate` and `.codec`: the names are in its v209 code, `setprop` from adb sets them without root and `getprop` reads them back. They are lost on reboot, so set them **after** the ADR-0034 reboot: `tools/demo/record.sh props 1920x1080 30 20000000`. Whether the output follows them is **UNVERIFIED** (that needs a recording started in the headset); `debug.oculus.fullRateCapture` is not in the recorder's code, so it is dropped.
- **Defaults** (no props set, uptime 6.5 h, so none were set): the 2026-11-01 22:50 recording of this app (`com.nubi.companion-20261101-224958-0.mp4`, 5:26) is 1920×1080 HEVC at 60 fps, ~7.6 Mbit/s, with AAC 48 kHz stereo. 60 fps HEVC is fine for the edit: transcode to the 30 fps H.264 timeline, or keep 60 for the push-in.
- **Passthrough is in the recording.** `tools/demo/check-frame.sh` on that file: 10 of 10 frames with passthrough (mean luma 55–87, 10th percentile 24–35); the frames show the real room, Nubi, the captions, the HUD and the badges. It also showed the old layout's faults: the settings panel over half the frame and the HUD and badges cut off at the edges, fixed by ADR-0042.
- **Start and stop are in the headset only.** The recorder's one intent (`horizonos.appactions.START_RECORDING` / `STOP_RECORDING` on `.capture.CaptureService`) is refused from adb with "Missing result messenger": it needs a Messenger extra that `am` cannot pass. The wearer starts it from the universal menu (Camera → Record video); the recorder has a controller shortcut, whose chord is **UNVERIFIED** on v209.
- **`adb shell screenrecord` / scrcpy** show the app's eye buffers without passthrough (a 10 s clip of the Jolly build at 23:52: 4128×2208, black behind the character, `check-frame.sh` FAIL). Not usable for the video.
- **The recorded field of view** is off-centre and narrower than the headset's: 13.6° above, 24.1° below, 32.6° left and 30.1° right of head-forward (measured on 2,605 frames of two recordings, ADR-0050). Head-forward sits at 35 % of the frame height, not the middle, and the recorder's stabiliser moves it. Every head-following view fits inside it at rest (ADR-0050); the settings panel at 0.7 m is taller than the frame, so for the beat 6 cost shot frame the usage line (bottom of the panel), not the whole panel.

Still **UNVERIFIED**, for the wearer at W1: a new recording with the props set (do they change resolution, fps, bitrate?), "record microphone" on during a push-to-talk turn (does the app keep the mic, is the voice in the file?), and stale frames while recording (one `tools/device/selftest.sh` with a recording running; turns were 0.49–0.60 % after a reboot). After each: `tools/demo/record.sh pull && tools/demo/check-frame.sh artifacts/demo/raw/<file>.mp4`.

If a new recording ever shows black behind Nubi (`check-frame.sh` FAIL): check that the APK has the OpenXR Composition Layers fix (`07c312f`) and that `tools/device/selftest.sh` reports `passthrough: compositor numLayers=1`, then try the MQDH cast. If both are black, the video becomes phone over-the-shoulder shots and the human decides whether to delay it (this is the single largest risk to the Demo score).

### Audio

| Track | Source | Level target |
|---|---|---|
| Nubi's speech | Inside the recording (app audio) | Natural; peaks around −6 dBFS in the edit |
| Wearer's voice | Headset mic in the recording if step 5 passes, otherwise the room recorder (clap-synced) | Matched to Nubi's level |
| Voice-over | Separate session: USB mic or a phone 15–20 cm from the mouth, in a soft-furnished room (wardrobe, sofa with cushions), after the recording day | −16 LUFS integrated for the VO stem |
| Room tone | 30 s of silence in the recording room, same setup | For gap filling |
| Music | Title and end card only, ducked to about −24 dB under VO; never under a beat | |

Final mix: about −14 LUFS integrated, true peak ≤ −1 dBTP (YouTube normalises to roughly −14; speculation that judges' players do anything more). Measure with `ffmpeg -i final.mp4 -af loudnorm=print_format=summary -f null -`.

Quiet set: phones on silent, washing machine and fridge noise checked, windows shut. For beat 5 the headset is on a phone hotspot that the wearer switches off in view; never touch the home router (the other phone and the Mac need it).

### Lighting

- The vision path refuses frames below mean luma 25/255 (`too_dark`, ADR-0039), and every device room so far measured 0.5–45. Aim far above the threshold: all ceiling lights plus a lamp beside the sofa, pointed at where the remote is held, not at the wearer's face.
- **Artificial light, curtains half drawn**, so takes match regardless of the time of day. Avoid a bright window behind the remote (the passthrough camera exposes for the window and the remote goes dark) and direct sun on the headset's cameras.
- Check with one capture at the hero position before every session: logcat must have no `evt=vision.too_dark`, and the luma value in `evt=vision.*` lines goes into the rehearsal log.
- Phone camera: lock exposure and white balance on the wearer; the headset front may reflect the lamp, so place the lamp off-axis.

### Framing (first person: the wearer is the camera operator)

- Move the head slowly; hold still for 2 s before speaking and 2 s after Nubi finishes. Every head movement is a camera move.
- Nubi framed in the lower-middle third at 1–1.5 m; the floor and a piece of furniture in the frame so the viewer reads "real room".
- Beat 2: remote at chest height, 40–50 cm from the face, in the centre 60 % of the view, the other hand pointing from the side so the hand does not cover the remote. Hold still until each answer ends (the outline is world-locked to the capture pose). After the second pinch, lean in slightly for the power button and hold 3 s, then turn toward the TV so the press and the TV coming on are in one frame.
- Beat 3: look back over the shoulder so Nubi is visible behind or beside you during the walk; the phone angle can only show the wearer, never Nubi.
- Meet shot and beat 1: the pinching hand in frame, so the viewer sees the trigger during the ~1 s before the first caption partial appears.
- Keep the settings panel out of frame except for beat 6 (hidden at launch since ADR-0042; open it from the wrist button, the left ☰ or "open settings" only for the cost shot).
- Clean the room for privacy: no family photos, mail, addresses, screens or anything with names in view of the passthrough or phone camera. No terminal with keys on any screen in the phone shots.

Third person (phone): tripod at seated eye height, 3–4 m back, the wearer and the sofa in frame; one wide for the cold open, one over-the-shoulder for beat 2 (shows the real remote in a real hand), one wide for the beat 3 walk.

### Overlays

| Type | Where | Style |
|---|---|---|
| In-app (already in the footage) | Live captions, tool and model badges, status indicator, mask outlines | Must be legible at 1080p on a phone: caption size Large, checked on the step-4 frame |
| Lower thirds (edit) | One per beat, 3–4 s, text in [`cards.md`](cards.md) | Bottom-left, white on 60 % black, sans serif, never covering the in-app captions or the mask |
| Corner tag (edit) | All first-person footage | "Quest 3 · real time · one take per beat", small, top-right |
| Picture-in-picture (edit) | Beat 2 (phone over-the-shoulder, 25 % size, 4–6 s), beat 6 (usage meter) | Bottom-right |
| Title and end cards | 0:17–0:23, 2:24–2:37 | Text in `cards.md` |

### Editing

- **Tool:** DaVinci Resolve (free) on the Mac: multitrack audio, SRT import, Fairlight loudness meter, H.264 export. iMovie is enough if Resolve is not already installed and learned; do not learn a new editor on Nov 3.
- **Timeline:** 1920×1080, 30 fps (match the capture; if the capture is 60 fps, use a 60 fps timeline only if every source is 60).
- **Cut order:** lay the VO first (it fixes the cold open and close lengths), then the best take per beat in script order, then trim heads and tails (never the middle), then lower thirds, then music, then captions.
- **Assembly rules:** cuts between beats are hard cuts or 6-frame dips; no transition effects. No zoom except the beat 2 push-in (up to 1.3×, which is why capture above 1080 helps).
- **Captions:** upload the SRT as a YouTube caption track (English). The in-app captions are already burned into the picture; additionally burn in only the VO lines, so a muted viewer gets the problem statement. Replace every `[…]` placeholder in the SRT with what was actually said in the chosen take, and shift the times to the final cut.
- **Music:** one calm instrumental under the problem and title (0:05–0:23, ducked under VO; silence under the hook) and the end card. Use a CC0 track, so there is nothing to attribute and nothing that conflicts with the Organizers' licence to "reproduce, adapt, modify" the Submission: FreePD.com (public domain / CC0) is the recommended source. Write the track title, URL and licence in the YouTube description and in the rehearsal log. Avoid tracks that need attribution or forbid redistribution (Pixabay's licence and the YouTube Audio Library's terms are fine for YouTube but add a question about the Organizers' re-use; speculation, so avoid rather than research it now). No music is a valid choice.
- **Export:** H.264 (High profile), 1920×1080, 30 fps, VBR about 16 Mbps, AAC 48 kHz stereo 320 kbps, MP4. Length ≤ 2:45 (or the portal's limit once known). Keep the project file and the export in `artifacts/demo/` (git-ignored); never commit video.
- **QC before upload:** watch once on a phone with sound off (captions carry it?), once on headphones (levels, no clipping, no room hum), and check the list in `capture-checklist.md` §QC.

### Upload and links

- YouTube, **unlisted** by default, title and description from `cards.md`, the SRT as English captions, the thumbnail from the beat 2 power-button frame. Processing to HD can take 30+ minutes; upload early.
- Public vs unlisted is a human decision: unlisted keeps it out of search until submission; public helps the community-vote and most-viral bonus pool. Switching to public later keeps the URL.
- Second video: the uncut full run, unlisted, linked from the README only.
- The README's "Demo video" line gets both links (agent, at submission; the lead commits). The release checklist row 7.6 flips then.
- A public GitHub repo URL must exist for the end card and the portal; the local checkout has no remote yet (`git remote -v` is empty). Human decision: the repo name and when it goes public.

---

## When

All times CET (UTC+1); ET = CET − 6 h. "Agent" means a Claude session; "device agent" is the one that owns `adb` and the headset; "human" is the developer, plus a second person where named.

### Mon Nov 2: verify on a worn headset, fix, start rehearsing

| CET | Who | What | Done when |
|---|---|---|---|
| 08:30–09:30 | Human | Portal sign-in: copy the video length limit and every field (`hackathon.md` §1). Discord: the official problem statements. Decide the Jolly question (ask the organizers in writing if Jolly is still wanted). Decide who appears on camera and get their consent. Decide the public repo name. | Answers written into the rehearsal log notes and this plan's decisions |
| 08:30–09:30 | Agents | Land the in-progress changes (Nubi faces the user while talking; settings panel hidden by default, opened by wrist, menu or voice), run `tools/ci/test.sh`, build the release APK | Tests green, APK hash recorded |
| 09:30–12:30 | Human wears, device agent reads logcat | **Wearer verification W1** in the living room, in this order: passthrough visible in the headset with Nubi on the floor; Space Setup of the room; light check at the hero position (no `too_dark`); pinch and push-to-talk with a wearer (no phantom pinches); beat 1 walk on the scanned floor around the sofa; beat 2 mask placement on the real remote (and the camera-extrinsics marker test if time allows); beat 2 second pinch (`highlight_part` on the cached frame) and the TV coming on from where the wearer stands; beat 3 follow and stop; beat 4; beat 5 with the phone hotspot switched off and on (and whether quick settings unfocuses the app); the gaze greeting firing in the Meet shot and *not* mid-beat; opening the settings panel; captions, badges and pill inside the recorded frame, no HUD (ADR-0050); the capture checks still UNVERIFIED in § Capture settings (props, mic, stale frames while recording). Jolly build only: "Hi Jolly, come here" ×5, log the partials, add mishearings to the local aliases | Each item ✓/✗ with a log line in the rehearsal log; a bug list |
| 12:30–17:00 | Agents | Fix the W1 blockers, beat 1 and beat 2 first. Device agent: `tools/demo/record.sh pull` + `tools/demo/check-frame.sh` on the W1 recordings (props effect, mic, stale frames) | Fixes merged; capture method decided and written into `demo-script.md` |
| 12:30–16:00 | Human | Room prep: declutter for privacy, lamp placement, clear 3 m of floor, props (remote with batteries, backup moka pot). Phone b-roll in daylight: cold-open wide of the person with the remote, room establishing shots | B-roll on the Mac, backed up |
| 17:00–19:00 | Human + device agent | **W2**: re-verify every W1 ✗; first full runs of beats 1–6 | All beats work at least once |
| 19:30–22:30 | Human | Rehearsals on the current build, logged; aim for 3 consecutive clean runs per beat | Rehearsal log rows |
| 22:30 | Human | Decide go/no-go for a code freeze tomorrow at 10:00 | |

### Tue Nov 3: freeze, 3 clean runs per beat, record

| CET | Who | What | Done when |
|---|---|---|---|
| 08:00–10:00 | Agents | Fix only failures seen in rehearsals; nothing new | |
| 10:00 | Agent (lead) | **Code freeze.** Tag the commit (e.g. `demo-1`), build the release APK, install it, record the SHA-256 in the rehearsal log. Any change after this restarts the 10-run count | Tag exists |
| 10:00–12:00 | Human | **3 consecutive clean runs per beat** on the frozen build (`demo-script.md` rule, relaxed from 10 for the deadline). A ✗ restarts that beat's count | 3 ✓ in a row per beat |
| 12:00–12:30 | Human + device agent | Reboot (ADR-0034), set the capture props, launch, warm-up greeting, Clear memory, persona Companion, captions Large, note the usage meter, Do Not Disturb, phone and room recorder ready | Pre-flight in `capture-checklist.md` ticked |
| 12:30–15:30 | Human (wearer), second person on the phone if available | **Record.** Per beat: at least 3 complete takes, keep going until 2 are clean. Then **2 uncut full runs** (beats 1–5, one take). Then the beat 6 usage-meter shot. Phone runs on every take | Takes logged with file names and the take's logcat numbers |
| 15:30–16:30 | Human | Pickups while daylight lasts: cold-open third-person shots, over-the-shoulder beat 2 (hand, remote, TV coming on), the hotspot-switch close-up | |
| 16:30–17:30 | Human | VO session (all lines, 3 reads each), 30 s room tone | VO files on the Mac |
| 16:30–17:30 | Agent | Pull logcats for every kept take; write the numbers per take (latency, mask time, barge-in silence, cost) into the rehearsal log so the lower thirds use this take's numbers | |
| 17:30–23:00 | Human | Assembly cut in Resolve (picture lock target 23:00) | Rough cut ≤ 2:45 |
| optional 18:00–20:00 | Human + device agent | Jolly cut, only per the Jolly decision: `build.sh --character jolly`, install, 3 clean runs of the Jolly lines, record beats 1–2 (and the rest if time allows) | |

### Wed Nov 4: finish, upload, submit by 21:00 CET

| CET | Who | What | Done when |
|---|---|---|---|
| 09:00–12:00 | Human | Fine cut, lower thirds, cards, music, captions (fill every placeholder from the takes) | |
| 09:00–12:00 | Agent | Re-time the SRT to the locked cut on request; check every on-screen number against the take log; draft the portal text from the README | |
| 12:00–13:00 | Human + agent | **QC** (`capture-checklist.md` §QC): length, loudness, muted watch, captions, no secrets, no unconsented faces, character matches the Jolly decision | All boxes ticked |
| 13:00–14:30 | Human | Export, upload both videos (unlisted), captions, thumbnail; wait for HD processing | Links play in HD in a private window |
| 14:30–15:30 | Agent | README "Demo video" line with both links, release checklist 7.6; lead commits | Commit on main |
| 15:30–18:00 | Human | Portal submission, filled from the draft; screenshot the confirmation | **Submitted by 18:00 CET** (3 h before the internal buffer) |
| 18:00–21:00 | — | Buffer. If anything slipped, this is where it lands; the internal deadline is 21:00 CET | |
| 21:00 | | Internal buffer ends (3 PM ET). Hard deadline 03:00 CET Thu | |

After submission: the live-final runbook in `demo-script.md` (Nov 9) takes over.

### If time collapses

Cut in this order, never the other way round: music → lower thirds beyond beats 1–2 → the phone picture-in-picture → the uncut second video → beat 3 (its time goes to beat 2) → the third-person cold open (fall back to the first-person shot of Nubi idling under the VO). Never cut: the hook, the problem VO, beat 2's two outlines and the TV coming on, the honesty of one take per beat, captions.

---

## Decisions only the human can make

1. **Jolly or Nubi in the submitted video** (recommendation: Nubi unless the organizers permit Jolly in writing by Nov 3, 12:00 CET).
2. **Who is on camera**: a real older adult (consent, comfort with the headset; strongest Impact) or the developer with the cold open showing the problem only. Nobody appears without consent; a minor never appears.
3. **Who operates the phone camera**: a second person, or a locked tripod.
4. **Public or unlisted** YouTube at submission.
5. **The public repo URL** and when the repo goes public (needed for the end card and the portal).
6. **The video length cap** once read from the portal (the plan assumes ≤ 2:45).
7. **Music or none**, and which CC0 track.
8. **Go/no-go on the code freeze** at Nov 3, 10:00 CET, and whether to delay the video if the capture shows black passthrough.

---

## Judge critique

Written 2026-11-01 ~23:30 CET from the point of view of a judge on their 200th video of the day. Scored against the rubric (Impact 30, Meta AI tooling 25, Tech depth 20, Demo 25).

### Verdict

**As drafted before this pass: a solid top-10 Indie entry, not first place.** The engineering is real, but the cut was built like a feature checklist, so it scored best on Tech depth, the smallest weight. Its problems, from the most expensive down:

1. **Nothing worth watching in the first 10 s.** It opened on 14 s of statistics over a phone shot, with no MR, no character and no outline. At video 200, a judge has decided by second 8. The best frame we have (the power-button outline) did not appear until about 1:10.
2. **No before/after.** The cold open shows someone stuck with a remote, and nothing in the video ever un-sticks them. Beat 2 ended on an outline, which is a UI state. The problem gets solved when the TV comes on.
3. **Beat 2 as scripted could not happen.** "What is this, and which button turns it on?" as one utterance needs `highlight_part` after `capture_and_identify`, and that tool ends the turn. It has never been observed. The hero moment of the video depended on luck.
4. **Promised on-screen proof that does not exist.** "SAM 3.1 + Spark badges" (there is only the agent's Spark badge), "latency HUD shows the barge-in" (no such row, and the HUD does not redraw on Listening), a "stop" badge (the local stop raises none), "Offline → Online" (there is no Online state; the pill just disappears), and "This session: $" (the meter is lifetime). A judge who pauses on those frames sees a script that oversells. That costs more than the claim gains.
5. **Too technical for the audience.** Twelve lower thirds in 2:45, "NavMesh" in VO, and ms values everywhere. Judges skim this. One number per beat is plenty.
6. **Weak "companion".** The title says companion, but every beat is a command. The only warm moment was an optional palm greeting.
7. **The trust section ran 47 s** (barge-in 20 + offline 27) on behaviour that engineers love and viewers find flat, and the Wi-Fi toggle put Quest system UI in the shot.
8. **Proof it is MR, not VR,** rested on passthrough alone. Nobody has seen that passthrough in a recording yet (it was black until `07c312f`).

### What this pass changed (`demo-script.md` and this folder)

- **Hook first (0:00–0:05):** a flash-forward of the power-button outline from the chosen beat 2 take, with Nubi's voice. It is honest because the full, unbroken beat plays later.
- **Problem 0:05–0:17:** V1 trimmed to 29 words ("One in four Americans 65 and older lives alone…"), with the TV visibly dark in the phone shot.
- **New "Meet" shot (0:17–0:23):** Nubi notices you on the real floor and greets you unprompted. This uses the gaze-dwell greeting, a baked line that is instant and needs no code. It gives the title card a living subject and is the first companion moment.
- **Beat 2 is now two pinches** ("What is this?", then "Which button turns it on?" on the cached frame), and it ends with the wearer **pressing the button and the real TV coming on**. That is the before/after, and it is a real-world effect a VR scene cannot fake. The hero object is a remote with a distinct (red) power button that controls a TV in the shot. New fallback rows cover an offset outline and a TV that does not respond.
- **Claims match the code:** SAM 3.1 and Spark vision are named by lower thirds (L3–L5); since the vision path raises its own model calls they also have in-app badges ("SAM 3.1", "Muse Spark · vision"); barge-in proof is the take's silence time in a lower third, not the HUD; there is no Stop badge promise; the status shot is "Offline pill appears, then clears"; the cost shot reads "This take: $(after − before) · total $X of a $20 hard cap".
- **Lower thirds 12 → 9**, at most one on screen at a time. V3 no longer says "NavMesh" ("Ask it to follow, and it walks with you, around your real furniture").
- **Beat 5 uses a phone hotspot**, switched off in passthrough view, instead of Quest quick settings. It ends on a warm "Thanks, Nubi. See you later!", with the wave as a bonus. The trust section is now 37 s (15 + 22).
- **Total about 2:37**, 8 s under the cap, as slack for real latencies.
- **Rehearsal rule relaxed** to 3 consecutive clean runs per beat (the 10-run rule stays for the live final). Beats are recorded as separate takes.
- **Setup additions:** a worn smoke test as the gate before everything, an adb reset between takes, the gaze-greeting hazard and how to use it, the light check, and a TV/remote IR test.
- Updated to the new timeline: `cards.md` (title, lower thirds, end card, chapters), `voiceover.md`, `capture-checklist.md` (shots, props, QC), `lines-jolly.md`, and both SRT tracks (rewritten; still placeholders until the takes exist).

### What remains (ranked by how much it can cost the Demo score)

1. **No one has seen MR on a worn headset since the passthrough fix.** Passthrough in a system recording, Nubi grounded, panel layout, mask alignment and the hand pinch are all unseen. Everything above is a plan until W1 (Mon 09:30) passes. If the system recording shows black passthrough, the plan's escalation applies.
2. ~~**The settings panel hide-by-default change has not landed.**~~ Landed (ADR-0042): hidden at launch, opened on demand, world-locked.
3. **Mask alignment on a real object is unmeasured** (R1 extrinsics marker test never run; the outline has only ever been shown on the bundled still). The hook and the thumbnail both depend on it.
4. **The gaze greeting can fire mid-beat** (head ray on Nubi for 1.5 s, every 20 s), most likely during the beat 1 walk-up. Without code: retake. With code (needs Unity, under 1 h): a recording flag that disables gaze dwell after the Meet shot. A judge will not notice one extra greeting; a greeting that talks over the wearer's question is a retake.
5. **Jolly vs Nubi.** The user wants Jolly. This plan still recommends the Nubi master unless the organizers permit the look-alike in writing, because of the IP warranty in the rules and the contradiction with our own README. Jolly's name has never been through ASR from a human voice ("Jolie"/"Jollie" are its only aliases). If Jolly is used, run the ×5 name test in W1 first.
6. **Beat 4's second utterance** ("That's enough, thanks.") goes to the model and gets a short spoken reply that has not been rehearsed. If it rambles, cut on the listening state and drop the line.
7. **The beat 5 hotspot path is untested.** The probe backs off 5 → 30 s, so keep the offline segment under 5 s. Whether quick settings unfocuses the app is speculation; check it in W1 before choosing the fallback.
8. **The TV has to come on from where the wearer stands** (IR angle, batteries, TV standby delay). Test it before every take. If the TV is slow, cut away to the phone over-the-shoulder shot on the same timecode; never speed-ramp.
9. ~~**Side panels may sit outside the recorded frame**~~ They did: the 2026-11-01 22:50 system recording cuts off the HUD and the badges. ADR-0042 moved them to x = ∓0.4 m and shrank them. ADR-0050 re-placed every head-following view inside the measured, off-centre window (13.6° above, 24.1° below, 32.6° left and 30.1° right of head-forward), within 25° across. Re-check on the first worn recording.
10. **There is no wide third-person shot of Nubi** (no MRC rig). The follow beat is first person, looking back over the shoulder, and the phone shows only the wearer. That is acceptable.
11. **Portal video limit and fields are still unverified.** If the limit is under 2:37, cut beat 3 first (`If time collapses`).
12. **The on-camera person.** If the wearer is the developer, the VO must not imply that the person in the problem shot is the wearer (honesty rule). A real older adult on camera, with consent, is the largest single Impact upgrade still available.
