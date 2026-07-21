# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Pho is a mock web service — a configurable stub server used to imitate real web services during testing (comparable to WireMock, Mockoon, or MockServer). Test authors define fake endpoints and canned responses through a web UI; Pho serves them so a system under test can run against a controllable stand-in. It also records received requests so calls can be verified.

This is a **spec-first** project: `docs/spec/` is the single source of truth. Read it before implementing, and keep it updated as behavior changes — per the global rule, the spec must stay complete enough to regenerate the project from scratch. Start at `@docs/spec/SPEC.md`.

## Stack

.NET 10 · ASP.NET Core · Blazor Web App (Interactive Server) · EF Core + SQLite · xUnit + FluentAssertions **7.x** (never 8.0.0+).

## Build / Test

Solution file is `Pho.slnx` (the .NET 10 XML solution format); the arg-less commands below find it automatically.

- Build: `dotnet build`
- Test all: `dotnet test`
- Single test: `dotnet test --filter "FullyQualifiedName~<substring>"`
- Run the app: `dotnet run --project src/Pho.Web`

## Structure

- `src/Pho.Domain` — entities + pure logic (matching, config history); no external dependencies. Start TDD here.
- `src/Pho.Infrastructure` — EF Core (SQLite), repositories, retention/pruning.
- `src/Pho.Web` — ASP.NET Core host: Blazor UI + mock-serving middleware + in-process app services.
- `tests/Pho.Domain.Tests`, `tests/Pho.Web.Tests` — xUnit + FluentAssertions.

## Architecture notes

- **Two ports:** admin UI on 8080, mock-serving surface on 8081 (configurable). Middleware branches by port; the mock port treats every request as mock traffic. See `docs/spec/08-architecture.md`.
- **No public authoring API** in v1 — Blazor components call in-process services directly. Only the mock-serving surface is a public HTTP surface.
- **Stubs have no priority.** Multiple enabled stubs matching one request is an error (HTTP 500 + logged + flagged in UI), not resolved by ranking.
- **History is whole-configuration** (`ConfigRevision`), not per-stub; this is what makes cascade-delete-undo work. See `docs/spec/03-domain-model.md`.
