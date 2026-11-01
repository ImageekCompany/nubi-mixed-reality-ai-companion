# 0008. Async model: UniTask and `IUniTaskAsyncEnumerable<T>`

- Status: accepted
- Date: 2026-11-01

## Context
Requirement 3 mandates UniTask. Every stage streams and must be cancellable. Unity 6 also offers `Awaitable`, which ARF 6.6 returns from anchor APIs.

## Decision
- `com.cysharp.unitask` **2.5.11**, from the OpenUPM scoped registry (`https://package.openupm.com`, scope `com.cysharp`). MIT.
- All contracts use `UniTask`/`UniTask<T>` and `IUniTaskAsyncEnumerable<T>`, with `CancellationToken` as the last parameter. No `Task`, no `async void`, no coroutines in new code.
- `Awaitable` results from Unity APIs are awaited directly inside adapters and never exposed.
- Streams are implemented with `UniTaskAsyncEnumerable.Create` (assembly **`UniTask.Linq`**, namespace `Cysharp.Threading.Tasks.Linq`) or `Channel.CreateSingleConsumerUnbounded` (core `UniTask`). Both are verified in UniTask source. Every asmdef that builds or composes streams references both `UniTask` and `UniTask.Linq`.
- `UnityWebRequest` is awaited with `ToUniTask(cancellationToken: ct)` with the default `cancelImmediately:false`, which calls `webRequest.Abort()` in `MoveNext` on the PlayerLoop (verified, `UnityAsyncExtensions.cs:1149-1152`). `cancelImmediately:true` would abort from the cancelling thread and is banned.
- Timeouts use `CancelAfterSlim` or `TimeoutController`, never `CancellationTokenSource.CancelAfter`, whose thread-pool timer would complete channel readers off the main thread (`Channel.cs:295,307-311`).
- Provider streams resume on the main thread (contracts §0).

## Consequences
- One async vocabulary. Barge-in is a token cancel.
- Unity 6.6 compatibility of UniTask is UNVERIFIED until the WP0 IL2CPP build (B12).

## Alternatives
- **`Awaitable`:** no `WhenAll`/LINQ/channels, and it is reused-instance-unsafe.
- **`System.Threading.Tasks`:** thread-pool continuations and allocations.
- **R3:** reactive streams are excellent for UI, but a second paradigm is unnecessary here.
