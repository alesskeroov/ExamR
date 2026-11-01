# AGENTS.md — ExamFlow

Shared rules for **every** coding agent working in this repository (OpenAI Codex, Claude Code, others).
Claude Code reads this file through `CLAUDE.md`. Agents take turns: when one agent's usage limit
runs out, the other continues from the same point. `TASKS.md` is the hand-off between them.

ExamFlow: upload exam PDFs → questions are parsed and stored → users take timed random exams from a
PDF's question range → results and history are stored per user.

## 1. Start of every session (mandatory, in this order)

1. Read this file (and `CLAUDE.md` if you are Claude Code).
2. Read `TASKS.md` — especially **"Davam edən iş / Handoff"**. If it describes unfinished work,
   continue it before starting anything new (unless the user asks otherwise).
3. Check Git state:
   ```bash
   git status --short
   git log --oneline -5
   ```
   Uncommitted changes may be the previous agent's unfinished work. Compare them with the hand-off
   note; never discard or overwrite them without asking the user.
4. Read only the docs relevant to the task (`docs/ARCHITECTURE.md` first for anything structural).

## 2. Project map

| Path | What |
|---|---|
| `src/ExamFlow.Api` | ASP.NET Core 9 Minimal API, EF Core 9 + SQL Server, Identity + JWT |
| `src/ExamFlow.Parsing` | PDF → questions (PdfPig). Pure library: no EF, no HTTP |
| `tests/ExamFlow.Parsing.Tests` | Parser tests (generated PDFs + real samples in `samples/`, not in Git) |
| `tests/ExamFlow.Api.Tests` | Unit tests for pure API logic (`ExamEngine`, `TokenService`) |
| `client` | React 19 + TypeScript + Vite + Tailwind v4 + TanStack Query + React Router |
| `docs/` | Architecture, API, DB schema, PDF, exam engine, frontend, roadmap (Azerbaijani) |
| `TASKS.md` | Status, next steps, known issues, hand-off |

Architecture details: `docs/ARCHITECTURE.md`. API contract: `docs/API_DESIGN.md`.

## 3. Commands

```bash
dotnet build                                   # whole solution, must be 0 errors / 0 warnings
dotnet test                                    # all .NET tests
dotnet run --project src/ExamFlow.Api          # API on http://localhost:5180
cd client && npm run dev                       # UI on http://localhost:5190 (proxies /api)
cd client && npm run build && npx oxlint       # type-check + build + lint
dotnet ef migrations add <Name> --project src/ExamFlow.Api -o Infrastructure/Data/Migrations
dotnet ef database update --project src/ExamFlow.Api
```

Required local secrets (`dotnet user-secrets`, project `src/ExamFlow.Api`): `ConnectionStrings:Default`,
`Jwt:SigningKey` (≥ 32 bytes). Without them the API refuses to start with a clear message.
Never print secret values in chat, logs, docs or commits.

## 4. Working rules

- **Minimal change.** Do what the task needs; no drive-by refactoring, renaming or reformatting.
- **Keep the architecture.** Feature folders (`Features/<Module>/`), Minimal API endpoint groups,
  `DbContext` used directly (no repositories, no MediatR, no AutoMapper). Changing this needs the
  user's explicit approval.
- **No new dependencies** (NuGet or npm) unless the task cannot be done without one; say why.
- **Database:** add a migration only when the model really changes. Never edit or delete a migration
  that is already in Git; add a new one.
- **Do not break** the PDF parser or the exam flow. Run the related tests after touching them.
- **Ask first** for big or irreversible decisions (architecture, schema redesign, deleting data,
  anything outward-facing).
- Answer the user in **Azerbaijani**. Code, identifiers, comments and commit messages in **English**.
  User-facing UI text and API error messages (`detail`) in **Azerbaijani**.

## 5. Code standards (same for every agent)

### Backend (.NET)
- One folder per module: `XxxEndpoints.cs` (routes + handlers), entities, `XxxDtos.cs`.
  Pure logic goes in a separate static class so it can be unit-tested (see `ExamEngine`).
