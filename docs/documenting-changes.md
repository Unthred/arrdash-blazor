# Documenting changes

Every merged change that affects behaviour, configuration, or deployment should update documentation in the **same PR**.

## Checklist

| Change type | Update |
|-------------|--------|
| New setting | `docs/settings-reference.md`, `SettingsHelpTexts.cs`, tests |
| New env var | `docs/configuration.md`, `docker-compose.example.yml` |
| New service / client | `docs/services.md`, `docs/architecture.md` |
| New HTTP route | `docs/api.md`, `Program.cs` comment if non-obvious |
| Deploy / Docker | `docs/deployment.md`, `README.md` if quick start changes |
| Bugfix users would notice | `CHANGELOG.md` under `[Unreleased]` |
| Agent/workflow | `docs/github-workflow.md`, `.cursor/rules/` |

## CHANGELOG.md

Use [Keep a Changelog](https://keepachangelog.com/) sections:

```markdown
## [Unreleased]

### Added
- ...

### Changed
- ...

### Fixed
- ...
```

On release, rename `[Unreleased]` to a version/date heading.

## Issue / PR

- Issue template asks which docs to touch.
- PR template has documentation checkboxes — fill them in.

## Checkpoints

Optional maintainer notes in `CHECKPOINT.md` for session handoff — not a substitute for user-facing docs in `docs/`.

## Writing style

- Complete sentences; link to files with paths users can click on GitHub.
- Prefer FQDN examples (`https://sonarr.example.com`) over personal hostnames in committed docs.
- For settings, describe **user-visible outcome**, not only property names.
