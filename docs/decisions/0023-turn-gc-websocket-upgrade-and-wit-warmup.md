# 0023. Turn GC: own WebSocket upgrade, packed ASR frames, slim Responses envelopes; Wit warm-up

- Status: accepted
- Date: 2026-11-01

## Context
The release audit left two device-measured gaps (`release-checklist.md` 4.1, 5.1):
- **GC during turns** was 11–42 KB per frame in a development player (DoD §4: ≤ 4 KB). Nobody knew where it came from: the turn path was written allocation-aware (pooled download buffers, reader-based JSON).
- **A cold first turn**: one run took 3.1 s from release to first audio on the first greeting against a p50 of ~1.5 s later.

The release player has no Profiler and nobody wears the headset, so a GUI Profiler session was not an option.

## Decision
- **Allocation profiles from the self-test.** `selftest.sh --profile` arms `TurnProfileCapture`: a development player writes each turn scenario to its own Profiler binary log with managed allocation call stacks (`Profiler.enableAllocationCallstacks`). `Jolly.Editor.Tools.GcAllocReport` loads a log in batchmode (`ProfilerDriver.LoadProfile`, `RawFrameDataView`) and sums `GC.Alloc` samples by thread, sample path and resolved call stack. Its per-frame totals match the `evt=selftest.frames` numbers to within 1 %.
- **The cause was Mono's TLS stream.** `MobileAuthenticatedStream.StartOperation` calls `BufferOffsetSize2.Reset()`, which allocates a new 16 KB buffer for **every** read and write. Mono's `ClientWebSocket` reads the HTTP 101 response one byte per read, so each ASR connect (and the launch warm-up connect) allocated several MB on a worker thread: 29.6 MB, 72 % of all bytes, in the first profile (the last 2,000 frames of the turns, about four connects).
- **`ClientWebSocketConnection` opens the connection itself**: DNS, a `Socket` per address as Mono did, `SslStream` (the same UnityTls path), and `WebSocketUpgrade`, which writes the RFC 6455 request and reads the response head in one or two reads, then `WebSocket.CreateFromStream` (in the netstandard 2.1 API and in the IL2CPP class library; verified on Quest). Bytes read past the head would be replayed by `PrefixedStream`; the Muse server sends none before the client's first message.
- **ASR audio goes out in frames of ≥ 240 ms** (`MuseAsrOptions.MinFrameSeconds`): every send still costs 16 KB. What is pending is sent the moment the source completes, so the final transcript does not wait; only partials can arrive up to 160 ms later.
- **Smaller main-thread allocators**, all seen in the profiles: `SseLineParser` grows reused `char[]` buffers instead of `StringBuilder` (whose `Clear` reallocated after every multi-chunk event) and reuses event names; `HttpTransport` reuses its SSE sinks; `ResponsesStreamReducer` reads only the type, id, usage, incomplete_details and error of `response.created/completed/incomplete/failed` instead of building the tree of the echoed request; `PcmChunk` and `ClipPcm` use dedicated `ArrayPool.Create()` pools (the shared pool's `Rent` kept allocating on these paths); the self-test converts its clips at launch instead of inside each turn.
- **Wit TTS warm-up at launch.** `StartupWarmup` also sends `GET /voices` to api.wit.ai (lists voices, synthesizes nothing, Wit is free). UnityWebRequest keeps the connection for the first `/synthesize`. The Meta Responses connection needs no new step: the connectivity monitor's launch probe (`GET /v1/models`, not billed) already opens it, and the first turn's request → first event time was the same as later turns' before this change (466–1,345 ms vs 513–1,096 ms). *Amended 2026-11-01:* `GET /voices` warmed the connection but not the synthesis path (release runs still saw 472–543 ms for the first sentence; from the dev Mac the first `/synthesize` after `/voices` on the same kept-alive connection took p50 143 ms against 88 ms after a synthesized warm-up). The warm-up now synthesizes "Hi." in the persona's voice through the normal streaming request and discards it.

## Consequences
- Turn GC (3 development-player runs each, 5 turn scenarios): p50 **14.5 → 3.9 KB/frame**, max **38.0 → 6.4 KB/frame**. `barge_in` (two ASR sessions in ~7 s) stays at 5.9–6.4 KB/frame and `voice_come_here` reaches 4.6 in one run: what is left is mostly Mono's 16 KB per TLS read (one per received ASR message), third-party code (XRI `CurveVisualController` copies its gradient every frame a ray is shown, ~0.3 KB/frame, likely also the bulk of the ~380 B/frame idle in development players; TMP and Input System array growth) and the strings a turn genuinely needs.
- First sentence queued → first audio on the first turn: 333–612 ms → 194–320 ms. The 3.1 s outlier was mostly model reasoning (first event → first text 1.4 s with 450 output tokens), which no warm-up can remove.
- The WebSocket handshake is now our code: proxies are not supported (Mono's ClientWebSocket on this path did not use one either) and only `wss://` is accepted.
- Every launch makes one extra free HTTPS request to api.wit.ai.

## Alternatives
- **A buffered stream between `ClientWebSocket` and TLS**: not possible, `ClientWebSocket` creates its stream internally.
- **Another TLS or WebSocket stack** (a native Android WebSocket over JNI, BouncyCastle): would remove the per-operation 16 KB entirely, but adds a large dependency or a Java bridge for ~2 KB/frame. Not done; it is the remaining lever for `barge_in`.
- **Larger ASR frames (e.g. 400 ms)**: fewer sends, but partial captions would lag visibly; 240 ms halves the sends at no cost to the final.
- **Re-warming connections at each PTT press**: not needed by the data (the Responses connection was warm on every first turn), so not added.
