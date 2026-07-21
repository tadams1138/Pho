# Pho — Specification

This directory is the **single source of truth** for Pho. It is written to a deliberately high bar: it must stay complete and accurate enough that a Claude agent could **regenerate the entire project from these documents alone**. When behavior changes, update the spec in the same change — the spec leads, the code follows.

## How to read this spec

| File | Contents |
|------|----------|
| [`01-overview.md`](01-overview.md) | What Pho is, v1 scope, non-goals, open questions |
| [`02-users-and-roles.md`](02-users-and-roles.md) | Who uses Pho and their permissions |
| [`03-domain-model.md`](03-domain-model.md) | Core entities (Stub, Group, ReceivedRequest, ConfigRevision), matching resolution, configuration history |
| [`04-features.md`](04-features.md) | Capabilities as testable acceptance criteria (BDD) |
| [`05-screens-and-flows.md`](05-screens-and-flows.md) | UI screens, navigation, and states |
| [`06-interfaces.md`](06-interfaces.md) | The mock-serving runtime surface and the app's internal API |
| [`07-non-functional.md`](07-non-functional.md) | Persistence, deployment, concurrency, security, performance |
| [`08-architecture.md`](08-architecture.md) | Stack decision, Docker Compose topology, project layout |
| [`09-glossary.md`](09-glossary.md) | Terms of art used throughout |

## Development method

Per the global rules, work proceeds **spec-first** and via **BDD/TDD** (Red → Green → Refactor). The acceptance criteria in `04-features.md` are the source for tests: write the failing test from a criterion, then the minimum code to pass it.

## Open questions

Decisions not yet made are marked inline with `> **Open question:**` callouts. The load-bearing ones today:

1. **Technology stack** — intentionally **deferred** until the spec is complete, then Claude recommends a best-fit stack for approval (`08-architecture.md`).

Decided so far:
- **Authentication** — the authoring UI uses anonymous authorization in v1; Active Directory is a candidate for a later iteration (`02-users-and-roles.md`).
- **Verification** — human-observed in the UI only, by design (no programmatic query endpoint, none planned); the full request and its receipt time are observable (`06-interfaces.md`).
