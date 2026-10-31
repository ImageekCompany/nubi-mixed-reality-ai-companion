# Meta Model API: wire contracts verified live

**Status:** verified with live calls on 2026-11-01 (server `date:` header `Sat, 31 Oct 2026 23:35 GMT`) using the team key in `.env`. The key was never printed.
**Scope:** Chat Completions (streaming), tool calling, image understanding, SAM 3.1, Muse Voice Transcribe (file + realtime WS), limits and errors, on-device options (Glimmer / Spark Open).
**Test harness:** throwaway scripts in the session scratchpad: `sse.py` (urllib SSE reader with timestamps), `onebit.py` (own SAM mask decoder), `rt.py`/`rterr.py` (websockets 17.4 client). Sample media are in `docs/research/samples/`.

Doc sources were fetched with `curl https://dev.meta.ai/docs/<page>` (server-rendered HTML). They are cited as `[docs:<page>]` = `https://dev.meta.ai/docs/<page>`.

---

## 0. TL;DR for Jolly

| Concern | Finding | Evidence |
|---|---|---|
| LLM endpoint | `POST https://api.meta.ai/v1/chat/completions`, OpenAI-compatible, SSE streaming works | live |
| Model | `muse-spark-1.3-contributor` exists for our team. Rate limit is **150 RPM** (standard 1.3 is 3,000 RPM) | `GET /v1/models`, `x-ratelimit-limit-requests: 150`, [docs:pricing-rate-limits] |
| Latency (TTFT) | Spark always reasons and **does not stream reasoning**, so the first content byte arrives only after reasoning ends. 1.3-contributor: minimal ≈1.6–2.3 s, low ≈2.4–2.7 s, default ≈4.1–5.0 s. 1.2-contributor minimal ≈0.7–0.9 s | live, see §1.3 |
| Tool calling | Works in streaming mode. `tool_choice` accepts **only `"auto"`**. The model often sends spoken preamble text before the tool call in the same turn | live |
| Vision | Base64 data URL accepted for JPEG/PNG/GIF/WebP/ICO. **HEIC is rejected.** A 640 px JPEG costs about 300 prompt tokens | live |
| SAM 3.1 | `POST /v1/responses`, `model: sam-3.1`. The prompt is a noun phrase **or** box/point tokens. The output is special-token text with box + **box-local mask in a custom base85 + range-coded format (not COCO RLE)**. About 0.8–1.5 s per 640 px image | live |
| STT | `wss://api.meta.ai/v1/asr/realtime` works. Partials arrive about 0.5 s behind audio, and the final arrives about 150 ms after `endStream`. Input is PCM s16le mono at 16 or 24 kHz, paced in real time | live |
| TTS | **None.** Meta Model API has no speech synthesis | [docs:overview] "It is speech-to-text only; it does not synthesize speech" |
| Offline LLM on Quest 3 | **Not feasible** with Muse Glimmer. The model is 30B dense and its smallest published quant is 16.8 GB. Exports exist only for CUDA and Metal | [docs:muse-glimmer/quantization], HF tree |
| Key on device | Docs say to keep keys out of mobile clients and to use a proxy. No ephemeral or short-lived token mechanism is documented | [docs:authentication] |

---

## 1. Chat Completions (OpenAI-compatible)

`POST https://api.meta.ai/v1/chat/completions`, `Authorization: Bearer <key>`. Keys look like `LLM_<numericId>_<secret>` [docs:authentication].

### 1.1 Discovery (live)
`GET /v1/models` returns HTTP 200 with these models: `muse-wonder-1.0, sam-3.1, muse-spark-1.3-contributor, muse-voice-transcribe-1.0, muse-spark-1.3, muse-image-1.0, muse-spark-1.2-contributor, muse-spark-1.2, muse-spark-1.1`. Every `created` value is `0`. Response headers include `x-request-id` and `x-route: model-api-rust`.

### 1.2 Non-streaming (live)
Request:
```json
{"model":"muse-spark-1.3-contributor","reasoning_effort":"minimal",
 "messages":[{"role":"developer","content":"You are Jolly, a friendly MR companion. Reply in one short sentence."},
             {"role":"user","content":"Hi! Who are you?"}]}
```
Response (HTTP 200, 1.17 s):
```json
{"id":"chatcmpl-01a19451-f6ef-77cf-8a07-a3655dc006af","choices":[{"finish_reason":"stop","index":0,
 "message":{"content":"I'm Jolly, your friendly MR companion!","refusal":null,"role":"assistant"},"logprobs":null}],
 "created":1793489761,"model":"muse-spark-1.3-contributor","object":"chat.completion",
 "usage":{"completion_tokens":53,"prompt_tokens":34,"total_tokens":87,
          "completion_tokens_details":{"reasoning_tokens":35},"prompt_tokens_details":{"cached_tokens":0}}}
```
Response headers: `x-ratelimit-limit-requests: 150`, `x-ratelimit-remaining-requests: 150`, `x-ratelimit-limit-tokens: 3000000`, `x-ratelimit-remaining-tokens: 3000000`.

