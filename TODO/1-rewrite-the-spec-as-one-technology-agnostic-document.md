# 1 — Rewrite the spec as one technology-agnostic document, with AGENTS.md and a human-facing README

## Summary
Replace the multi-file spec in `docs/spec/` with a single, stack-free `SPEC.md` at the repo
root that describes every feature of Pho precisely and completely. Move the Gherkin
acceptance criteria out of the spec into executable feature files in a new
`Pho.Test.Acceptance` project, as part of reorganizing the test projects by test type
(Unit / Integration / Acceptance). Add an
`AGENTS.md` as the starting point for all AI agents, reduce `CLAUDE.md` to a pointer to it,
and polish `README.md` as the introduction for humans arriving from GitHub.

## Motivation
- The user wants to be able to recreate the whole project later **on any tech stack they
  choose**. Today the spec mixes behavior with implementation choices (.NET, Blazor,
  EF Core/SQLite, project layout, Docker Compose topology, port numbers), which ties it to
  the current stack.
- Nine spec files plus an index are harder to read and keep in sync than one document.
- Agent guidance currently lives only in `CLAUDE.md`; other AI tools (Codex, Cursor, etc.)
  look for `AGENTS.md`.
- `README.md` is already in good shape; it should read clearly as the introduction for a
  human seeing the project for the first time on GitHub, while keeping its deployment
  and backup instructions front and centre.

## The three layers of specification (the user's model)
This is the organizing principle for the whole item; it must be written down in `AGENTS.md`
so future agents put things in the right layer.

1. **`SPEC.md` — what the product does, stack-free.** Example: "the admin UI is accessible
   from a web browser". It never mentions hosting choices (Docker), ports, endpoints,
   frameworks or storage technology.
2. **Gherkin feature files — decisions, verified at a slightly more granular level, still
   without saying *how*.** Gherkin is part of the specification, just not part of
   `SPEC.md`. Example: once it is decided to host in Docker, a scenario says the container
   can indicate when it is healthy — without saying how.
3. **Code (including the step definitions behind the Gherkin) — the exact mechanism.**
   Example: the app has a health endpoint that works and that the Dockerfile's health check
   uses.

Consequences:
- Things that "just exist" to support a decision (health endpoint, default ports
  8931/8932, file names, schemas) do not belong in `SPEC.md`; they live in Gherkin and/or
  code.
- Coding standards and process rules (BDD/TDD, Given/When/Then style, Red-Green-Refactor)
  belong in agent instructions (`AGENTS.md`), not in `SPEC.md`.

## Behavior

### 1. Single spec document — `SPEC.md`
- One file, `SPEC.md` at the repo root (next to `README.md`), replaces `docs/spec/SPEC.md`
  and `docs/spec/01-…09-*.md`. The `docs/spec/` folder (and `docs/` if then empty) is
  deleted.
- **Must not contain**: tech stack, frameworks, languages, databases, folder/project
  layout, file schemas, JSON/code samples, class/entity names written as code,
  container/compose topology, hosting choices, **port numbers**, **Gherkin /
  Given-When-Then**, or development-process rules.
- **Must contain** a precise, detailed listing of every feature — enough that the whole
  product could be rebuilt from it on an arbitrary stack, restated in product language:
  - what Pho is, the problem, users/personas, roles and permissions (anonymous in v1);
  - the concepts: groups (nested), stubs, request matching rules (methods, path, query,
    headers, body, case sensitivity — names vs values, ignore-case option, Basic auth
    pattern matching), response definitions, received requests;
  - matching resolution, including "no priority — multiple enabled matches is an error
    (reported to the caller as a server error, logged, and flagged in the UI)";
  - whole-configuration history with undo/redo (and that undoing a cascading delete
    restores everything it removed);
  - all features F1–F12 described as precise prose requirements (manage stubs,
    enable/disable, nested groups, match and serve, verify received requests, undo/redo,
    export/import, body formatting/validation, spell-check hints, light/dark theme,
    keyboard tree navigation, Basic auth credential helper);
  - screens, layout, navigation and states (stub tree, stub editor, received-requests view,
    export/import flow, theming);
  - two separate surfaces (admin vs mock-serving), each independently configurable, and
    support for being served under a sub-path behind a reverse proxy; configuration
    settings, retention/pruning, persistence expectations, concurrency, performance,
    security, observability — as requirements, not implementations;
  - glossary.
- Keep the current product decisions (anonymous auth in v1, verification is
  human-observed only, no public authoring API).

