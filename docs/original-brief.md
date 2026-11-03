# Original brief

The starting brief for the project, kept as written. Several things changed afterwards: the repository ships Nubi rather than the mascot it names (ADR-0011, ADR-0052), "contributor models" here meant cheaper coding agents, not Meta's `*-contributor` models, and the model files it mentions were never committed.

---

We are making a tool for Meta Global AI Developer Hackathon, Indie Developer Track
Here are judging wieghts:
30% - Impact (Real world potential, who benefits, and fit with a problem statement)
25% - Use of Meta AI tooling (Creative use beyond a basic integration)
20% - Technical Depth (Sound, refined engineering with real craft)
25% - Demo (A working demo that holds up, and a demo video worth watching)

Three stages from submissions to winners:
Stage 1 - Pass / fail (Does the project reasonably fit a theme and use Meta AI products?)
Stage 2 - Rubric scoring (Judges score every passing project from 0 to 10 on the rubric)
Stage 3 - Live final round (Top 5 per track. Judges watch the demos live on Nov 9 and rank them)

Or Idea is a product for Meta VR headsets (Test and demo recording on Meta Quest 3, main target device new Meta VR Glasses), where in MR we can see AI character (we decided it's gonna be Meta mascot Meta Muse Jolly) - Meta+Muse+Jolly+v2+Whole.glb, Meta+Muse+Jolly+v2+Whole.stl) and can talk with it, go with him around the apartment, show him stuff and ask what is it and how to use it and etc. Real-world potential is huge because we can give him different task and personalities and implement him as Pscyhologist, Guru, Senior worker who shows how to work and do different jobs and etc. People love to talk with their AI right now and we can move it a step further giving AI body and people sense of human connection.

Read the official rules and problem statements first, then map the project to a specific theme and write that mapping into the README.

Here are some requirements:
1. We use Unity 6.6
2. It's an open source project, so you should use only publicly available plugins and tech
3. Tech quality is a must and one of the criteria of this competition, so make sure to apply all best practices regarding stability, security and accessibilty. Architecture is clean, files are short, no repetition, UniTask for async, good memory usage / optimization, no spaghetty code, clean API interfaces and testability.
4. We need to use Meta AI, so character should talk through it, we also want to use SAM for image recognition when we show some of the real world object (like pen, table, controller) and ask what is it and how to use it and etc. So make sure to implement it and possible different others that way be of help. Of course later on we also considering making it fully secure and offline working for stuff like Psychologist and other sensible professions, so it all should be wrapped up nicely with abstraction and good replacibility by anything.
5. Our character should walk and talk, good animations, opening mouth correctly with words, you can use any available tool and re-do from the zero it if it's not permitted for open source.
6. If something possible to do in parallel - do it, but don't loose quality over it.
7. Use contributor model's for cheaper token costs
8. Use local git from the start, write sensible commits and everything, project also gonna be reviewed. Conventional commits, small PRs/branches even locally, an ADR log (/docs/decisions) for each key choice, and a CHANGELOG.
9. You can re-do the Jolly model completly if needed, remember it is open source project and we are not gonna make money using this character, when project is gonna be used for real release - we'll use our own original character.
10. Verify the licenses of everything used (Meta XR SDK, Jolly asset terms, SAM, Llama/Muse, TTS/STT, lipsync). Meta SDKs are often not OSI-licensed, so document this honestly in a THIRD_PARTY file.
11. Use Passthrough Camera Access (permissions, minimum Horizon OS version) for SAM input. This is the technically riskiest piece, so spike it first. Previously it was impossible to get camera image from headset because of privacy reasons, but current documentation show's that it's possible (https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.7/manual/features/camera/image-capture.html). If it's not possible by ordinary means, make it possible.
12. Everything should work with Hand Tracking 100%, but controllers support is also possible.
13. Performance budget: 72/90 fps on Quest 3, thermals, battery, a draw-call and memory ceiling, and Quest profiling (OVR Metrics, Unity Profiler).
14. Define the full voice loop: wake word or push-to-talk, STT, LLM, TTS, lipsync. Streaming at every stage, barge-in (interrupting Jolly), and a latency budget (e.g. under 2s to first audio).
15. Use tool/function calling so the LLM can trigger actions: “come here”, “look at this”, “capture and identify”, “walk to the kitchen”.
16. Conversation memory, persona/system-prompt profiles (Psychologist, Guru, Mentor) as swappable data assets, and context from the scene (what Jolly has seen).
17. Any API keys and other secrets should stay local-only
18. Localization-ready architecture (English first, Polish possible) for strings, STT and TTS. (Unity Localization)
19. Where possible use default Unity solutions
20. Don’t invent APIs: verify every SDK call against current docs or source, and flag uncertainty rather than guessing.
21. Define “done” objectively: a checklist of tests passing, a performance target met, the demo script run end-to-end, and the README complete.
22. Report format: a short progress summary at milestones, with blockers and anything requiring human action (headset testing, API keys, accounts, asset licensing decisions).
23. If network is not available - character still must be live but with pre-defined speech of greetings and talking who he is, but only when person interacts with him, like periodical phrases and actions / animations when in games player not doing anything.
24. A “wow vs risk” feature-priority list (must/should/could) so the contributor cuts scope instead of quality when time runs short.
25. Later on we may want to port this project to other platforms (like Android XR, Pico and others), so keep this in mind choosing SDK's and architecture. (For example OpenXR is best choice hardware wise)

If anything is unclear or subject to discussion - ask it from the begining. After initial answers are given you should make every decision yourself to the best of your abilities considering our main goal is to win in the competition.
You should not stop until product is production ready, you should build self-checking pipeline and full e2e testing before saying that it's done and ready to see.