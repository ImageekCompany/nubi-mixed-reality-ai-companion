# 0043. Local guards take the name as heard; each character's own come-here self-test line

- Status: accepted (amends the come guard of ADR-0036)
- Date: 2026-11-01

## Context
- The last `--character jolly` device run (`f27ea04`, 23:18 UTC+1) sent "come here" to the model: `voice_come_here` DEGRADED, `come_source=model`, release → first audio 2,334 ms instead of a guard walk under 600 ms.
- Cause, from the run's `selftest_results.json`: the self-test plays the Nubi clip "Hi Nubi, come here." for every character, and live ASR returned `Hi, newbie. newbie. Come here.`. With Jolly selected, the transcript aliases are Jolly's (Jolie, Jollie), so "newbie" stayed as it was, and the come fillers hold Jolly's name (`{name}`), not Nubi's. Two words the guard did not know meant "the person said more than the command", so the model decided.
- The same happens to a person: ASR turns a name it does not know into whatever word it knows ("newbie", "Nobi", and for Jolly plausibly "jelly", "Holly" or "Charlie"). Each character's alias list only fixes the mishearings someone has already seen.

## Decision
- **The name as heard.** In `ShortCommand` (stop, come and settings guards) a clause, the words between , . ! ? ; or :, that holds no trigger and exactly one word the tables do not cover counts as the person calling the character. ASR sets a called name apart with punctuation, so "Hi, newbie. newbie. Come here.", "Hi Nubi, come here." (Jolly selected), "Jelly, come here!" and "Come here, Holly." match. "Holly come here" (no punctuation), "Hi Holly Jelly, come here." (two unknown words), "Wait, come here and look at this." and "don't come here" do not. A command said with no name ("Come here.") matched before and still does. The 6-word cap stays.
- **The selected character's name** is already a filler (`{name}` in the `Crisis` table, ADR-0026), and its aliases are rewritten to it before the guards; the local Jolly definition now also lists Jelly, Jolli, Joly and Jolley (a local asset, not in git). Real names (Holly, Charlie) are not aliases: rewriting them would change what the user says about other people, and the clause rule covers them as a called name.
- **Self-test line per character.** `SelfTestAssets.comeHereByCharacter` maps a character id to its own come-here clip; `come_here_jolly.wav` ("Hi Jolly, come here.", Wit Charlie like the others) is the entry for `jolly`. `voice_come_here` plays the selected character's line, else the shared one.
- **A clip that names another character** is a test artefact. If one is played and the model answers, the scenario records `guard=name_mismatch` instead of degrading for the guard; it still fails without a walk, and the transcript and the 2.0 s first-audio budget still apply. With its own line, a model path degrades as before.

## Consequences
- Beat 1 for a Jolly cut no longer depends on ASR spelling "Jolly" right, as long as it punctuates the name.
- A one-word clause can be something other than a name: "Never, come here." and "No, stop." now act. Both read as the command anyway, and a turn that only halts locomotion (stop) still goes to the model.
- A character added later needs no code for its name; a self-test line for it is optional.

## Alternatives
- **Fuzzy-match the name** (edit distance to `{name}`). "newbie" is 5 edits from "Jolly"; the threshold that accepts it accepts most short words.
- **Allow any one unknown word.** "Never come here" and "Don come here" (from "don't") would walk.
- **Add every mishearing as an alias.** Only fixes the ones seen, and real names in aliases rewrite what the user says.
