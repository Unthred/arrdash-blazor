#!/bin/bash
# Create GitHub labels and the ArrDash project board for Unthred/arrdash-blazor.
# Requires: GH_TOKEN or /boot/config/scripts/github-ha-project.token
set -euo pipefail

REPO="Unthred/arrdash-blazor"
OWNER="Unthred"
PROJECT_OWNER="@me"
PROJECT_TITLE="ArrDash"
GH_BIN="${GH_BIN:-/tmp/gh}"
GH_VERSION="2.74.2"
LOG_TAG="setup-github-arrdash-project"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $LOG_TAG: $*"; }

install_gh() {
  if [[ -x "$GH_BIN" ]]; then
    return 0
  fi
  log "Installing gh to $GH_BIN"
  local tmp
  tmp=$(mktemp -d)
  curl -fsSL "https://github.com/cli/cli/releases/download/v${GH_VERSION}/gh_${GH_VERSION}_linux_amd64.tar.gz" \
    | tar xz -C "$tmp"
  cp "$tmp/gh_${GH_VERSION}_linux_amd64/bin/gh" "$GH_BIN"
  chmod 755 "$GH_BIN"
  rm -rf "$tmp"
}

gh_cmd() { "$GH_BIN" "$@"; }

ensure_auth() {
  if [[ -z "${GH_TOKEN:-}" ]] && [[ -f /boot/config/scripts/github-ha-project.token ]]; then
    GH_TOKEN=$(tr -d '\r\n' < /boot/config/scripts/github-ha-project.token)
    export GH_TOKEN
  fi
  if [[ -z "${GH_TOKEN:-}" ]]; then
    log "ERROR: Set GH_TOKEN or create /boot/config/scripts/github-ha-project.token"
    exit 1
  fi
  export GH_TOKEN
}

create_labels() {
  while IFS='|' read -r name color description; do
    [[ -z "$name" ]] && continue
    if gh_cmd label list --repo "$REPO" --json name --jq ".[] | select(.name==\"$name\") | .name" | grep -q .; then
      log "Label exists: $name"
    else
      log "Create label: $name"
      gh_cmd label create "$name" --repo "$REPO" --color "$color" --description "$description" --force
    fi
  done <<'EOF'
area:ui|d4c5f9|Blazor UI, CSS, components
area:backend|c5def5|Services, API clients, collectors
area:settings|bfdadc|Settings page and preferences
area:tests|0e8a16|Unit tests
area:docs|e4e669|Documentation
area:infra|fbca04|Docker, compose, Unraid template
risk:low|0e8a16|Low blast radius
risk:high|b60205|Breaking or wide-reaching
needs-docker-test|fbca04|Verify in container after change
EOF
}

create_project() {
  if gh_cmd project list --owner "$PROJECT_OWNER" --format json | jq -e --arg t "$PROJECT_TITLE" '.projects[] | select(.title==$t)' >/dev/null; then
    log "Project already exists: $PROJECT_TITLE"
    gh_cmd project list --owner "$PROJECT_OWNER" --format json | jq -r --arg t "$PROJECT_TITLE" '.projects[] | select(.title==$t) | .number'
    return 0
  fi

  log "Creating project: $PROJECT_TITLE"
  local number
  number=$(gh_cmd project create --owner "$PROJECT_OWNER" --title "$PROJECT_TITLE" --format json | jq -r .number)

  log "Project number: $number"

  local statuses=(Todo "In Progress" Done)

  log "Adding Status field options (skip if project already has Status)"
  if ! gh_cmd project field-list "$number" --owner "$PROJECT_OWNER" --format json | jq -e '.fields[] | select(.name=="Status")' >/dev/null; then
    gh_cmd project field-create "$number" --owner "$PROJECT_OWNER" \
      --name Status --data-type SINGLE_SELECT \
      --single-select-options "${statuses[0]}" --format json >/dev/null || true
  fi

  echo "$number"
}

link_repo() {
  local project_number="$1"
  log "Linking $REPO to project"
  gh_cmd project link "$project_number" --owner "$PROJECT_OWNER" --repo "$REPO" 2>/dev/null || \
    log "Repo may already be linked"
}

main() {
  install_gh
  ensure_auth
  create_labels
  local project_number
  project_number=$(create_project)
  link_repo "$project_number"
  log "Done."
  log "Board: https://github.com/users/$OWNER/projects/$project_number"
  log "Create issues: bash scripts/arrdash-issue-create.sh --title '...' --body '...'"
}

main "$@"
