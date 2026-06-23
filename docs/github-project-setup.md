# GitHub Project setup (one-time)

The **ArrDash** project board is **not** created automatically until you run the setup script with a GitHub token. Issue templates and workflow docs are in the repo; the **board** lives under your GitHub user **Projects**.

## Why a token?

Git push uses **SSH**. GitHub **Projects** API needs a **Personal Access Token** (PAT).

On Unraid, a shared token may already exist at `/boot/config/scripts/github-ha-project.token` (used by HA and ha-staging-kit scripts).

## One-time setup

### 1. PAT permissions

Classic PAT with **`repo`** + **`project`**, or fine-grained with Issues and Projects access on `Unthred/ArrDash`.

### 2. Run setup

```bash
cd /path/to/ArrDash
export GH_TOKEN='github_pat_...'   # optional if token file exists
bash scripts/setup-github-arrdash-project.sh
```

This creates:

- GitHub labels (`area:*`, `risk:*`, `needs-docker-test`)
- **ArrDash** project with Status: Todo, In Progress, Done
- Link between project and `Unthred/ArrDash`

### 3. Find the board

| Where | How |
|-------|-----|
| Your projects | GitHub avatar → **Your projects** → **ArrDash** |
| Repo | **Projects** tab (after link) |

If the **Projects** tab is empty but issues have cards, link manually: Repo → **Projects** → **Link a project** → **ArrDash**.

### 4. Configure board columns (once)

In the project board UI, map **Status** values to columns if they appear in one column initially.

## Create issues after setup

```bash
bash scripts/arrdash-issue-create.sh --title "[ArrDash] My change" --body "..." --label area:backend --status Todo
```

## Without a token (manual)

1. GitHub → **Projects** → **New project** → **Board** → name **ArrDash**
2. Add Status options: Todo, In Progress, Done
3. Repo → **Settings** → **Labels** — create labels from [github-workflow.md](github-workflow.md)
4. Link project to `Unthred/ArrDash`
5. Create issues via **ArrDash change** template

## Related repos

| Repo | Issue script | Project |
|------|--------------|---------|
| HomeAssistant config | `ha-issue-create.sh` | HA Config Pipeline (#2) |
| ha-staging-kit | `ha-staging-kit-issue-create.sh` | ha-staging-kit (#4) |
| **ArrDash** | `scripts/arrdash-issue-create.sh` | **ArrDash** |
