# Hackathon research: rules, submission, strategy

Researched 2026-11-01. Each claim cites a URL, a file, or a command. Anything not confirmed from a primary source is marked **UNVERIFIED**.

Primary sources:
- **[CV]** Event page: https://cerebralvalley.ai/e/meta-ai-developer-hackathon (and `.md`). The Official Rules and Event Waiver are embedded in this page's payload and served at https://cerebralvalley.ai/e/meta-ai-developer-hackathon/waiver. I extracted them with `curl` plus a Python text extraction of the page's JSON.
- **[META]** Meta's event page: https://dev.meta.ai/events/global-hackathon
- **[DOCS]** Meta Model API docs index: https://dev.meta.ai/llms.txt
- **[SPEC]** `/Users/andrewurbanovich/Git/Projects/MetaCompetitionClaude/requirements.md`

---

## 1. Rules and submission

### Dates (all ET)
| When | What | Source |
|---|---|---|
| Oct 26, 12:00 PM to Nov 4, 9:00 PM EST | Hackathon window | [CV] |
| Nov 4, 9:00 PM ET (Nov 5, 02:00 UTC) | **Submissions due** | [CV], [META] |
| Nov 4 to Nov 8 | Judging | [CV], [META] |
| Nov 9 | "Closing ceremony - live finals and winners announced" | [CV], [META] |
| about Nov 10 | Bonus and public-vote prizes announced | [CV] rules |

The rules add: "Dates may be subject to change at Meta's sole discretion. Any change will be posted on this page."

### Where to submit
The rules say: "Participants will submit their project to Cerebral Valley's portal at https://cerebralvalley.ai/e/meta-ai-developer-hackathon/hackathon/submit." ([CV] rules)

**UNVERIFIED: the submission form fields.** The portal requires a sign-in. Without one it shows only the public event page (`curl` of `/hackathon/submit` returned the event page, with no form). The public pages do not state:
- the video length limit,
- whether a repo link is required,
- the description fields,
- how the theme is selected.

Participants plan around a 3-minute video, but these are their own notes, not official rules:
- https://github.com/meta-ai-hackathon/meta-hack says "3-minute demo video is the primary judging artifact".
- https://github.com/jjresher/Meta-hackathon-bladesight/issues/19 says "length <= 3:00".
- https://thegenaimagazine.com/meta-global-ai-developer-hackathon-ideas-prizes guesses 2 to 3 minutes and says so.

**Action (human):** sign in to the portal today and copy down the exact fields and the video limit. Until then, plan for a video of **2:45 or less**, hosted on YouTube (unlisted), plus a public repo and a written description.

### Tracks (verbatim, [CV])
> **Indie developer track:** For individual builders and students. Judged on creativity, technical ambition, and whether it works.

From the rules: the Indie track is "Open to individual builders and students, teams of up to four (4)". A submission may enter "one (1) track only". The Representative "shall identify all other team members in the project submission."

### Themes (verbatim, [CV] / [META])
> **Build for Your World.** Bring Meta's models into your domain. Solve a problem you know firsthand, free up time and mental space, and make something possible that wasn't before.
>
> **Push the Frontier.** Use Meta's models to tackle challenges in software, robotics, manufacturing, energy, materials and beyond. Give a machine a new capability, accelerate a scientific experiment, or unlock a better way to understand and control a physical system. Show us what becomes possible when these models meet the real world.

**UNVERIFIED: the "problem statements".** The rubric says Impact covers "whether it fits one of the problem statements", but no problem-statement list appears on [CV], [META], or in the rules text. It was most likely posted in Discord, at the kickoff livestream, or inside the portal. **Action (human):** check Discord #announcements and the portal, and quote the matching statement word for word in the README.

### Judging rubric (verbatim from the Official Rules, [CV])
> Impact (30%) — the real-world potential of the Project, who benefits, how much it matters, whether it could become something people use, and whether it fits one of the problem statements; Use of Meta AI Tooling (25%) — how creatively the team used Meta's AI tooling, whether they went beyond a basic integration, and whether they surfaced unexpected capabilities; Technical Depth & Execution (20%) — whether the engineering is sound and thoughtfully refined and the Project demonstrates real craft; and Demo (25%) — whether the Project has a working, impressive demo, whether the product holds up live, and the quality of the demo video.