- `async`/`await` with `CancellationToken` everywhere; read queries use `AsNoTracking()` and
  `Select` projections into DTO records. Filter **before** projecting into a DTO.
- Errors: `TypedResults.Problem(detail, statusCode)` (ProblemDetails). Validation:
  DataAnnotations on request classes + `.Validate<T>()` endpoint filter.
- Every endpoint group calls `RequireAuthorization()` unless it is deliberately public.
  User data is always filtered by `user.GetUserId()`; another user's resource returns **404**.
- Never send correct answers to the client before the exam's reveal rules allow it.
- Configuration via options classes + `appsettings.json`; secrets only in user-secrets / env vars.
- Match the existing code style (there is no `.editorconfig`): file-scoped namespaces, primary
  constructors, `sealed record` DTOs, nullable enabled, comments only where intent is not obvious.

### Frontend (React)
- Feature folders: `features/<module>/` with `api.ts` (types + TanStack Query hooks) and pages.
  Shared UI in `components/common/`; helpers in `lib/`. Query keys only in `lib/query-keys.ts`.
- All HTTP through `lib/api-client.ts` (adds the JWT). No axios, no global state library.
- Tailwind with the existing tokens (slate / indigo / emerald / rose / amber, `rounded-xl` cards).
  Loading → skeleton/spinner, empty → `EmptyState`, error → `Alert`. Accessible markup (labels, roles).
- Files that export React components export only components (lint rule `only-export-components`).

### Tests and checks before finishing a task
- `dotnet build` (0 warnings) and `dotnet test` when backend changed.
- `npm run build` and `npx oxlint` in `client/` when frontend changed.
- For behaviour changes, verify against the running app/API; report what was and was not verified.

## 6. Task protocol and hand-off

**After every finished task**, update `TASKS.md`:
- move the item to "Tamamlanmış işlər" (one line: what + date),
- adjust "Növbəti addımlar" and "Məlum problemlər" if they changed,
- clear the "Davam edən iş / Handoff" section if nothing is left.

**If you must stop with unfinished work** (usage limit, user stops you, blocker), fill in the
"Davam edən iş / Handoff" section of `TASKS.md` **before** ending:
- goal of the task (user's request in one or two sentences),
- what is done, what is left (concrete next steps, in order),
- changed / new files (`git status --short` output is fine),
- state of build/tests (passing? which fail and why?),
- migrations created / applied or not,
- anything the next agent must not do or must ask the user.

Write it so that an agent with **no memory of this conversation** can continue.

## 7. Git rules

- **Never commit or push without the user's explicit approval** in the current conversation.
  Approval for one commit does not cover later ones. Never force-push; never rewrite pushed history.
- When a task ends (or before switching agents), **propose** a commit: list the files to include,
  call out files that must not be committed, and give a commit message.
- Commit message style (as in `git log`): imperative subject ≤ 72 chars, blank line, short bullet
  list of what changed and why. Add your own agent attribution trailer if your tooling requires it.
- Must never be committed: `node_modules/`, `bin/`, `obj/`, `.env*`, `App_Data/` (uploads, emails),
  `samples/*.pdf` (real exam PDFs), secrets of any kind. `.gitignore` already covers these — check
  `git status` anyway.
- Remote: `origin` = `https://github.com/alesskeroov/ExamR`, branch `main`.

## 8. Environment notes (Windows)

- Git Bash rewrites `origin/main:path` arguments → prefix with `MSYS_NO_PATHCONV=1`.
- A running API locks `bin/`; stop it before `dotnet build` / `dotnet ef`.
- `sqlcmd` against tables with filtered indexes needs `-I` (QUOTED_IDENTIFIER) for writes.
- Rate limits (per IP, in-memory): login/register 10/min, forgot/reset password 5/15 min —
  restart the API to reset them while testing.
- Local emails are written as `.eml` files to `src/ExamFlow.Api/App_Data/mail/`.
