# 8. Architecture

## Stack decision — DEFERRED (by decision)

The technology stack is **intentionally deferred** until the spec is fully defined. The plan: complete and stabilize the specification, then have Claude **recommend the best-fit stack based on the finished project definition** — so the stack serves the requirements rather than constraining them. Until then, the spec is written to be stack-agnostic.

When the spec is deemed complete, produce a stack recommendation that weighs: fit to the feature set (two HTTP surfaces, tree UI, history, persistence), Docker Compose deployability, testability under BDD/TDD, and the user's existing preferences (see the FluentAssertions/.NET hint below). Present the recommendation for approval before scaffolding.

Requirements the stack must satisfy:

- Full-stack web app: a browser UI (the authoring experience, incl. a tree view) plus a backend that serves both the UI's API and the mock-serving surface.
- A persistent database.
- Packaged and run via **Docker Compose**.
- Testable under a BDD/TDD workflow (per the global rules).

> **Context:** the user's global rules reference **FluentAssertions** (a .NET assertion library), suggesting a likely preference for **.NET / C#** (e.g. ASP.NET Core backend, with a Blazor or a separate JS/TS frontend). This is a hint, not a decision — confirm before committing. If .NET is chosen, tests use xUnit/NUnit with FluentAssertions **7.x** (global rule: never 8.0.0+).

Once chosen, record here: backend framework, frontend framework/UI library, database, ORM/data layer, test framework, and lint/format tooling — then fill in the `Build / Test / Lint` and `Structure` sections of the root `CLAUDE.md`.

## Two HTTP surfaces

A core constraint (see `06-interfaces.md`): the **mock-serving surface** must be separable from the **authoring UI/API**, so a stub for an arbitrary path cannot collide with the app's own routes. The mechanism (separate port, path prefix, or hostname) is decided with the stack.

## Docker Compose topology

The intended `docker-compose.yml` (added during implementation) defines at least:

- **`app`** — the Pho application (UI + backend + mock-serving surface). Builds from a `Dockerfile` in the repo. Publishes the UI/mock port(s). Depends on the database being healthy.
- **`db`** — the persistent database, with a **named volume** for its data directory so definitions survive restarts.
- A healthcheck on `db` (and ideally `app`) so `depends_on` ordering is reliable.

Concrete images, ports, environment variables, and volume names are set when the stack is chosen. A non-working placeholder compose file is intentionally **not** committed yet, so the README does not advertise a deployment that fails.

## Project structure — TODO

_TODO: define the top-level layout (e.g. `src/` backend, `web/` or `client/` frontend, `docs/spec/`, `docker-compose.yml`, `Dockerfile`, tests) once the stack is chosen. Mirror it in the root `CLAUDE.md`._