This matches the weights in [SPEC]. Note the phrases "surfaced unexpected capabilities" and "holds up live". The second one means the Nov 9 final is a live demo, so a demo that only works in a pre-recorded video is not enough.

### Stage process
[SPEC] lists three stages:
- **Stage 1:** pass/fail on theme fit and Meta AI use.
- **Stage 2:** judges score each project 0–10 on the rubric.
- **Stage 3:** the top 5 per track are watched live on Nov 9.

**UNVERIFIED in public sources.** The rules only say "Finalists will be chosen by a panel of Organizers selected judges … selections are final and binding." The prize table pays 5 places per track, which is consistent with a top-5 final. One participant's repo says "Closing ceremony, top 10 live final" (https://github.com/meta-ai-hackathon/meta-hack), which conflicts with the top 5. The finals format (length, Q&A, in-headset or cast) is **UNVERIFIED**.

### Prizes (Indie, [CV] rules)
- Indie track: $400k total. 1st $150k, 2nd $100k, 3rd $75k, 4th $50k, 5th $25k.
- Bonus pool of up to $50k, "including … a community vote award, most viral project award". A public, shareable video and social posts have direct value here.

### IP and open source (verbatim, [CV] rules, "Hackathon Submission")
- "the entrant retains ownership of all intellectual and industrial property rights". The entrant also grants the Organizers a "perpetual, irrevocable, worldwide, royalty-free, and non-exclusive license to use, reproduce, adapt, modify … the Submission".
- The entrant warrants that the Submission "(a) is their … original work product; (b) is solely owned by the Participant … ; and (c) does not violate the intellectual property or other proprietary rights (including copyright, **trademark**, patent, contract, or privacy rights) of any other person or entity."
- "Participants may also include open source software or hardware, provided applicable open source licenses are followed and the Submission enhances or builds upon the underlying open source product."
- The rules also say nothing shared is confidential ("none of the Organizers can agree to obligations of confidentiality").

**Risk: the Jolly model.** `Meta+Muse+Jolly+v2+Whole.glb/.stl` is a Meta mascot of unknown origin. Warranty (c) covers trademark. Using Meta's own mascot in a Meta-run event is probably tolerated, but that is not established. **UNVERIFIED:** whether the asset was distributed with a license. [SPEC] item 9 already allows replacing the character. Recommendation:
- Document the asset's provenance in THIRD_PARTY.
- Keep the character swappable.
- If provenance is unclear, ship an original look-alike and do not use the Meta name or logo as product branding.

### Must the work be created during Oct 26 to Nov 4?
**Not stated in any public rules text I found.** The rules require original work and allow open-source components. A participant's notes say "New work only — demo shows only what was built during the event. Unclear contribution = disqualification" (https://github.com/meta-ai-hackathon/meta-hack). That is **UNVERIFIED** as an official rule. The safe path:
- start git history in the window (true for this project, which has no git yet: `ls` shows only `requirements.md` and the model files);
- list every third-party package and asset in THIRD_PARTY;
- say in the README that Jolly was built during the window.

### Eligibility and credits
- Participants must be 18 or older. Meta and CV employees are excluded, as are sanctioned regions, which include Belarus, Russia and Ukraine ([CV] rules).
- Selected participants get "$150 in Meta Model API credits, valid for a 10-day development window" ([CV] rules).
- The API key works and has all target models enabled. I ran `GET https://api.meta.ai/v1/models` (key loaded from `.env` and never printed) and it returned `muse-wonder-1.0, sam-3.1, muse-spark-1.3-contributor, muse-voice-transcribe-1.0, muse-spark-1.3, muse-image-1.0, muse-spark-1.2-contributor, muse-spark-1.2, muse-spark-1.1`.

---

## 2. Theme mapping and problem statement

**Theme: Build for Your World.**

