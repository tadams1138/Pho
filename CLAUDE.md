# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Pho is a mock web service — a configurable stub server used to imitate real web services during testing (comparable to WireMock, Mockoon, or MockServer). Test authors define fake endpoints and canned responses through a web UI; Pho serves them so a system under test can run against a controllable stand-in. It also records received requests so calls can be verified.

This is a **spec-first** project: `docs/spec/` is the single source of truth. Read it before implementing, and keep it updated as behavior changes — per the global rule, the spec must stay complete enough to regenerate the project from scratch. Start at `@docs/spec/SPEC.md`.

## Status

Greenfield — no application code yet, and the stack is not chosen (see `docs/spec/08-architecture.md`). Fill in the command/structure sections below once tooling exists.

## Build / Test / Lint

_TODO: record exact commands once the stack is chosen (dev server, build, test, lint/format, and how to run a single test)._

## Structure

_TODO: describe the top-level layout once it exists (see `docs/spec/08-architecture.md`)._
