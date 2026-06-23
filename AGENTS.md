# Agent instructions

Cursor rules for this repository live in **[`.cursor/rules/`](.cursor/rules/)**.

## Required workflow

1. **Create a GitHub issue** before code (use `scripts/arrdash-issue-create.sh`).
2. Implement with tests where appropriate.
3. Update `CHANGELOG.md` and relevant `docs/`.
4. Open a PR with `Closes #<issue>`.

See **[docs/github-workflow.md](docs/github-workflow.md)** for the full process.

## Rules summary

| File | Purpose |
|------|---------|
| [work-tracking.mdc](.cursor/rules/work-tracking.mdc) | Issues, branches, commits |
| [arrdash-development.mdc](.cursor/rules/arrdash-development.mdc) | .NET / Blazor / Docker conventions |
| [documentation.mdc](.cursor/rules/documentation.mdc) | Doc update expectations |
| [agent-workflow.mdc](.cursor/rules/agent-workflow.mdc) | Issue-first triggers |

## Open in Cursor

Open the **`ArrDash`** repository root as the workspace so `.cursor/rules/` apply automatically.

## Human docs

- [docs/README.md](docs/README.md) — documentation index
- [docs/development.md](docs/development.md) — build, test, contribute