Justification: Jolly brings Muse Spark, SAM 3.1 and Muse Voice Transcribe into the user's own home. It does this as an embodied guide that walks with you, looks at what you hold up, and shows you how to use it. That is literally "bring Meta's models into your domain … make something possible that wasn't before": hands-free, spatial, patient help with physical objects, which a chat window cannot give.

"Push the Frontier" is aimed at robotics, science and industrial control. Jolly controls no physical system, so claiming that theme would invite a weak Impact score. Robotics-like vocabulary such as "Give a machine a new capability" is still useful as secondary framing for the embodied agent and tool calling. Once the official problem statements are found (see the UNVERIFIED item above), quote the closest one word for word.

**Problem statement (draft):**
> Millions of people live alone, and many of them (older adults, people with low vision, people new to a job) get stuck on everyday physical tasks: an unfamiliar appliance, a tool, a label they can't read. Today's help is either a human who isn't there or a phone chatbot that can't see what they're holding and can't show them. Jolly is a mixed-reality companion that lives in your room: you talk to it naturally, hold up an object, and it segments it, names it, and walks you through using it step by step. Its persona (Mentor, Companion, Guru) is swappable data, so the same body becomes a workplace trainer, a patient tutor, or a friendly presence for someone who is lonely. It keeps you company even when offline.

Who benefits. Pick one hero user for the video; the others go in the README as an expansion path:
1. **Hero user: an older adult living alone.** The pitch is companionship plus "how do I use this?" help with things like a coffee machine or a TV remote. This is the strongest emotional Impact story.
2. Low-vision users: point at an object and hear what it is. Note that Quest is not an accessibility device today; frame this as the glasses roadmap.
3. New workers: the Mentor persona shows a tool and its safety steps.
4. Learners being taught to use real-world tools.

