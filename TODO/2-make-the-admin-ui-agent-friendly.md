# 2 — Make the admin UI agent-friendly

## Summary
Make Pho's admin UI easy for browser agents (e.g. Claude in Chrome) to understand and
operate: semantic, accessible markup; a visible Help page also served as `/llms.txt`; and
a pointer to the MCP endpoint (#1) for heavier work. No hidden instructions.

## Motivation
Users will drive Pho through browser-agent plugins. Those agents read the page mostly
through the accessibility tree and page text, not pixels, so the quality of the markup
decides how well they do. The same work improves Pho for screen-reader and keyboard users.

## Behavior

### 1. Semantic, accessible markup
- Every input has a real, associated label; every button has an accessible name;
  icon-only buttons have `aria-label`.
- The stub tree uses tree semantics (`role="tree"` / `treeitem` / `group`,
  `aria-expanded`, `aria-selected`, `aria-level`) — complements F11 keyboard navigation.
- Outcome and state are text, not only colour/icons: "Saved", validation errors, the
  ambiguous-match flag, undo/redo availability. Async results are announced via
  `aria-live` regions so an agent (and a screen reader) knows when an action finished or
  failed — important because Blazor Interactive Server updates without page loads.
- Every action available by drag-and-drop (e.g. moving a stub or group) also has a
  button/menu equivalent.
- Stable, meaningful element ids or `data-testid` attributes on key controls (also useful
  to the bUnit acceptance tests in `Pho.Test.Acceptance`).
- Page `<title>` and headings reflect the current view (e.g. "Edit stub 'Get user' — Pho").

### 2. Help page + `/llms.txt`
- A Help page, linked from the global layout, explaining what isn't obvious from the
  screens: stubs vs groups; how matching works (methods, path, query, headers, body, case
  sensitivity); **stubs have no priority — two enabled matches is an error**; undo/redo is
  whole-configuration; where received requests appear and what they contain; export/import
  modes; the mock surface vs the admin surface.
- The same content served as Markdown at `/llms.txt` (an emerging convention agents look
  for), honouring the configured sub-path behind a reverse proxy.
- Single source for both, so they can't drift.

### 3. Point agents to MCP
- The Help page and `/llms.txt` state that an MCP endpoint is available (its URL, relative
  to the current base) and what it offers, so a browser agent can suggest the user connect
  it for bulk or scripted work.

### 4. No hidden instructions
- No text addressed to AI agents that is invisible to the human user (hidden elements,
  comments, off-screen text, meta tags with instructions). Agents treat hidden
  instructions as likely prompt injection; visible documentation is read as trustworthy
  reference material instead. Record this as a rule in `AGENTS.md`.

## Acceptance criteria
- Automated accessibility check (e.g. axe-core rules, or bUnit assertions on roles/labels)
  reports no missing labels/names on the stub tree, stub editor, received-requests view
  and export/import flow.
- The stub tree exposes tree roles and expanded/selected state.
- Save, validation and conflict outcomes appear as text inside a live region.
- Every drag-and-drop action has a non-drag equivalent.
- A Help page is linked from every screen; `/llms.txt` returns the same content as
  Markdown, also under a configured sub-path.
- Help and `/llms.txt` mention the MCP endpoint.
- No hidden agent-directed text exists in the rendered HTML.

## Technical notes
- Razor components under `src/Pho.Web` (stub tree, editor, received-requests,
  export/import) get the markup changes; `App.razor` / the main layout gets the Help link.
- `/llms.txt`: map in `src/Pho.Web/Program.cs` next to `/health` and `/export` on the admin
  surface; generate from the same Markdown source as the Help page (e.g. an embedded
  `.md` rendered to HTML for the page, served raw for `llms.txt`).
- Spec: `SPEC.md` gains "the admin UI is operable by assistive technology and
  agents" and "built-in help"; ARIA/llms.txt specifics go to Gherkin/code per the
  three-layer model.
- Optional: try a real Claude-in-Chrome run against Pho afterwards to find remaining
  friction.

## Dependencies
- **#1** for the MCP pointer (the rest can ship independently; add the pointer when #1
  lands).
- The requirements and scenarios live in `SPEC.md` and `Pho.Test.Acceptance` (both exist).

## Out of scope
- A redesign of the UI.
- Hidden or agent-only instructions in the HTML.
