# 0048. Meta's built-in web search answers questions about today; tool namespaces go on the wire eagerly

- Status: accepted
- Date: 2026-11-02

## Context
- Jolly knew nothing current. "What's the weather like today?", "Who won last night?" or "Is the pharmacy open?" got an apology or a guess, and the model did not know the date.
- Meta's Responses API has a built-in `web_search` tool ([docs](https://dev.meta.ai/docs/search-grounding.md)). The server decides whether to search, runs the search and answers in the same response. It is not available on Chat Completions, which Jolly does not use (ADR-0004).
- Live probes, `muse-spark-1.3`, `stream:true`, 2026-11-02 (docs/research/meta-model-api.md §10):
  - Stream: a short spoken-style preamble message ("I'll check today's London weather for you." at 2.25 s), then a `web_search_call` item with `response.web_search_call.in_progress` → `.searching` → `.completed` (0.5–1 s), a second reasoning item, and the answer 0.3–6 s later. Request to completion: 3.4–3.8 s for the weather question.
  - A search adds about 3,100 input tokens at `search_context_size:"low"` (4,326–4,374 in, 1,122–2,274 cached; the same request without a search ≈ 1,230) plus $2.50 per 1,000 searches: about $0.007–0.008 per searched turn against about $0.001 for a plain one.
  - Without an instruction the model sometimes asked "what city?" instead of searching. "For anything current … search the web; the user is near the location you were given" fixed it.
  - Answers may carry markdown and `url_citation` annotations.
  - Replaying a `web_search_call` item in a later round's input is accepted (its `id` is optional).
  - `web_search` reserves the function names `browser.search`, `browser.open` and `browser.find`; a function with one of them fails the request with 400.
  - `max_tool_calls` caps built-in tool calls only (web search, tool search), never our functions ([tool-calling docs](https://dev.meta.ai/docs/tool-calling.md)).
- Namespaces (ADR-0047's character actions build on them): a `{"type":"namespace"}` tool with eager inner functions added about 0.1 s and was chosen reliably ("Go stand behind me" → `actions.move_relative`, "show me how to dance" → `actions.dance {}`). The function call arrives named `actions.dance` (no separate `namespace` field), and a later round that replays `actions.dance` with its output is accepted. Deferred loading (`defer_loading` + `{"type":"tool_search"}`) added about 0.5 s; with `max_tool_calls:1` the model loaded the namespace, did not call the function and said it had.

## Decision
- **Request** (`ResponsesRequestWriter`): top-level functions first, then one `{"type":"namespace","name","description","tools"}` per `ModelRequest.Namespaces` entry holding its `ns.fn` tools as `fn`, eagerly (no `defer_loading`, no `tool_search`), then `{"type":"web_search","search_context_size":"low","user_location":{"type":"approximate",…}}` with only the location fields that are set, and `max_tool_calls: 2`. Each group keeps the registry's order so the cached prefix is as stable as the tool list. A dotted tool whose namespace is not in the request goes out flat under its full name (the API allows one dot). `PromptCacheKey` also hashes whether web search is offered. The registry refuses a namespace named `browser`.
- **Stream** (`ResponsesStreamReducer`): `response.web_search_call.in_progress` → `ModelEventKind.WebSearchStarted`, its `output_item.done` → `WebSearchCompleted` (both carry the item id). The `web_search_call`, `tool_search_call` and `tool_search_output` items are Opaque items replayed within the turn and never function calls. Searches are counted into `UsageRecord.WebSearches`. A function call with a separate `namespace` field (the tool-search docs' shape) is named `ns.fn`, so `ToolRegistry.TryValidate`/`TryGet` see the registered name either way.
- **When it is offered** (`WebSearchPolicy`): `JollyConfig.webSearch` is on, the persona's `AllowedTools` is null or lists `web_search` (the name is gated like a tool name; the psychologist's list leaves it out), and connectivity is `Online`. In `Degraded` a search would add a second model pass to an already failing link, so it is left out.
- **Where the user is** (`UserLocation`, never finer than a city): the city and country the user said, through the new `set_home_location {city, country?}` tool, persisted with the other preferences (`UserPreferencesSnapshot.HomeCity/HomeCountry`, PlayerPrefs). Otherwise the device locale's country. With a city, only the country said with it is sent: a headset set up in the US and used in Warsaw would otherwise send "Warsaw, US". The IANA time zone always comes from the device; a non-IANA id is dropped. On Android both come from Java (`java.util.Locale`, `java.util.TimeZone`), and the startup line `evt=location.device` logs Java's id beside what `TimeZoneInfo.Local.Id` returned under IL2CPP. The city is sanitised like other context text and never logged.
- **Prompt**: the instructions add, only when search is offered, "For anything current or local (weather, news, scores, prices, opening hours, recent events) search the web for the user's home or the location you were given instead of asking where they are. Answer from the search in one or two short sentences; never read out URLs, sources, citations or markdown." The local date, weekday and time, and the home city, go in the per-turn context block ("Local time: Monday, 2 November 2026, 10:15. Home: Kraków, PL."), after the history, so the cached prefix (instructions, tools, earlier turns) never holds the time.
- **Speech**: `SpokenText.Clean` runs on every sentence before TTS and captions: markdown emphasis, headings, list markers, code ticks and tables, links (the label stays), bare URLs, "(bbc.co.uk)" attributions and "[1]" / "【3†source】" markers are removed.
- **Visibility**: the agent raises the tool badge (`tool.web_search`, "Web search") from search start to end; there is no `ToolRoundExecutor` call and no extra model round. `evt=llm.web_search model= id= ms=` per search and `searches=` on `evt=llm.completed`.
- **Spend**: `ModelPrice.PerWebSearch` ($0.0025 for Spark, `JollyConfig.prices`), persisted per model as `webSearches` in usage.json.
- **Self-test**: `voice_web_search` speaks "What's the weather like today?" (Wit Charlie, like the other clips) and passes when `web_search` ran and an answer was spoken after it; search → answer audio over 6 s degrades.

## Consequences
- Jolly answers weather, news, scores, prices and opening hours for the user's area with no new service, key or licence; it is Meta's tooling, used beyond a plain chat call.
- A searched turn costs about 7× a plain one and its answer lands 1–6 s after the search. The question acknowledgement (ADR-0040) and the model's own preamble fill the gap, and the badge shows what is happening.
- The question, a coarse location (country, city the user gave, time zone) and the time reach Meta's search. No position, GPS or address is used; Quest has no GPS, and the time zone and locale are device settings. Meta's search back end and its retention are not documented (UNVERIFIED).
- An aborted searched request is settled with the usual token estimate; a search that ran before the abort is not counted (rare: barge-in during the 0.5–1 s search).
- The answer's quality is Meta's: "a strong starting point rather than a final authority" (docs). The prompt keeps it to one or two sentences.
- A persona that lists its tools must add `web_search` (and any `actions.*` tool) to offer it.

## Alternatives
- **A client weather API as a function tool** (e.g. Open-Meteo). Exact and free, but it covers one domain, adds a provider, a licence row and a round trip through our tool loop, and answers nothing about news, scores or opening hours. Rejected.
- **Deferred tool search for namespaces** (`defer_loading` + `tool_search`). Saves schema tokens on a large catalogue, costs about 0.5 s per load and counts against `max_tool_calls`; with a cap of 1 the model claimed an action it never called. Our catalogue is small and cached, so eager namespaces win. Revisit past ~30 tools.
- **`search_context_size: "medium"`**: more tokens per search for answers that are spoken in one or two sentences anyway. Rejected.
- **Search in `Degraded`**: more answers on a flaky link, at the price of a second slow pass. Rejected; the model says it cannot check.
- **Asking for location permission or using the IP's location**: finer than needed, a permission prompt in MR, and Quest has no GPS. Rejected; the user can say their city.
