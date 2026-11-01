# 0001. Record architecture decisions

- Status: accepted
- Date: 2026-11-01

## Context
The project is reviewed on technical depth, and several coding agents work on it in parallel. Decisions need one written source of truth that is easy to diff.

## Decision
We use MADR-lite ADRs in `docs/decisions/NNNN-title.md` with the sections Status, Context, Decision, Consequences and Alternatives. An ADR is never edited after it is accepted. A new ADR supersedes it instead, and both files get their Status updated. The research files in `docs/research/` are evidence, and `critic.md` settles contradictions between them.

## Consequences
- Every key choice is listed in the README's ADR index.
- A PR that changes a public contract (`docs/contracts.md`) must reference an ADR.

## Alternatives
- A wiki or a single DESIGN.md. Rejected: it is harder to review per decision and history is lost.
