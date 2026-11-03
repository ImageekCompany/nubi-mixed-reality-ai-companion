# 0042. Settings on demand: wrist button, menu button, voice; world-locked when open

- Status: accepted; "Side views" superseded by [ADR-0050](0050-demo-layout-from-the-measured-window.md)
- Date: 2026-11-01

## Context
- From the headset: "Settings are always before my eyes, I can't get rid of them, they should appear only if I need them."
- `SettingsPanel` was a head-following canvas (`LazyFollow`, 0.55 m ahead, 0.15 m down) with no way to close it. A Quest system recording of the app on 2026-11-01 (1920×1080, 60 fps) shows the 0.34 × 0.52 m panel over half the frame, Nubi behind it, and the captions hidden under it.
- The same recording cuts off the latency HUD (outer edge 33° off centre at x = −0.6 m) and the tool badges (29° at x = +0.55 m). Measured on its frames, a system recording shows about ±30° across and ±18° up and down, much less than the headset's own field of view.
- The demo plan opens the panel on camera only for the cost shot (beat 6).

## Decision
- **Hidden at launch.** `SettingsWindow`, on the panel's root, turns off the canvas and its raycaster: a hidden panel draws nothing and catches no ray, so pinch push-to-talk is not gated by it. `SettingsPanel` keeps serving `IUserPreferences` while hidden.
- **Opening.** Three ways, all routed by `SettingsLauncher`:
  - *Wrist button.* `WristMenuButton.prefab` (8.5 × 3.6 cm) sits 3.5 cm off the inside of the left wrist while the left palm faces the head (`WristAnchor`: appears within 50°, goes past 65°, stays 0.4 s through a tracking loss, since the pressing hand often covers the wrist). It is a world-space UI button like the panel's, so the other hand pokes it or pinches it with the ray. Pressing toggles.
  - *Menu button.* `MenuButton` binds the left Touch controller's ☰ (`<…TouchController>{LeftHand}/menu`, OpenXR `/user/hand/left/input/menu/click`, which Horizon OS leaves to the app) for the three Touch layouts, by name as in ADR-0021, and the left-hand palm pinch that Horizon OS reports as `MetaAimFlags.MenuPressed` on `<MetaAimHand>{LeftHand}/aimFlags` (the Meta hand-tracking aim feature is on). Pressing toggles.
  - *Voice.* A local `SettingsGuard` before the model: a short final made of "open settings", "show the settings", "settings", "open the menu" (Polish "otwórz ustawienia", "pokaż ustawienia", …) or the close forms, plus fillers (`Crisis` table keys `settings.open`, `settings.close`, `settings.fillers`, with built-in fallbacks). Close is checked first, because "close settings" contains "settings". The turn ends with no model round and nothing spoken: the panel is the answer.
- **Placement.** On open, once: 0.7 m ahead of the eyes along the level heading, 0.15 m below eye height, upright and facing the user. Then it stays in the room (world-locked); `LazyFollow` is removed. Opening an open panel brings it back in front.
- **Closing.** Its X button, the wrist or menu button again, "close settings", any talk turn (the state reaching Listening), and 20 s with no control on it used (`PanelVisibility`).
- **Log.** `evt=settings.open source=wrist|menu|voice`, `evt=settings.close reason=button|wrist|menu|voice|talk|idle`, `evt=guard.settings open=`.
- **Side views.** HUD at x = −0.4 m and scale 0.0007, badges at x = +0.4 m and 0.0008, captions 5 cm higher (y = −0.25 m): every head-following view's edge is within 25° across and 18° up or down (`ViewLayoutTests`).

## Consequences
- The panel is out of every take unless opened, and the beat 6 shot opens it from the wrist or menu.
- A talk pinch closes the panel, so the user cannot talk while reading it; they reopen it after.
- The voice command costs one ASR session and no model call.
- Unverified with a wearer when written: the wrist button's position and readability, the left-hand menu gesture reaching the app, and the controller ☰ binding on Horizon OS v209. The PlayMode tests drive the Input System layouts and synthetic hand poses, not the runtime.

## Alternatives
- **Head-locked but smaller or further away.** Still always in view; the complaint is that it is there at all.
- **A model tool (`open_settings`).** A model round (≈ 1–2 s and a cost) for what a short phrase list decides locally, and the model might answer instead of calling it.
- **Only the system menu gesture.** Hands only, and the gesture is not discoverable; the wrist button is visible where the user looks.