### 2. Gherkin feature files — executable
- The Given/When/Then scenarios now embedded in `docs/spec/04-features.md` move out into
  `.feature` files in the new **`Pho.Test.Acceptance`** project (no `.feature` files exist
  in the repo today).
- They are **executable now**: bound to step definitions and run by `dotnet test`. Every
  scenario must pass (or, if one exposes a real gap between spec and code, the gap is
  reported to the user rather than silently papered over).
- They may also gain scenarios for stack decisions that `SPEC.md` deliberately omits
  (e.g. the container reports its health; the default ports), still phrased without
  mechanism.
- Every behavior in a scenario must still be traceable to a requirement in `SPEC.md`, or
  to a recorded decision (in `AGENTS.md`) that the scenario supports.

### 3. Restructure the test projects by test type
The user's reasoning: the *type* of test is what drives a test project's dependencies, more
than which production project it exercises. Replace the current per-layer projects with:

| New project | Holds | Typical dependencies |
|---|---|---|
| `tests/Pho.Test.Unit` | Pure, in-memory tests of logic | xUnit, FluentAssertions 7.x, production projects only |
| `tests/Pho.Test.Integration` | Tests against real infrastructure (SQLite/EF Core, the hosted app) | + EF Core/SQLite, `Microsoft.AspNetCore.Mvc.Testing` as needed |
| `tests/Pho.Test.Acceptance` | The `.feature` files + step definitions | + a Gherkin runner (e.g. Reqnroll with xUnit), `Mvc.Testing` to drive the app, bUnit for UI scenarios |

- Every existing test moves to the project matching its type; no test is lost (compare
  test counts before and after).
- Update `Pho.slnx`, the `Dockerfile` (if it copies/restores test projects) and any docs.

### 4. AGENTS.md
- New root `AGENTS.md`: the entry point for every AI agent. Holds everything `CLAUDE.md`
  holds today: project summary, spec-first rule and where the spec is, stack, build/test/run
  commands, code structure, architecture notes, the `## TODO` backlog rules.
- Adds: the three-layer model above; coding standards and the BDD/TDD process
  (Red-Green-Refactor, Gherkin for acceptance, feature files in `Pho.Test.Acceptance`,
  which test type goes in which test project and why); the technical
  decisions removed from the spec (stack choice, two-port design and default ports, Docker
  Compose hosting, reverse-proxy mechanics, project layout).

### 5. CLAUDE.md
- Contains **nothing but the pointer** to `AGENTS.md` (`@AGENTS.md`).

### 6. README.md
- The user considers the current README **pretty good** — this is a light touch-up toward
  a GitHub visitor's introduction, not a rewrite. Improve the intro (what Pho is, why you'd
  use it, key features at a glance, how to use it: define a stub, point the system under
  test at the mock surface, check received requests), and replace links into `docs/spec/`
  with links to `SPEC.md`.
- **Must retain all installation and backup instructions in the README itself**, kept
  prominent so someone viewing the repo on GitHub immediately sees how to deploy Pho and
  back up their data: prerequisites, `install.sh` quick install, manual install,
  reverse-proxy setup, backup and restore, local development. Don't move these into the
  spec or another doc, and don't bury them below lengthy new material. (Ports, Docker etc.
  are fine here — the README is operational, not the spec.)

## Acceptance criteria
- Exactly one spec document exists, at `SPEC.md` in the repo root; `docs/spec/` is gone.
- `SPEC.md` contains no mention of .NET, ASP.NET, Blazor, EF Core, SQLite, Docker, xUnit,
  port numbers, health checks, source folders/projects, code blocks, JSON samples, or
  Given/When/Then (grep-checkable).
- Every behavior from the current `docs/spec/*.md` is accounted for in exactly the right
  layer — `SPEC.md`, a `.feature` file, or `AGENTS.md` (do a section-by-section checklist
  before deleting the old files).
- All Gherkin scenarios from `docs/spec/04-features.md` exist as `.feature` files in
  `tests/Pho.Test.Acceptance`, are bound to step definitions, and pass under `dotnet test`
  (no pending/undefined steps).
- The test projects are exactly `Pho.Test.Unit`, `Pho.Test.Integration` and
  `Pho.Test.Acceptance`; the old `Pho.Domain.Tests`, `Pho.Infrastructure.Tests` and
  `Pho.Web.Tests` are gone; every previously existing test still exists and passes.
- FluentAssertions stays on 7.x in every test project.
- `SPEC.md` is consistent with the code as currently implemented (tests in
  `tests/Pho.Domain.Tests`, `tests/Pho.Web.Tests` are a good cross-check for detail the
  current spec may be missing).
