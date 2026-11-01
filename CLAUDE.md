# CLAUDE.md

All project rules, standards, commands and the hand-off protocol live in `AGENTS.md` so that
Claude Code and OpenAI Codex follow exactly the same rules. They are imported here:

@AGENTS.md

## Claude Code specifics

- Session start: follow `AGENTS.md` §1 (rules → `TASKS.md` → `git status` / `git log -5`) before any edit.
- Before your usage limit runs out or when the user switches to Codex, write the hand-off in
  `TASKS.md` (`AGENTS.md` §6) and propose — do not create — a commit (`AGENTS.md` §7).
- Do not add rules here that Codex would not see; put shared rules in `AGENTS.md`.
