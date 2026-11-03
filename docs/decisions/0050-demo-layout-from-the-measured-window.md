# 0050. Demo layout from the measured recording window; captions and tool badges draw over the character; latency stats off by default

- Status: accepted (supersedes the "Side views" part of ADR-0042; amends ADR-0024's rejection of `ZTest Always`, for captions and tool badges)
- Date: 2026-11-03

## Context
- ADR-0042 fitted the head-following views to "±30° across and ±18° up and down", centred on head-forward. Measured on 2,605 frames (4 fps) of two system recordings of the Jolly build, the recorded window is off-centre: head-forward sits at 35.2 % of the frame height, not at the middle.

  | | 09:40 recording | 08:52 recording |
  |---|---|---|
  | Top / bottom | +13.53° / −23.99° | +13.66° / −24.08° |
  | Left / right | −32.6° / +30.1° | −32.6° / +30.4° |

  So the top is +13.6° ±0.3 and the bottom −24.1° ±0.4. `ViewLayoutTests` passed the status pill with its top edge at 14.2°, and the pill's top quarter is cut at rest. Badges were cut or at the top edge in 32 of 289 and 52 of 366 badge frames.
- The recorder stabilises the video, so head-forward moves in the frame: the shift is p50 0.9–1.1°, p90 6–14°. Follow lag adds 0.07 m at p50 and 0.25 m at p90 on the caption. LazyFollow offsets are targets, not positions.
- Jolly is in view in 1,807 of 1,991 app-active frames (90.8 %), and its top edge sits around head-forward (p50 −1.0° to +0.1°). The captions at y = −0.25 m were on it: in 58 % of frames with caption text and Jolly in view (602 of 1,046), a text row ends at the silhouette, and words disappear behind the fur. This is the behaviour ADR-0024 documents: captions are `LEqual` on a world-space canvas, and the skin and the fur write depth.
- Raising the captions trades touching Jolly against leaving the frame. Touch is % of Jolly-in-view frames, both recordings pooled, with lag and stabiliser simulated; L is font 48, S is font 28, the digit is the line count. The 95 % CI is about ±4–7 points at y ≤ −0.15 and up to ±11 at y ≥ −0.10. The take uses Large (the demo checklist), so the L columns apply.

  | Caption y (m) | L1 | L2 | S1 | S2 | User line top in frame, 1 / 2 lines (%) |
  |---|---|---|---|---|---|
  | −0.25 | 84 | 91 | 83 | 89 | 99.7 / 99.5 |
  | 0.00 | 46 | 62 | 42 | 55 | 97.4 / 94.3 |
  | +0.05 | 35 | 53 | 31 | 42 | 94.6 / 87.9 |
  | **+0.08** | 30 | 45 | 21 | 35 | 92.3 / 81.8 |
  | +0.10 | 23 | 41 | 18 | 30 | 89.4 / 73.9 |
  | +0.15 | 16 | 28 | 9 | 19 | 77.7 / 39.2 |

- No candidate removes the overlap, so a raise alone still loses words.

## Decision
- **Layout.** The measured window replaces ADR-0042's offsets and scales. Angles are off head-forward, computed by script from the prefab values:

  | View | Target offset (m) | Root × scale | Across | Up and down |
  |---|---|---|---|---|
  | Captions | 0, +0.08, 1.3 | 1000 × 300 × 0.001 | ±21.04° | −3.08° to +10.03° |
  | ToolBadges | +0.48, −0.16, 1.3 | 340 × 244 × 0.0007 | +15.52° to +24.74° | −10.69° to −3.28° |
  | LatencyHud | −0.44, −0.18, 1.3 | 460 × 300 × 0.0007 | −24.81° to −12.11° | −12.37° to −3.30° |
  | StatusIndicator | 0, +0.29, 1.4 | 700 × 70 × 0.001 | ±14.04° | +10.32° to +13.07° |

  `ViewLayoutTests` now holds each view within 25° across, 13.6° above and 24° below, and requires four pairs to clear each other; the tightest gap is 0.20°, captions over badges. `Main.unity` overrides the root size of all five views, so a prefab-only resize passes that test and does not ship: `SceneViewSizeTests` boots the scene and compares each root with its prefab.
- **Captions draw over the character.** A new material, `LiberationSans SDF - Caption Overlay`, uses `TextMeshPro/Mobile/Distance Field Overlay` (`ZTest Always`) on both caption lines, with face dilate 0.25, outline 0.2 and a soft underlay (mesh padding 8.31, limit 9). This amends ADR-0024 for captions and tool badges (below): every other UI graphic keeps its depth test. The queue is overridden to 3000, not the shader's 4000: captions stay after the character and the occluder (2999), and a nearer settings panel still covers them. A test pins the shader, queue and atlas, because on an atlas mismatch TMP silently reverts to the font material.
- **Badges draw over the character** and become one line: a title that auto-sizes between fonts 16 and 24 and a no-wrap detail, in darker state colours. The labels fit by hand calculation (widest about 187 of 224 units at font 16). The stack can only sit in Jolly's body band: captions fill ±21° above it and the recorded window has no room for it beside them. So the text uses `LiberationSans SDF - Badge Overlay` (the same TMP shader as the captions, no outline or underlay) and the plates use a new `Jolly/UI/Overlay` shader through `M_UiOverlay`, both queue 3000. A batch render against a near cube: hidden with the old materials, drawn over it with the new, and pixel-identical to the old with nothing in front. Badges are how the video shows the tooling, so a chip hidden for its whole 4 s loses that beat.
- **Title overflow.** Titles are bold and set to Ellipsis, but TMP looks up U+2026 at the bold weight, finds no bold typeface on the font and returns no character, though the font has the glyph. So each Title logs a warning on first layout and drops to Truncate. No demo label reaches the limit (`LongestLabels_FitEverySlotWithoutTruncation`), so nothing shows. A longer label would be cut without "…"; the fix is a bold typeface entry or a non-bold Title, not a new glyph.
- **Latency stats.** The HUD is hidden unless the new Settings row is on: `UserPreferencesSnapshot.ShowLatencyStats`, default false, stored as `jolly.prefs.show_latency_stats`. `LatencyHud` disables its canvas (as `SettingsWindow` does, ADR-0042) and keeps its text current while hidden, so turning it on shows the last turn. The HUD keeps its depth test: it is opt-in, and drawn over Jolly it would cover the character for a whole take. A nearer Jolly hides parts of it (its rect is more than 5 % over Jolly in 59–64 % of in-view frames). `Construct` takes `IUserPreferences`. Numbers on screen come from logcat (`evt=turn.latency`, `evt=vision.timing`) unless stats are on for that take.
- **Settings panel.** The sixth row grows the root from 860 to 988 in the prefab and the scene. At 860 the Close button covers 38.1 units (22.9 mm) of the Caption size row's Next button and wins the raycast; at 988 the overlap is 0.
- **Labels.** `tool.go_to_surface` and `tool.request_room_scan` were missing from the UI table, so those badges showed the raw key. Both are added in English and Polish.

## Consequences
- **Stereo comfort gets worse up close.** Text and chips at 1.3 m drawn over a nearer Jolly conflict with their own depth: disparity says behind, occlusion says in front. Reading the text up close can double Jolly. The raise reduces how often this happens; it does not remove it.

  | Jolly distance | Disparity against the caption plane | Accommodation gap |
  |---|---|---|
  | 0.7 m | 2.38° | 0.66 D |
  | 1.0 m | 0.83° | 0.23 D |
  | 1.2 m | 0.23° | 0.06 D |

- speculation: a Quest recording is monocular, so the video will not show the conflict; only a wearer can judge it.
- Captions no longer lose words to Jolly. speculation: nor to fur hidden by a real object, which still writes depth.
- **Chips cover Jolly.** A chip that overlaps Jolly now paints over it, its face included, for up to 4 s (all five in the worst case, with Jolly to the wearer's right).
- **First-draw compile.** `QuestVulkan.graphicsstate` has no Overlay variant (and none for ShellFur or `Jolly/UI/Overlay`), so expect compiles at the first caption and the first badge, size undetermined. Do one rehearsal launch with a tool call before recording, or re-record the warmup (`docs/perf/README.md`), which spends API money.
- **Cost** (speculation until measured): one extra `tex2D` and two interpolators, average quad area up about 49 %, under 0.1 ms and only while a caption shows.
- **Contract:** `UserPreferencesSnapshot` gains a field (`docs/contracts.md`). The flag persists in PlayerPrefs and `tools/demo/reset.sh` keeps it, so `docs/demo/capture-checklist.md` checks it is off.
- **Unverified with a wearer when written:**
  - Comfort of reading captions over a close Jolly, and whether the settings panel really covers captions and badges.
  - `Jolly/UI/Overlay` in both eyes: it copies the TMP Overlay shader's stereo macros and blend, and was rendered only in mono in the editor.
  - Legibility of white text on the fur: the outline and underlay are computed, not viewed. Glyph quads overlap, so one glyph's halo can paint over its neighbour; if edges look dirty, set `m_characterSpacing: 2` on both lines.
  - The window itself across people: both recordings are one wearer in one room. The touch rates are simulated, and per-frame position errors reach 90–250 px at p90 when the follower state is wrong.
  - The stabiliser's default parameters, and whether turning it off works.
  - The layout on a headset: it was checked by script, not in a new recording.

## Alternatives
- **A smaller raise.** At y = −0.05 and below, L2 touches in 81–91 % of frames. At +0.05 it is still 53 %, over half of two-line captions, and words would still vanish without the overlay.
- **A larger raise.** +0.10 is not separable from +0.08 on touch within the CI and loses a two-line user caption's top in 26 % of frames. At +0.15 that top is in frame only 39.2 % of the time.
- **A tighter follow** (`_MaxDistanceAllowed` 0.1). Keeps text in frame better (94 % against 89 % at y = +0.10) and does not reduce touching (L2 47 % against 41 %).
- **Moving the caption plane closer.** It must beat Jolly's front at about 0.46 m with scale about 0.00035. That is about 5° of disparity and 1.39 D against Jolly at 1.2 m on every read, and up to 0.3 m of follow lag could bring it nearly to the face.
- **A backing plate.** The most legible, but it needs `ZTest Always` on a UI Image and layout code.
- **Depth-tested badges.** The inner edge at 15.52° clears the character's 0.32 m collider half-width only with Jolly centred at 1.2 m (14.9°); at the beat 1 stop distance of about 1 m it is 17.7°, and Jolly often stands to one side. On the stored masks (frames with mask area ≥ 3000, a static rect) the top chip's rect is more than 50 % on Jolly in 26.5 % and 4.3 % of frames, against 2.0 % and 2.0 % at the old position. Staging the take around it was rejected: the chips follow the head, and whether Jolly is nearer than 1.3 m is not something a wearer can judge. The overlay costs a small shader, extra pipelines and a wider ADR-0024 exception.
- **`SetInt("unity_GUIZTestMode", 8)` at runtime.** Unverified, and the shader already exists.
- **Hiding the HUD with `SetActive`.** Its Background still draws from scene load until `BindViews` (170 ms plus frames in the last device log), and after a long disable LazyFollow can leave it up to 0.30 m off, past the recorded edge. An inactive prefab skips `OnDestroy` until first activation.
