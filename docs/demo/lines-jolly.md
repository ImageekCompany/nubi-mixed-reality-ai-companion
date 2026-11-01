# Jolly variant of the spoken lines

Use only if the human chose to record the Jolly cut ([`production-plan.md` § The Jolly question](production-plan.md#the-jolly-question-decision-for-the-human)). The skin, its definition and its baked lines are local and git-ignored (`docs/art/jolly-demo.md`, ADR-0026); this file holds only text.

## Lines the wearer says

| Beat | Nubi cut | Jolly cut |
|---|---|---|
| Warm-up (off camera) | "Hi Nubi, who are you?" | "Hi Jolly, who are you?" |
| 1 | "Nubi, come here." | "Jolly, come here." |
| Meet | (nothing; look at the character 1.5 s: gaze greeting) | same (Jolly's baked greeting) |
| 2 | "What is this?" … "Which button turns it on?" (two pinches) | same |
| 3 | "Follow me." … "Stop." | same |
| 4 | "Tell me everything about this remote." … pinch … "That's enough, thanks." | same |
| 5 | "Are you there?" … "Thanks, Nubi. See you later!" | "Are you there?" … "Thanks, Jolly. See you later!" |
| Live fallback, beat 1 | "Nubi, come to me." / "Come closer." | "Jolly, come to me." / "Come closer." |

## Voice-over

| # | Jolly cut |
|---|---|
| V1 | unchanged: "One in four Americans 65 and older lives alone. When a new remote or a coffee machine gets the better of them, there's nobody in the room to ask." |
| V2 | unchanged: "Hold something up, point at it, and ask." |
| V3 | unchanged: "Ask it to follow, and it walks with you, around your real furniture." |
| V4 | "When the network drops, Jolly doesn't go silent." |
| V5 | "For anyone at home with nobody to ask. Built on Muse Spark, SAM 3.1 and Muse Voice Transcribe, and open source as Nubi." (23 words, about 9 s; the repo ships Nubi, so the video must not say the Jolly build is the open-source one) |

## What the character says

The canned lines are the same text in both cuts, with `{name}` filled from the selected character (`CharacterNamedLocalizer`, ADR-0026). Jolly speaks them in its own Wit voice ("Railey"); the lines that contain the name are baked per character into `JollyCharacter.asset` `lineClips`, so Jolly never plays a clip that says "Nubi".

| Situation | Nubi | Jolly |
|---|---|---|
| Beat 1 reply (baked, one of) | "On my way!" / "Coming!" / "Coming right over!" | same |
| Greeting (warm-up) | "Hi! I'm Nubi. It's lovely to see you." | "Hi! I'm Jolly. It's lovely to see you." |
| Who-are-you (offline, one of) | "I'm Nubi, your little helper. Show me something and I'll tell you what it is." | "I'm Jolly, your little helper. Show me something and I'll tell you what it is." |
| Beat 5 offline (one of, no name) | "I can't reach the internet right now, but I'm still here with you." / "My connection is down, so I can't think very well. Let's try again in a bit." / "Sorry, I'm offline at the moment. Please check the Wi-Fi." / "I lost my connection. I'll keep you company until it's back." | same |
| Beats 2, 3, 4 answers | model-authored, transcribe from the take | model-authored, transcribe from the take |

## Device checks for the Jolly build (device agent, before any Jolly take)

All **UNVERIFIED** until seen on the headset:

- `tools/device/build.sh --character jolly` builds, and logcat shows `evt=character.selected id=jolly` at launch (ADR-0026 says this was never checked on the headset).
- **ASR of the name.** `JollyCharacter.asset` lists the aliases "Jolie" and "Jollie" only. Say "Jolly, come here." five times and read the live caption and `evt=asr.final`: if Muse Voice Transcribe hears "jelly", "Holly" or "Charlie", the come-here guard does not match and beat 1 falls to the model (slower, may not walk). Adding aliases is a change to a local asset, owned by the lead, not this doc.
- The caption reads "Jolly", not an alias, in the live partial.
- The greeting and the who-are-you lines play the Jolly clips (Railey voice, says "Jolly"), never Nubi's.
- Beat 2 vision answer: the persona prompt says the name from `{name}`; check the answer never calls itself Nubi.
- Size: the skin's body values (`scale`, `agentRadius`, `height`) differ from Nubi's; check it stops about 1 m in front of you and is not cut by the sofa occluders earlier than Nubi.
