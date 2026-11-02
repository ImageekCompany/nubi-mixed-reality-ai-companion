# 0047. The character's body actions are a tool namespace that follows from its clips

- Status: accepted
- Date: 2026-11-02

## Context
- Asked "show me how to dance", "jump!" or "go stand behind me", Jolly could only answer in words or reach for `play_gesture`, whose seven upper-body gestures are expressive punctuation, not things the user asks for. A model told nothing about a body also claims moves it never made: in the namespace probe (ADR-0048) it answered "Go stand behind me" with "I'm behind you now" and no call.
- Characters are swappable (ADR-0026) and each comes with its own clips. The clips agent's `66cd24d` added `Dance` (4 s loop), `Jump` (1.33 s, feet 0.20 m off the floor) and `Bow` (2 s) to Nubi and the local Jolly; a future character may not have them.
- The Responses API groups tools in a `{"type":"namespace"}`; eager namespaces cost about 0.1 s and were chosen reliably, the call comes back as `actions.dance` (live probe 2026-11-02, ADR-0048). The contracts for namespaces (`ToolNamespace`, `IToolRegistry.RegisterNamespace`, `ModelRequest.Namespaces`) were committed in `04af35c`.
- The Gesture layer is masked to the upper body (`bones.upperBody`, `Spine` and below). Played there, a jump keeps its legs on the floor and a dance loses its hips and foot taps.

## Decision
- **An MCP-like action catalog.** `CharacterActionCatalog.For(character, …)` builds the active character's actions as tools in the namespace `actions`, once, in `ToolCatalog.Build` (the character is chosen and constructed before it runs). The model sees one namespace whose description is generated from the offered actions ("Things your body can do: jump, dance, take a bow, spin around, celebrate and move relative to the user (…). When the user asks you to do one, or asks whether you can, call it in this turn to show them; never say you did it without calling it.") and calls `actions.<verb>` like any tool. Adding an action is one tool class and one `Offer` line.
- **Availability follows the clips.** `ICharacterController.CanPlay(GestureId)` is true when the character's Animator has the gesture's state, read once from the controller (`Animator.HasState`). An action is offered only when the character can play every gesture it needs; `actions.spin` and `actions.move_relative` need none of the optional clips. Startup logs `evt=actions.offered count= tools=`.
- **Actions (v1):**
  - `actions.jump {}`, `actions.bow {}`, `actions.celebrate {}` (Jump then Happy): `GestureActionTool`, awaited to the end so the model speaks after the move.
  - `actions.dance {duration_s? 2–12, default 4}`: `ICharacterController.PlayLooped(Dance, seconds)`. A new `GestureHold` Animator bool keeps a looping gesture state from leaving; its exit time (below 1) is then checked on each loop, so the dance ends where a loop ends, never mid-move. No `style` parameter: there is one dance clip, and a style the body cannot show would be a promise.
  - `actions.spin {turns? 1–3, direction? left|right}`: procedural. `SpinMotion` turns the root by the requested degrees over 1.2 s per turn with smoothstep easing while Happy plays; attention is off during the spin and brings the body back to the user after it (ADR-0041).
  - `actions.move_relative {direction: front|behind|left|right|closer|away, distance_m? 0.5–3, default 1}`: `RelativeTarget` resolves a floor point from the user's head yaw only (front/behind/left/right at `distance_m` from the user; closer to 0.7 m from the user; away backs off along the user → character line). The point snaps to walkable floor within 0.5 m and must stay 0.5 m from the user (the tap-target minimum, ADR-0044); otherwise the tool says `unreachable`, and "closer" when already close says `already_close`. Locomotion concurrency, returns when the walk starts, watches the user so it ends facing them. Withheld in seated mode with `walk_to`.
  - The existing top-level tools (`play_gesture`, `come_here`, `walk_to`, …) are unchanged. `come_here` and `move_relative closer` overlap; both are correct for "come closer".
- **Concurrency.** Clip and spin actions are `Exclusive`, so "jump and then spin" plays one after the other in a round; move_relative is `Locomotion`.
- **Animator.** Full-body gestures (`GestureClips.IsFullBody`: Dance, Jump, Bow) play on a new unmasked **Action** layer above Gesture, with an `Empty` state that writes nothing. They are **optional** clips (`CharacterClips.Optional`): the builder wires those the definition has; the validator warns on a missing one and errors only when the model has the clip but the controller does not play it. `GestureId` gains `Dance`, `Jump`, `Bow` at the end (the values are Animator conditions). Face presets read the Action layer first. Local characters' controllers rebuild with `CharacterAnimatorBuilder.BuildLocal`.
- **Prompt.** When the request carries the `actions` namespace the instructions add: "You have a body: when the user asks you to move or do something one of your actions tools does, call it in this turn; never say or imply you did an action you did not call." A persona with an explicit tool list (Psychologist) offers an action only if it lists `actions.<verb>`; it lists none.
- **Telemetry and UI.** `evt=action.<verb> … status=` per call; badges `tool.actions.<verb>` (en, pl).
- **Self-test.** `action_dance` ("Show me how to dance!") passes when `actions.dance` ran and the Action layer entered the Dance state; `action_move_behind` ("Go stand behind me.") passes when `actions.move_relative` ran and the character arrived at least 135° off the user's forward (as they faced when speaking), within 1.8 m. The clips name no character, so they serve Nubi and Jolly alike.

## Consequences
- "Dance", "jump", "take a bow", "spin around" and "go behind me" do what they say, and a character's repertoire grows by adding a take with a canonical name and rebuilding its Animator. The same seam would take actions from another source (a remote MCP server, a scripted routine) without the agent changing.
- Six more tool definitions in every request; they sit in the cached prefix, so after the first turn they cost cached-input rates.
- A dance or bow holds the model's next round until it ends (up to about 12 s for the longest dance); the pre-call phrase covers the start and the comment after it lands when the move is done. A barge-in cancels the turn and fades the move out.
- While the Action layer plays it overrides the Talk pose and gestures, so speaking during a dance keeps dancing (the mouth and face still move).
- The model can still answer a question about an action in words ("Can you jump?" failed once in the eval before the namespace said "or asks whether you can"; it passed once after, a single sample).
- `ICharacterController` grows by three members; both fakes implement them.

## Alternatives
- **More `play_gesture` enum values.** One tool for everything blurs "gesture while talking" with "do this for me", cannot carry parameters per action (duration, direction) and cannot be filtered per character without changing its schema. Rejected.
- **Deferred namespace loading** (`defer_loading` + `tool_search`). About 0.5 s per load, and with a tight `max_tool_calls` the model loaded the namespace and then claimed the action without calling it. Our action list is short. Rejected for now (ADR-0048).
- **Full-body clips on the Gesture layer with a per-state mask.** Unity masks are per layer; a second layer is the only way. Accepted as the Action layer.
- **Looping the dance by re-triggering one-shot plays.** Each restart fades through Empty, a visible hitch every 4 s. Rejected for `GestureHold`.
- **move_relative in user-relative space from the full head rotation.** Looking down would put "in front of me" at the user's feet. Yaw only.