- `AGENTS.md` exists at the root, describes the three-layer model and coding standards, and
  links to `SPEC.md`, the feature files and `TODO.md`.
- `CLAUDE.md` contains only the `@AGENTS.md` pointer.
- `README.md` reads as an introduction for a newcomer; every installation, deployment and
  backup/restore instruction from the prior README is still in the README and easy to find
  near the top; it links to `SPEC.md` and `TODO.md`.
- No dangling links to `docs/spec/...` anywhere in the repo (README, CLAUDE.md, AGENTS.md,
  `src/`, comments, `TODO/`).

## Technical notes
- Where the technical content in the current spec lives:
  - `docs/spec/08-architecture.md` — almost entirely technical (stack decision, project
    structure, compose topology) → `AGENTS.md`. Its reverse-proxy and two-surface sections
    contain product behavior that must survive in neutral form in `SPEC.md`.
  - `docs/spec/07-non-functional.md` — deployment/persistence sections mention the stack
    and the health check (`Program.cs`, `Dockerfile`, `docker-compose.yml` implement it).
  - `docs/spec/04-features.md` — Gherkin source for the feature files. F8 "Body formatting
    and validation (JSON / XML)" is a **user-facing** feature (users edit JSON/XML bodies),
    so naming those body formats in `SPEC.md` is product behavior. F7 export/import:
    describe what is exported/restored, not the file schema.
  - `docs/spec/06-interfaces.md` — internal authoring API section is implementation; the
    mock-serving surface is product behavior.
  - `docs/spec/03-domain-model.md` — entity names (`ConfigRevision`, `RequestMatcher`…)
    become plain concepts ("configuration revision", "matching rules").
  - `docs/spec/SPEC.md` "Development method" section → `AGENTS.md`.
- `CLAUDE.md` currently links `@docs/spec/SPEC.md`, `docs/spec/08-architecture.md`,
  `docs/spec/03-domain-model.md`; README links `docs/spec/SPEC.md` and
  `docs/spec/04-features.md` — update all.
- Search `src/` and `tests/` for references to `docs/spec` in comments.
- Current test projects (note: `CLAUDE.md` lists only two of the three) and their likely
  destinations — classify each test individually, these are starting points:
  - `tests/Pho.Domain.Tests` (refs `Pho.Domain` only; `BasicAuthTests`, `MatchRuleTests`,
    `MockResolverTests`, `Tree*Tests`, etc.) → `Pho.Test.Unit`.
  - `tests/Pho.Infrastructure.Tests` (refs `Pho.Infrastructure`; `ConfigHistoryIntegrationTests`,
    `ConfigPorterTests`, `StubPersistenceTests`, `ReceivedRequestLogTests`, `*ServiceTests`)
    → mostly `Pho.Test.Integration` (real SQLite); any pure-logic ones → Unit.
  - `tests/Pho.Web.Tests` (`Mvc.Testing`; `MockServingTests`, `AdminUiTests`, shared
    `TestDoubles.cs` with `InMemoryStubStore`, `FakeReceivedRequestLog`,
    `AlwaysMockTrafficPolicy`) → `Pho.Test.Integration`, or become step definitions in
    Acceptance where they duplicate a scenario. `TestDoubles.cs` may need to be shared
    by Integration and Acceptance — whether to duplicate it or add a small shared
    test-support project is deliberately left to be decided during implementation
    (user's call: "decide as we go"; prefer the simplest).
- Namespaces change with the project rename (`Pho.Domain.Tests` → `Pho.Test.Unit`, etc.).
- Gherkin runner: Reqnroll (the maintained SpecFlow successor) with its xUnit integration
  is the likely choice for .NET 10; confirm it supports .NET 10 and the xUnit version in use.
- Acceptance step definitions should drive Pho through its real surfaces
  (`WebApplicationFactory<Program>` over HTTP for the mock surface). **UI scenarios
  (stub tree, editor, keyboard tree navigation, theme, spell-check hints, Basic auth
  helper, received-requests view, export/import flow) bind via bUnit** — decided by the
  user; no browser automation. Keep steps free of mechanism in the `.feature` text.
- Gherkin for the health check: `Program.cs`, `Dockerfile` and `docker-compose.yml`
  already implement a health check; a scenario like "the service reports when it is
  healthy" can bind to that endpoint.
- The global rule "keep the spec complete enough to regenerate the project" still applies;
  regenerating *this* implementation now takes `SPEC.md` + feature files + `AGENTS.md`
  together — say so in `AGENTS.md`.

## Out of scope
- Any change to product behavior or production code (beyond what's strictly needed to
  make it testable from the acceptance project).
- Moving to a different tech stack.