Avoid demoing a "Psychologist" persona. A mental-health claim invites safety scrutiny, and if Contributor-mode models are used, conversation content may be used to train Meta models: "Contributor mode … in exchange for permission to use your content … to train future Meta models" (https://dev.meta.ai/docs/pricing-rate-limits.md). Mention the private/offline path (Muse Glimmer open weights, Apache-2.0 per https://dev.meta.ai/docs/muse-glimmer.md) as roadmap only.

Portability angle: Meta's own cookbook ships a Ray-Ban Meta "look and ask" recipe (https://dev.meta.ai/docs/cookbook/rayban-openclaw.md). "Same brain, from Quest MR today to glasses tomorrow" lines up with Meta's direction.

---

## 3. Demo video script (target 2:40, hard cap under 3:00 until the portal limit is confirmed)

Recording: Quest 3 MR capture with passthrough. Record many takes and edit. Show on-screen captions of the user's speech (from Voice Transcribe output) and small "model badges" when each Meta model fires. **UNVERIFIED:** whether the Quest system recorder captures passthrough together with the app layers at good quality on the target Horizon OS. Test this today.

| t | Shot | Says / does | Meta tooling shown |
|---|---|---|---|
| 0:00–0:12 | Cold open: a living room with an elderly-user framing. Jolly idles on the couch, then turns toward the user. | Title card: "Jolly: a companion that can see your world." | — |
| 0:12–0:30 | **Greeting.** User: "Hey Jolly!" Jolly waves, walks over, mouth synced: "Hi! Want to show me something?" | Live captions appear as the user speaks | Muse Voice Transcribe realtime (`wss://api.meta.ai/v1/asr/realtime`), Muse Spark 1.3 streaming |
| 0:30–0:48 | **Follow me.** "Come with me to the kitchen." Jolly walks along the floor and avoids the table. | Tool-call badge: `follow_user()` / `walk_to("kitchen")` | Muse Spark **tool calling** (https://dev.meta.ai/docs/tool-calling.md) |
| 0:48–1:30 | **Show object (hero moment).** User picks up a moka pot or a TV remote, points with a hand: "Jolly, what is this and how do I use it?" The frame freezes, the SAM mask outline glows on the real object, and Jolly names it and gives 3 steps while gesturing. | Badge: `capture_and_identify()` → mask overlay → answer | Passthrough Camera → **SAM 3.1** point prompt from the hand ray (mask) → **Muse Spark vision** on the masked crop. See note below. |
| 1:30–1:50 | Follow-up question with memory: "Which part did you say gets the water?" Jolly points at the masked region. | Scene memory ("what Jolly has seen") | Spark multi-turn plus scene context |
| 1:50–2:08 | **Persona swap.** "Switch to Guru." A short particle/outfit change, then a calmer voice and wiser phrasing answers the same kind of question. | Persona is a ScriptableObject; nothing is hard-coded | System-prompt profiles; optional **Muse Image** generated persona props |
| 2:08–2:22 | **Barge-in.** Jolly starts a long answer and the user interrupts: "Wait, stop. Shorter please." Jolly stops instantly and gives a one-line answer. | Show the latency stopwatch overlay (first audio under 2 s) | Streaming STT with VAD/endpointing; cancel the in-flight response (`POST /v1/responses/{id}/cancel`, https://dev.meta.ai/docs/api-reference/responses/cancel-response.md) |
| 2:22–2:35 | **Offline idle mode.** Wi-Fi is toggled off on screen. Jolly stays alive: idles, plays a canned line "I can't reach my brain right now, but I'm still here!", and does an animation when the user waves. | Graceful degradation | Shows engineering robustness (Technical Depth) |
| 2:35–2:45 | Closing card: architecture diagram (Voice Transcribe → Spark (tools) → SAM 3.1 → TTS/lipsync), "Open source · OpenXR-ready · glasses next", repo URL. | | |

**SAM pipeline note (verified against the docs).** SAM 3.1 "locates the objects you name; it doesn't answer questions or reason about a scene. For scene understanding, use … Muse Spark … then pass the concept to SAM 3.1" (https://dev.meta.ai/docs/sam/segmenting.md). It takes either a short noun phrase or box/point prompts, "never both" (same page). The design therefore has to be one of these two:
- **(a)** hand-ray point → SAM point prompt → mask → crop → Spark identifies and explains (the most "unexpected capability", because it uses hand pointing as a SAM prompt);
- **(b)** Spark names it → SAM noun-phrase mask for the visual highlight.

Do not describe SAM as "recognizing" objects in the README or the video.

### Meta AI tooling that scores (25%: "beyond a basic integration … surfaced unexpected capabilities")
Ranked by judge impact per hour of work:
1. **Muse Spark tool calling driving a body.** The LLM moves a character in physical space (`walk_to`, `follow_user`, `look_at`, `capture_and_identify`). "Model gets legs" is the strongest beyond-chat story.
2. **SAM 3.1 on live passthrough frames prompted by hand pointing.** A visible mask on a real object is the single most memorable frame in the video. This pattern is not in Meta's cookbooks as far as I found (speculation).
3. **Muse Voice Transcribe realtime.** Streaming, VAD/endpointing and keyword biasing (for the name "Jolly") come from https://dev.meta.ai/docs/models.md. Use the endpointing to drive barge-in and turn-taking. Also try keyword biasing as a wake word.
4. **Muse Spark vision plus scene memory** across turns (Responses API multi-turn, https://dev.meta.ai/docs/protocols/responses.md).
5. **Contributor model** (`muse-spark-1.3-contributor`) to cut cost. Mention it in the README as a cost-engineering decision. Its rate limit is only **150 RPM**, versus 3,000 for standard (https://dev.meta.ai/docs/pricing-rate-limits.md). That is fine for one headset, but have the standard model as a config fallback for the live final.
6. **Muse Image (optional, could-have).** Generate persona props, or a "show me what it looks like assembled" card. Rate limit is 150 RPM. Low risk only if pre-generated offline; otherwise skip.
7. **Muse Glimmer** (open weights, Apache-2.0): roadmap slide for "private/offline Psychologist mode". Do not build it now.

**Gap: Meta has no TTS.** Muse Voice Transcribe "does not synthesize speech or provide a speech-to-speech conversation API" (https://dev.meta.ai/docs/models.md). TTS and lipsync must come from a third party or the platform. The README should say this plainly and keep TTS behind an interface.

---

## 4. Lessons from previous Meta hackathon winners

| Event | Winner | What won | Source |
|---|---|---|---|
| Presence Platform Digital Hackathon 2024 | **Flaivor** (Hobbies & Skill Building) | MR cooking assistant: Passthrough in the real kitchen, an AI assistant "Martha" giving real-time feedback, ingredient recognition from snapshot or voice (Voice SDK) | https://developers.meta.com/horizon/blog/presence-platform-2024-digital-hackathon-winners-meta-quest/ |
| same | **Dreamscape** (Utility & Design) | Passthrough + Hand Tracking + Interaction SDK; worlds generated from spoken prompts with AI | same |
| same | **Blobble** (Casual & Social) | Scene API so balls bounce off real furniture; "each room produces a different experience" | same |
| Presence Platform Hackathon 2023 | Digital Wardrobe; SUBMERSED | Hand gestures; the user's real room turned into the game | https://developers.meta.com/horizon/blog/announcing-our-presence-platform-hackathon-results |
| Llama Impact Hackathon London 2024 | 1st **Guardian**, 2nd **Gripmind**, 3rd **Pharmallama** | Guardian: an NHS triage agent, "a second pair of eyes". Gripmind: Llama 3.2 Vision driving a robot arm for assisted living. Pharmallama: an on-device pharmacist chat | https://about.fb.com/news/2024/11/metas-llama-impact-hackathon-pioneering-ai-solutions-for-public-good/ |
| Llama Impact Hackathon SF 2024 | Llama Navigator AI, Compllama, Farmipole | Concrete public-service and accessibility use cases | https://lablab.ai/blog/llama-impact-hackathon-sf-summary |

Patterns:
- **Real room, real objects.** The MR winners make the user's actual space matter (Scene, Passthrough). Jolly should visibly use room geometry when walking and a real object when segmenting.
- **AI that sees and talks in context.** Flaivor is the closest precedent: an MR AI assistant that recognizes real items and gives step-by-step help. It won its category. Jolly's differentiator is embodiment plus segmentation plus tool-driven movement.
- **Named beneficiary and a sharp use case.** The Llama Impact winners are all "X for Y": triage for NHS staff, a robot arm for assisted living. Pick one hero user, as in section 2.
- **Vision model driving a physical action** (Gripmind) maps directly to "Spark tool calls move Jolly".
- Judging criteria were not published for those events (as noted in each source). The patterns above are inferences, not rules (speculation).

---

## 5. Top risks
1. **Live final "holds up live".** It depends on Wi-Fi, API latency and passthrough camera access. Build a deterministic demo mode: a scripted fallback, cached last answer, and offline idle mode. Rehearse 10 full runs in a row.
2. **Unknown submission fields and video limit (UNVERIFIED).** Check the portal today and do not finish the video blind.
3. **Problem-statement list not found publicly (UNVERIFIED).** Impact (30%) explicitly scores fit with it. Get it from Discord or the portal.
4. **Jolly asset and trademark.** The rules' warranty (c) covers trademark. Document provenance, keep the character swappable, and have an original fallback model.
5. **Passthrough Camera Access + SAM round-trip latency** (SPEC item 11). This is the hero shot and the riskiest technical piece, so spike it first.
6. **Barge-in on Quest** needs echo cancellation, so Jolly's own TTS does not trigger STT. **UNVERIFIED** whether the Unity `Microphone` on Quest gets platform AEC. Fallback: push-to-talk (pinch) during Jolly speech.
7. **No Meta TTS.** Third-party TTS adds license and latency risk and slightly dilutes "Meta tooling". Disclose it and pick an open-licensed or platform TTS.
8. **Contributor mode** sends content to Meta for training, and is capped at 150 RPM. Fine for the demo, but disclose it and avoid sensitive personas.
9. **Timeline.** About 3.5 days remain until Nov 4, 9 PM ET. Set an internal deadline of Nov 4, 3 PM ET, because portal uploads at the deadline are a common failure (speculation).
