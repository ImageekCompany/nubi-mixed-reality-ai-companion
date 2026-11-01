# 0009. Dependency injection: a plain composition root

- Status: accepted
- Date: 2026-11-01

## Context
There are about 30 services, one scene and one app lifetime, and several agents build in parallel. Requirement 3 asks for testability and no spaghetti.

## Decision
- `JollyBootstrap` (`Jolly.App`) constructs the whole graph with `new` in `Awake`. It hands dependencies to scene MonoBehaviours through `Construct(...)` methods and owns `appCts` and disposal.
- No service locator, no static singletons, no `FindObjectOfType`.
- Platform adapters are selected with `#if UNITY_ANDROID && !UNITY_EDITOR` in the root only.

## Consequences
- The graph is greppable, a missing dependency is a compile error, and there is no reflection to keep from being stripped under IL2CPP.
- The root file grows (about 200 lines). It is split by feature into `partial` builder methods if needed.

## Alternatives
- **VContainer (MIT):** scopes and auto-wiring, at the cost of a dependency, IL2CPP and stripping configuration, a learning curve for every agent, and runtime errors instead of compile errors. Revisit if multi-scene lifetimes appear.
- **Zenject/Extenject:** heavier, and slow under IL2CPP.