### 1.3 Streaming (SSE), with exact chunks (live)
Add `"stream": true`. Sending `stream_options:{include_usage:true}` was accepted, but usage arrives on the final chunk **even without it** (both cases were observed).
```
[2254ms] data: {"id":"chatcmpl-01a19452-…","choices":[{"delta":{"content":"A mug is for","role":"assistant"},"finish_reason":null,"index":0}],"created":1793489775,"model":"muse-spark-1.3-contributor","object":"chat.completion.chunk"}
[2289ms] data: {"id":"…","choices":[{"delta":{"content":" holding hot drinks like coffee and tea."},"finish_reason":null,"index":0}],…}
[2415ms] data: {"id":"…","choices":[{"delta":{"content":" stay cozy!"},"finish_reason":null,"index":0}],…}
[2418ms] data: {"id":"…","choices":[{"delta":{},"finish_reason":"stop","index":0}],…,"object":"chat.completion.chunk",
          "usage":{"completion_tokens":107,"prompt_tokens":35,"total_tokens":142,"completion_tokens_details":{"reasoning_tokens":75},"prompt_tokens_details":{"cached_tokens":0}}}
[2435ms] data: [DONE]
```
Wire facts:
- Lines are `data: <json>` with no `event:` names. The stream terminates with `data: [DONE]`.
- `role` appears only on the first delta. The final chunk has an empty `delta`, a `finish_reason` (`stop`, `tool_calls` or `length`) and `usage`. No separate usage-only chunk was seen.
- **No reasoning deltas or keep-alives arrive during the think phase.** The first byte of the HTTP body is the first content token. Deltas are phrase-sized (3–8 words), not single tokens.

**Time to first token, measured** (prompt: 2-sentence answer, 3 runs each, from the dev Mac's network (location not measured), wall clock from request start):

| model | `minimal` | `low` | omitted (default) |
|---|---|---|---|
| muse-spark-1.3-contributor | 1609 / 1890 / 1867 ms (plus 2254 ms in an earlier run) | 2742 / 2732 / 2360 | 4101 / 5049 / 4287 |
| muse-spark-1.2-contributor | 935 / 692 / 747 | 1538 / 2084 / 1355 | 2398 / 3069 / 1676 |
| muse-spark-1.3 (standard) | 984 / 1287 / 1283 | 1555 / 1256 / 1518 | 1973 / 1943 / 2182 |

Implication: for a target of under 2 s to first audio, use `reasoning_effort:"minimal"`. Consider `muse-spark-1.2-contributor` for chit-chat turns. Total stream time after the first token is short (about 100–400 ms for 2 sentences).

### 1.4 Parameters and validation that matter (docs plus live)
- `reasoning_effort`: docs list `none|minimal|low|medium|high|xhigh` [docs:protocols/chat-completions]. Live, `none` returned **400**: `"reasoning_effort 'none' is not supported for model 'muse-spark-1.3-contributor'. Supported values: [minimal, low, medium, high, xhigh, max]"`. The error lists `max`, but the models page says `max` is not available on `-contributor` [docs:models]. UNVERIFIED which one is right.
- `max_completion_tokens` **includes reasoning tokens**. Live, `max_completion_tokens:5` gave `finish_reason:"length"`, `content:null`, `reasoning_tokens:3`. Do not cap tightly.
- `minimal` does not mean little reasoning. The image call in §3 used 539 reasoning tokens at `minimal`.
- `temperature` defaults to 1.0 and the docs recommend leaving it. `top_p:0` returns 400. `n>1` returns 400. `logprobs:true` returns 400. `frequency_penalty`/`presence_penalty` are unsupported [docs:protocols/chat-completions].
- Roles: `developer` (preferred) and `system` (accepted, same precedence), plus `user`, `assistant` and `tool` [docs:protocols/chat-completions].
- Chat Completions does **not** carry reasoning across turns. Responses API (`previous_response_id`) does. The docs recommend Responses for agent loops [docs:tool-calling].
- Prompt caching works automatically. The second tool turn showed `cached_tokens: 625` of 772 (live). `prompt_cache_key` is available [docs:protocols/chat-completions].
- Non-streaming requests can 504 (`gateway_timeout`). Streaming is exempt once the first event arrives [docs:error-handling].
- `safety_identifier` (≤64 chars, a hashed user id) is recommended [docs:protocols/chat-completions].

---

## 2. Tool / function calling (live, streaming)

Tool definitions use the Chat Completions nested format:
```json
[{"type":"function","function":{"name":"walk_to","description":"Make Jolly walk to a named location or anchor in the user's room.",
  "parameters":{"type":"object","properties":{"location":{"type":"string","description":"Location name, e.g. 'kitchen', 'user', 'table'"}},
                "required":["location"],"additionalProperties":false}}},
 {"type":"function","function":{"name":"capture_and_identify","description":"Capture a passthrough camera frame and segment the object the user is pointing at.",
  "parameters":{"type":"object","properties":{"object_hint":{"type":"string","description":"Short noun phrase for the object, e.g. 'pen'"}},
                "required":["object_hint"],"additionalProperties":false}}}]
```
User: *"Jolly, walk to the kitchen and then tell me what this pen is."* (`reasoning_effort:"minimal"`, `stream:true`). Stream:
```
[2393ms] data: {…"choices":[{"delta":{"content":"Got it — walking to the kitchen, then I'll check that pen.","role":"assistant"},"finish_reason":null,"index":0}],…}
[2454ms] data: {…"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_01a19453a7a174c5a2af5539aa0a23bc","type":"function","function":{"name":"walk_to","arguments":""}}]},"finish_reason":null,"index":0}],…}
[2618ms] data: {…"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"location\":\"kitchen\"}"}}]},"finish_reason":null,"index":0}],…}
[2619ms] data: {…"choices":[{"delta":{},"finish_reason":"tool_calls","index":0}],…,"usage":{"completion_tokens":257,"prompt_tokens":668,…,"reasoning_tokens":184}}}
[2665ms] data: [DONE]
```
Delta shape:
- The first delta for a call carries `index`, `id`, `type`, `function.name` and `arguments:""`.
- Later deltas carry only `index` and `function.arguments` fragments. Accumulate them by `index`.
- The final chunk has `finish_reason:"tool_calls"`.
- **Text content can precede tool calls in the same message.** This is useful: Jolly can say "Got it, walking…" while the action starts.

**Parallel calls (live).** When asked to do both at once, the model sent index 0 `walk_to{"location":"sofa"}` and then index 1 `capture_and_identify{"object_hint":"mug"}` in one turn, with a distinct `id` for each. Unprompted, it chained the tools sequentially across turns. `parallel_tool_calls:false` limits it to one call per turn [docs:tool-calling].

**Round-trip.** Append the full assistant message, then one `tool` message per call:
```json
{"role":"assistant","content":"Got it — walking to the kitchen, then I'll check that pen.",
 "tool_calls":[{"id":"call_01a19453a7a174c5a2af5539aa0a23bc","type":"function","function":{"name":"walk_to","arguments":"{\"location\":\"kitchen\"}"}}]},
{"role":"tool","tool_call_id":"call_01a19453a7a174c5a2af5539aa0a23bc","content":"{\"status\": \"arrived\", \"location\": \"kitchen\"}"}
```
The next streamed turn had TTFT 1018 ms. It sent text, then the next tool call `capture_and_identify{"object_hint":"pen"}`, with `cached_tokens:625`. After a fake SAM result it answered in text: *"I'm in the kitchen now. That is a ballpoint pen — confidence…"* (TTFT 1270 ms).

Validation (live):
- `tool_choice:"required"` returns 400 `"only \"auto\" is supported for tool_choice. \"none\", \"required\", and named function choices are not currently supported"`. As a result, **forcing a tool is impossible**. Handle the text-only answer path.
- A `tool` message without its preceding assistant `tool_calls` message returns 400 `"role='tool' message with tool_call_id='…' has no matching assistant message with tool_calls"`.

Docs-only rules [docs:tool-calling]:
- Tool names must match `^[a-zA-Z0-9_.-]+$` with at most one dot.
- `call_id` must be 1–64 characters.
- `strict` defaults to false. Arguments are not guaranteed to validate against the schema, so validate them client-side.
- `max_tool_calls` limits only built-in tools.
- `type:"custom"` tools are Responses-only; on Chat Completions they return 400.
- Built-in `web_search` costs $2.50 per 1k searches [docs:pricing-rate-limits].

Tool definitions add prompt tokens: about 630 for these two small tools (668 vs 35 for no tools).

---

## 3. Image understanding (live)

Chat Completions content part (object form):
```json
{"role":"user","content":[{"type":"text","text":"What is this and how do I use it?"},
  {"type":"image_url","image_url":{"url":"data:image/jpeg;base64,<…>"}}]}
```
The Responses API uses `{"type":"input_image","image_url":"<string>"}` (a plain string) [docs:image-understanding].

| Test | Result |
|---|---|
| `pen_640.jpg` (640×428, 20 KB) | 200. `prompt_tokens: 396` (about 300 of them image). Answer: *"This is a blue ballpoint pen with its separate cap. To use it, remove the blue cap…"* plus bbox `{"x_min":70,"y_min":50,"x_max":865,"y_max":910}` on a 0–1000 grid. Non-streaming 6.8 s (539 reasoning tokens); streaming TTFT 3.8 s |
| `mug_1280.jpg` (1280×720, 100 KB) | 200. `prompt_tokens:1262`, TTFT 2.9 s, *"white enamel camp mug…"*, bbox `[541,340,828,746]` |
| PNG (converted with `sips`) | 200 |
| HEIC (`data:image/heic`) | 400 `"unsupported media type at messages[1].content[1]: \`image/heic\`. Supported: JPEG, PNG, GIF, WebP, ICO."` |
| Truncated JPEG (300 bytes) | 400 `"invalid image data at messages[1].content[1]: the \`image/jpeg\` payload could not be decoded (it may be corrupt or truncated)."` |
| `{"type":"file","file":{"file_id":"file-…"}}` after a `/v1/files` upload | 200 ("Mug"). This shape is a guess and is not shown for images on the docs page I read, but it worked live |

Limits [docs:image-understanding]:
- Inline image: 50 MB max. Files API: 1 GiB.
- Up to 50 images per request. More returns 400.
- MIME types: jpeg, jpg, png, gif, webp, x-icon.
- Spark returns grounding coordinates on a normalized 0–1000 grid, with (0,0) at top-left.
- Token cost scales with resolution: a ~1280 px image is about 1.3–1.5k tokens. Use `POST /v1/responses/input_tokens` to count exactly.

**For Jolly:** send a 640 px JPEG of the passthrough frame (≈300 tokens). Encode to JPEG on device. Quest camera frames arrive as YUV/RGBA, and HEIC is never accepted.

---

## 4. SAM 3.1 (media segmentation, live)

### 4.1 Request
`POST /v1/responses` with `model:"sam-3.1"`. Send one user message with **one** `input_text` and **one** media part [docs:sam/overview, sam/segmenting].
```json
{"model":"sam-3.1","stream":true,
 "metadata":{"include_confidence":"true","mask_encoding":"one_bit"},
 "input":[{"type":"message","role":"user","content":[
   {"type":"input_text","text":"pen"},
   {"type":"input_image","image_url":"data:image/jpeg;base64,<…>"}]}]}
```
- **Prompt kinds**, exactly one per request:
  - (a) A **concept** noun phrase, such as `"pen"` or `"red mug"`. One concept per request. Lists, commands and questions often return nothing. Formatting, plurals and case are ignored.
  - (b) **Box/point tokens** in the output grammar, for example `<0f>1<|box;x1=..;y1=..;x2=..;y2=..;w=W;h=H|>,2<|point;x=..;y=..;w=W;h=H|>-<|point;…|>` (`-` marks a negative point). Up to 16 objects. `w`/`h` must equal the media's displayed size, after EXIF rotation, or the request returns 400 [docs:sam/segmenting].
- **Media:** `input_image.image_url` (public URL or base64 data URL) or `input_image.file_id`. Video uses `input_video` (`video_url` or `file_id`) in MP4/MOV with H.264, H.265 or MPEG-4, ≤15,000 frames [docs:sam/segmenting].
- **`metadata`** values are **strings**: `include_confidence:"true"|"false"`, `mask_encoding:"lossless"|"one_bit"`, `propagation_direction:"both"|"forward"|"backward"` (video only) [docs:sam/segmenting].
- **File upload flow (live):** `curl -F purpose=user_data -F file=@mug_640.jpg https://api.meta.ai/v1/files` returned 200 in 1.55 s:
  ```json
  {"id":"file-2224880075128406","bytes":31326,"created_at":1793490101,"expires_at":null,"filename":"mug_640.jpg","object":"file","purpose":"user_data","status":"uploaded"}
  ```
  `{"type":"input_image","file_id":"file-2224880075128406"}` then worked with SAM (200, 2.39 s non-streaming). `DELETE /v1/files/{id}` returned `{"id":"file-…","object":"file","deleted":true}`. Files never expire unless `expires_after:{anchor:"created_at",seconds:3600..2592000}` is set. Teams have a 100 GiB storage cap [docs:file-handling]. **For one-shot stills, inline base64 is simpler and faster.**

### 4.2 Response (streamed Responses API events, live, pen 640×428)
```
[895ms] event: response.created          data: {"response":{…"id":"resp_6ae67c65de9d3b924ca04468","model":"sam-3.1","status":"in_progress",…},"sequence_number":0,"type":"response.created"}
[895ms] event: response.in_progress
[895ms] event: response.output_item.added data: {"item":{"content":[{"annotations":[],"logprobs":[],"text":"","type":"output_text"}],"id":"msg_3182…","role":"assistant","status":"in_progress","type":"message"},…}
[895ms] event: response.content_part.added
[895ms] event: response.output_text.delta data: {"content_index":0,"delta":"<0f>0<|box;x1=50;y1=26;x2=372;y2=397;w=640;h=428;c=0.8709677457809448|><|mask;x=0;y=0;c=0.8709677457809448;data=372,323,!!!!#_Fw=L!un6Z6pNM!yxy8=…!!|>\n",…}
[895ms] event: response.output_text.done
[896ms] event: response.content_part.done
[896ms] event: response.output_item.done
[1046ms] event: response.completed       data: {"response":{…"status":"completed","usage":{"images_processed":1,"input_tokens":0,"output_tokens":0,"total_tokens":0,…}}}
[1046ms] data: [DONE]
```
The non-streaming response has the same text at `output[0].content[0].text`, with `usage.images_processed:1`.

### 4.3 Output grammar [docs:sam/reading-segmentation], confirmed live
```
<Nf>id<|box;x1=;y1=;x2=;y2=;w=frameW;h=frameH[;c=conf]|><|mask;x=0;y=0[;c=conf];data=H,W,<enc>payload|>,id<|box…|><|mask…|>
```
- **Boxes** are in source pixels and `x2`/`y2` are **inclusive**.
- **The mask is box-local**: `H = y2-y1+1` and `W = x2-x1+1`. This was verified live, for example on the pen with box 50..372 × 26..397 and `data=372,323`. Draw it at (x1,y1) at 1:1 scale.
- The `enc` marker is `~` (lossless, an 8-bit coverage raster thresholded at ≥129) or `!` (one_bit).
- The payload is base85 over a custom 85-character alphabet: ASCII `0x21..0x7E` minus `" \ , ; < > |`. It starts with a 5-character length prefix. One_bit masks are then decoded with an **adaptive binary range coder using a 12-pixel causal context**. Lossless masks use a range coder with Paeth-like prediction.
  - Source: `meta_sam_parser/_mask_codec.py` in `meta-sam-parser` 0.1.0 (PyPI), the same algorithm as `@meta-sam/parser` 0.1.0 (npm).
- **I wrote an independent one_bit decoder (~60 lines of Python)** from reading that file. It decoded the live pen and mug masks correctly: `samples/sam_pen_mask_onebit.png` and `samples/sam_mug_mask_onebit.png` (coverage 0.109 and 0.724). The harness blocked running the downloaded parser itself, so the reference decoder was not run side by side.
- **Payload size:** the mug with `one_bit` was 152 characters. The mug at 1280 px with lossless was 15,365 characters. The pen point prompt was 4,405 characters lossless. **Always request `one_bit`.**
- A **zero-match** prompt (`"banana"` on the mug) returned 200 with an empty `output_text` and no delta events (live). This is not an error.
- **Point/box prompt (live):** `<0f>1<|point;x=470;y=300;w=640;h=428|>,2<|box;x1=50;y1=26;x2=372;y2=397;w=640;h=428|>` returned object 1 = pen cap `box 368,214–561,387` and object 2 = pen body, under **our ids**.
- **Doc discrepancy:** the docs say box/point prompts "always return one_bit masks". Live, without `mask_encoding`, they returned `~` (lossless). With `mask_encoding:"one_bit"` they returned `!`. Read the marker on every mask and send `one_bit` explicitly.

### 4.4 Latency and cost
- Pen 640 px streaming: first byte 641–1270 ms and total 802–1473 ms over 5 runs.
- Mug 1280 px: total 1.35 s.
- Non-streaming 640 px: 1.36 s.
- Images return a single delta, so streaming gives no partial benefit for stills.
- Price: $2.50 per 1,000 images. Rate limit: 3,000 RPM and no TPM limit [docs:pricing-rate-limits].

### 4.5 Licence of the client libraries
`@meta-sam/parser` (`"license": "SEE LICENSE IN LICENSE"`, repo `github.com/meta-models/meta-sam`) and PyPI `meta-sam-parser` 0.1.0 (license field = full **SAM License**, last updated 2025-11-19) are both under the **SAM License, which is not OSI-approved**:
- It permits use, modification and redistribution with a copy of the agreement.
- §1.b.iv forbids using the materials to "reverse engineer, decompile or discover the underlying components".
- It includes Trade Controls and ITAR restrictions.

A C# port of the codec is a derivative of the SAM Materials. Ship the SAM License text with it and list it in THIRD_PARTY. `_mask_conversion.py` also contains BSD-licensed COCO code.

---

## 5. Muse Voice Transcribe (`muse-voice-transcribe-1.0`)

### 5.1 File: `POST /v1/asr/transcribe` (live)
The body is multipart with two parts: `request` (JSON, `type=application/json`) and `audio` (WAV). The `sessionId` query parameter is optional.
```bash
curl "https://api.meta.ai/v1/asr/transcribe?sessionId=jolly-research-11566" -H "Authorization: Bearer $KEY" \
  -F 'request={"model":"muse-voice-transcribe-1.0","audioEncoding":"WAV"};type=application/json' -F 'audio=@hi24.wav'
```
```json
{"sessionId":"jolly-research-11566","transcript":"Hey Jolly, walk to the kitchen and tell me what this pen is.","audioDurationMs":3280,"turns":[]}
```
(HTTP 200, 3.31 s round trip for 3.2 s of audio.) The WAV came from `say -o hi.aiff` and `afconvert -f WAVE -d LEI16@24000 -c 1`. It contains an extra `FLLR` chunk, which the server tolerated.

| Variant | Result |
|---|---|
| 16 kHz s16 mono | 200, same text, 2.1 s |
| 44.1 kHz | 400 `"Unsupported WAV format: channels=1, sampleRate=44100, bitsPerSample=16; expected mono s16le at 16000 or 24000 Hz"` |
| `audioEncoding` missing | 400 `"You must specify the 'audioEncoding' field"` (`param:"audioEncoding"`) |
| `mode:"ENDPOINTING"`, 2 phrases with 1.5 s gap | `{"transcript":"What is this object? and how do I use it","audioDurationMs":4000,"turns":[{"turnId":0,"startMs":0,"endMs":1260,"transcript":"What is this object?"},{"turnId":1,"startMs":2460,"endMs":3500,"transcript":"and how do I use it"}]}`. **turnId starts at 0**; the docs example shows 1 |
| `Accept: text/plain` | `What is this object?\nAnd how do I use it` |
| `Accept: text/event-stream`, `partialMode:"DELTA"` | `event: transcript` / `data: {"type":"transcript","transcript":" Jolly,","final":false,"audioProcessedMs":1280}` … final `{"transcript":"Hey Jolly, walk to the kitchen and tell me what this pen is..","final":true,…}` |
| `keywords:["Jolly"]` + `languageBias:["English"]` | `"Hey Jolly, Jolly, walk…"`: **keyword biasing duplicated the word**. Without bias the text was correct. Use keywords cautiously |
| Polish (`say -v Zosia`), `languageBias:["Polish"]` | `"Cześć Jolę, co to jest?"` (Polish works; the name was misheard) |
| No `sessionId` | The server generates `voyager/transcribe/<hex>` |

Limits [docs:speech-to-text]: body ≤32 MB (else 413), audio ≤10 min (else 400), 429 on rate limit, and 500 or a terminal SSE `error` on budget overrun (retry once).

### 5.2 Realtime: `wss://api.meta.ai/v1/asr/realtime?sessionId=<opt>` (live, Python `websockets` 17.4)
Protocol [docs:speech-to-text, api-reference/voice/realtime, api-reference/voice/schemas]:
1. Connect. The **HTTP `Authorization` header is ignored.**
2. Within **10 s**, send the handshake as the first text frame:
   ```json
   {"authorization":{"accessToken":"Bearer <key>"},"audioEncoding":"PCM_24KHZ","model":"muse-voice-transcribe-1.0",
    "mode":"PUSH_TO_TALK","partialMode":"CUMULATIVE","emitAudioProgress":false,"languageBias":["English"],"keywords":[]}
   ```
   `audioEncoding` is required: `PCM_24KHZ` (native, 48,000 B/s) or `PCM_16KHZ` (32,000 B/s, resampled). `mode` is `PUSH_TO_TALK` (default), `ENDPOINTING` or `DIARIZATION`. `partialMode` is `CUMULATIVE` (default; replace text) or `DELTA` (append; docs say it is meant for file SSE). `emitAudioProgress` defaults to true. The config is **immutable** for the session.
3. The ack has no `type` field: `{"sessionId":"jolly-rt-3fd4e781"}`.
4. Send **binary frames of raw s16le mono PCM**. Frame boundaries carry no meaning; 80 ms frames (3,840 bytes at 24 kHz) are used in the docs. **Pace in real time.** Stay no more than 5 s ahead, and send silence rather than nothing while the source is live.
5. Send the text frame `{"type":"endStream"}` and keep reading until **close 1000**.
6. Server events, each with `type`: `transcript{transcript,final,audioProcessedMs}`, `speechStart{turnId}`, `speechEnd{turnId}`, `speechComplete{turnId,transcript}`, `speaker{label}`, `audioProgress{audioProcessedMs}`, `error{message,sessionId,errorType?,errorCode?,errorParam?}`. Ignore unknown types.
7. Sessions last at most 60 min, with no resume. Limits are 256 concurrent streams and 16k streams per hour per team.

**PUSH_TO_TALK, 24 kHz (live, timestamps from connect):**
```
[577ms] connected
[1052ms] ACK {"sessionId":"jolly-rt-3fd4e781"}
[1993ms] {"type":"transcript","transcript":"hey","final":false,"audioProcessedMs":880}
[2478ms] {"type":"transcript","transcript":"hey Jolly","final":false,"audioProcessedMs":1360}
…
[4321ms] {"type":"transcript","transcript":"hey Jolly walk to the kitchen and tell me what this","final":false,"audioProcessedMs":3200}
[4333ms] -- last speech audio sent --
[4731ms] {"type":"transcript","transcript":"hey Jolly walk to the kitchen and tell me what this pen is.","final":false,"audioProcessedMs":3600}
[4813ms] -- sending endStream --
[4957ms] {"type":"transcript","transcript":"hey Jolly walk to the kitchen and tell me what this pen is.","final":true,"audioProcessedMs":3760}
[4977ms] CLOSED code=1000 reason="No more transcript segments"
```
- Handshake RTT: about 0.5–0.8 s.
- Partials lag audio by about 0.5–1.1 s.
- **The final arrives about 145 ms after `endStream`.**
- Partials are lowercase and unpunctuated. The final is cased.

**ENDPOINTING, 24 kHz, two phrases (live):**
```
[1368ms] {"type":"speechStart","audioProcessedMs":0,"turnId":0}
[1605ms] {"type":"transcript","transcript":"What","final":false,"audioProcessedMs":560}
[2581ms] {"type":"transcript","transcript":"What is this object?","final":false,"audioProcessedMs":1520}
[2581ms] {"type":"speechEnd","audioProcessedMs":1020,"turnId":0}
[2581ms] {"type":"speechComplete","audioProcessedMs":1020,"turnId":0,"transcript":"What is this object?"}
[4006ms] {"type":"speechStart","audioProcessedMs":2460,"turnId":1}
…
[5138ms] {"type":"speechComplete","audioProcessedMs":3580,"turnId":1,"transcript":"And how do I use it?"}
[7132ms] {"type":"transcript","transcript":"","final":true,"audioProcessedMs":6000}     ← after endStream
[7133ms] CLOSED code=1000
```
- Endpointing decided about 0.5 s of audio after speech stopped. `speechEnd` and `speechComplete` arrived together.
- **This gives server-side VAD for hands-free use**, so no wake-word engine is needed for turn-taking.
- One 16 kHz PTT run ended with the ideographic full stop `"。"` instead of `.`. Normalize punctuation.

**Errors and close codes (live):**

| Case | First frame | Close |
|---|---|---|
| Bad key | `{"type":"error","message":"Unauthorized: the credential presented in the handshake was not accepted","sessionId":"voyager/duplex/…","errorType":"authentication_error","errorCode":"invalid_api_key"}` | `1008 "Unauthorized"` |
| No `audioEncoding` | `{"type":"error","message":"You must specify the 'audioEncoding' attribute",…,"errorParam":"audioEncoding"}` | `1008 "Bad Request"` |
| `mode:"FOO"` | `{"type":"error","message":"Malformed handshake request",…}` | `1008 "Bad Request"` |
| 16 s of audio sent instantly | ack, then `{"type":"error","message":"Audio processing backlog too large",…}` | `1008 "Audio backlog too large"` (1.25 s) |

Documented codes: `1011` is internal (retry; `Max session duration reached` means reconnect), and `1013` is rate limited [docs:api-reference/voice/realtime].

Pricing is $0.18/h with no contributor mode [docs:pricing-rate-limits]. Supported languages are 25 including English and Polish. There are no word-level timestamps [docs:speech-to-text].

---

## 6. Rate limits, errors, retries, capabilities

**Rate limits** are per team, not per key [docs:pricing-rate-limits]:

| model | RPM | TPM | other |
|---|---|---|---|
| muse-spark-1.3 / 1.2 / 1.1, muse-wonder-1.0 | 3,000 | 4,000,000 | |
| **muse-spark-1.3-contributor / 1.2-contributor** | **150** | 3,000,000 | |
| sam-3.1 | 3,000 | none | |
| muse-image-1.0 | 150 | none | |
| muse-voice-transcribe-1.0 | none | none | 256 concurrent streams, 16,000 streams/h |

`x-ratelimit-{limit,remaining}-{requests,tokens}` headers are present on token-model responses (seen live). ASR responses do not include them.

**Error envelope (live):** `{"error":{"code":<string|null>,"message":"…","param":<string|null>,"type":"invalid_request_error|authentication_error|rate_limit_error|server_error"}}`. Examples:
- 401 `{"code":"invalid_api_key","message":"Unauthorized","type":"authentication_error"}`
- 404 `{"code":"model_not_found","message":"The requested model was not found.","type":"invalid_request_error"}`

`code` is often `null` on 400s. An unknown path returns 404 with an **empty body** [docs:error-handling].

**Retry guidance** [docs:error-handling, docs:pricing-rate-limits]:
- Retry 429/500/503 with exponential backoff and jitter: start at 0.5–1 s, double each time, and honour `Retry-After`. 503 codes are `server_shutting_down`, `service_overloaded` and `backend_unavailable`.
- Never retry 400/401/402/403/404/413 unchanged.
- A 504 `gateway_timeout` (non-streaming) means stream or shrink the request.
- Mid-stream failures on the Responses API arrive as `response.failed` or `type:"error"` events. Handle them alongside `response.completed` and `response.incomplete`.

**Capability matrix** [docs:models, docs:overview, live]:

| model | input | streaming | tools | endpoints |
|---|---|---|---|---|
| muse-spark-1.3(-contributor) | text, image, video, audio*, PDF | yes (live) | yes (live) | Chat Completions, Responses, Messages |
| muse-spark-1.2(-contributor) | same; audio fully supported | yes (live) | yes (docs) | same |
| muse-wonder-1.0 | same | yes | yes | same; standard mode only, $4/$20 per M |
| sam-3.1 | text or box/point prompt + image/video | yes (live) | n/a | Responses |
| muse-voice-transcribe-1.0 | audio | WS + SSE (live) | n/a | /asr/* |
| muse-image-1.0 | text + image | – | – | /images/*, Responses |

\*Audio on 1.3 is "not fully supported … quality may be degraded" [docs:models].

**Contributor vs standard** [docs:models, docs:pricing-rate-limits]:
- Same model weights.
- Contributor content **may be used to train Meta models**. Do not send therapy-style (Psychologist persona) content through `-contributor` without user consent.
- Price per 1M tokens: input $0.10 vs $1.25, output $0.20 vs $4.25, cached $0.002 vs $0.15.
- **RPM is 150 vs 3,000.**
- `max` reasoning is unavailable on 1.3-contributor (docs; the live error message disagrees).
- Live, 1.3-contributor TTFT was about 1.5–2× slower than 1.3 standard at the same effort (§1.3).

---

## 7. Muse Glimmer / Muse Spark Open: offline on Quest 3?

| | Muse Glimmer | Muse Spark Open |
|---|---|---|
| Size | **30B dense**, text + image in, 128K ctx [docs:muse-glimmer] | 816B MoE, 42B active; BF16 ≈1.65 TB, NVFP4 ≈537 GB [docs:muse-spark-open] |
| Smallest artifact | `Muse-Glimmer-30B-KQuant-17GB-Q4_K_M.gguf` **16.8 GB** + mmproj 1.4 GB. 19.0 GiB total VRAM at 131k ctx [docs:muse-glimmer/quantization] | NVFP4 on a 4-GPU Blackwell node |
| Runtimes | vLLM, SGLang, llama.cpp ≥ b10353, Ollama, LM Studio, ExecuTorch. **ExecuTorch exports only for CUDA (`sm80+ptx`) and Apple silicon (`metal`)**; "ExecuTorch doesn't support CPU export" [docs:muse-glimmer/deploy]. HF `meta-models/Muse-Glimmer-30B-ExecuTorch-PTE` tree lists 16 exports, all `-metal` or `-sm80+ptx` (live HF API) | GPU servers / hosted providers |
| License | **Apache-2.0** (HF API `license:apache-2.0` on all 3 repos, not gated) | Muse Model License + AUP (not OSI) [docs:muse-spark-open] |

**Verdict:** neither runs on Quest 3 (8 GB RAM shared with the OS and the app). Glimmer Q4 needs about 17–19 GB, more than twice the device's total RAM. No Android, Vulkan-mobile or QNN build is published. llama.cpp is portable, but a 17 GB GGUF cannot load in 8 GB, and 2-bit quants are not published (UNVERIFIED whether a self-made ~2-bit quant would fit or be usable; it would still be about 8 GB or more).

The offline path for the "secure/offline Psychologist" goal is a **LAN or desktop host** running Glimmer behind the same OpenAI-compatible Chat Completions contract. Keep `IChatModel` endpoint-agnostic so the base URL switches to `http://<pc>:8080/v1`. For a fully on-device fallback, use canned lines per requirement 23, or a much smaller third-party model. That is out of scope here and would need its own licence check.

---

## 8. Recommendations for the Unity client
1. **Transport:** `UnityWebRequest` with a custom `DownloadHandlerScript` to parse SSE `data:` lines for Chat Completions and Responses (SAM). Use `System.Net.WebSockets.ClientWebSocket` for ASR realtime (UNVERIFIED on Quest IL2CPP; spike it). Neither the OpenAI nor the Anthropic SDK is needed.
2. **Models:**
   - Chat: `muse-spark-1.3-contributor`, `reasoning_effort:"minimal"`, `stream:true`. Keep `muse-spark-1.2-contributor` as a configurable fast path.
   - Sensitive personas: a standard-mode model ID.
   - Respect **150 RPM**. One user is fine, but a demo plus CI tests share the team budget.
3. **Voice loop:**
   - Realtime ASR in `ENDPOINTING` mode for hands-free use, or `PUSH_TO_TALK` (send `endStream` on release, and the final arrives in about 150 ms).
   - Send `PCM_16KHZ` or `PCM_24KHZ` from the Quest mic, resampled and converted to s16le. Pace 80 ms frames.
   - Budget: ASR final ≈0.2–0.5 s after speech end, plus LLM TTFT ≈1.6–2.3 s, plus TTS. **2 s to first audio is not achievable with 1.3-contributor reasoning.** Use a filler or backchannel animation and line, use 1.2-contributor, or speak the tool-call preamble text as soon as it arrives.
4. **Capture and identify:** downscale the passthrough frame to ≤640 px JPEG.
   - Run Spark vision for the "what is it and how to use it" answer.
   - Run **in parallel** SAM `one_bit` with the noun phrase Spark proposes (or a point prompt at the user's pointing ray hit) for the highlight mask.
   - Port the one_bit decoder (§4.3) to C#, about 80 lines. Include the SAM License.
5. **Keys:** an embedded key in an APK is extractable, and the docs say to proxy [docs:authentication]. For the hackathon, keep the key in a local untracked config, and document the proxy as production-required. No ephemeral ASR token is documented.

## 9. Sample media
- `samples/pen_1280.jpg`, `samples/pen_640.jpg`: "04-BICcristal2008-03-26.jpg" by Trounce, **CC BY 3.0** (https://commons.wikimedia.org/wiki/File:04-BICcristal2008-03-26.jpg). Attribution is required.
- `samples/mug_1280.jpg`, `samples/mug_640.jpg`: "Coffee Mug (Unsplash).jpg" by Ryan Holloway, **CC0** (https://commons.wikimedia.org/wiki/File:Coffee_Mug_(Unsplash).jpg).
- `samples/sam_pen_mask_onebit.png`, `samples/sam_mug_mask_onebit.png`: SAM 3.1 output masks decoded by our decoder (box-local rasters).
