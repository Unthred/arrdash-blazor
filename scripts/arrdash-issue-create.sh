#!/bin/bash
# Create a GitHub issue on Unthred/arrdash-blazor and add it to the ArrDash project board.
# Also supports adding existing issues: arrdash-issue-create.sh --add 10
set -euo pipefail

REPO="Unthred/arrdash-blazor"
PROJECT_OWNER="@me"
PROJECT_TITLE="ArrDash"
GH_BIN="${GH_BIN:-/tmp/gh}"
DEFAULT_STATUS="Todo"

log() { echo "[arrdash-issue-create] $*"; }

usage() {
  cat <<'EOF'
Usage:
  arrdash-issue-create.sh --title TITLE --body BODY [--label LABEL]... [--status STATUS]
  arrdash-issue-create.sh --add ISSUE_NUM [ISSUE_NUM...] [--status STATUS]

Creates issues on Unthred/arrdash-blazor and adds them to the ArrDash project board.
Default status: Todo. Board statuses: Todo, In Progress, Done.

Examples:
  arrdash-issue-create.sh --title "Fix status bar wrap" --body "..." --label area:ui --label risk:low
  arrdash-issue-create.sh --add 3 --status "In progress"
EOF
}

ensure_auth() {
  if [[ -z "${GH_TOKEN:-}" ]] && [[ -f /boot/config/scripts/github-ha-project.token ]]; then
    GH_TOKEN=$(tr -d '\r\n' < /boot/config/scripts/github-ha-project.token)
    export GH_TOKEN
  fi
  if [[ -z "${GH_TOKEN:-}" ]]; then
    log "ERROR: Set GH_TOKEN or create /boot/config/scripts/github-ha-project.token"
    exit 1
  fi
}

gh_cmd() { "$GH_BIN" "$@"; }

project_number() {
  gh_cmd project list --owner "$PROJECT_OWNER" --format json \
    | jq -r --arg t "$PROJECT_TITLE" '.projects[] | select(.title==$t) | .number'
}

project_id() {
  gh_cmd project list --owner "$PROJECT_OWNER" --format json \
    | jq -r --arg t "$PROJECT_TITLE" '.projects[] | select(.title==$t) | .id'
}

status_field_id() {
  local num="$1"
  gh_cmd project field-list "$num" --owner "$PROJECT_OWNER" --format json \
    | jq -r '.fields[] | select(.name=="Status") | .id'
}

status_option_id() {
  local num="$1"
  local name="$2"
  gh_cmd project field-list "$num" --owner "$PROJECT_OWNER" --format json \
    | jq -r --arg n "$name" '.fields[] | select(.name=="Status") | .options[] | select(.name==$n) | .id'
}

add_issue_to_board() {
  local issue_num="$1"
  local status="${2:-$DEFAULT_STATUS}"
  local url="https://github.com/$REPO/issues/$issue_num"
  local pnum pid item_id field_id option_id

  pnum=$(project_number)
  if [[ -z "$pnum" || "$pnum" == "null" ]]; then
    log "ERROR: Project '$PROJECT_TITLE' not found. Run scripts/setup-github-arrdash-project.sh first."
    exit 1
  fi

  pid=$(project_id)

  log "Add #$issue_num to project board"
  if item_id=$(gh_cmd project item-add "$pnum" --owner "$PROJECT_OWNER" --url "$url" --format json 2>/dev/null | jq -r '.id'); then
    :
  else
    log "Issue may already be on the board — looking up item id"
    item_id=$(gh_cmd project item-list "$pnum" --owner "$PROJECT_OWNER" --format json \
      | jq -r --arg n "$issue_num" '.items[] | select(.content.url | endswith("/issues/" + $n)) | .id' | head -1)
    if [[ -z "$item_id" || "$item_id" == "null" ]]; then
      log "ERROR: Could not add or find issue #$issue_num on the board"
      exit 1
    fi
  fi

  field_id=$(status_field_id "$pnum")
  option_id=$(status_option_id "$pnum" "$status")
  if [[ -z "$option_id" || "$option_id" == "null" ]]; then
    log "WARN: Unknown status '$status' — card added but status not set"
    return 0
  fi

  gh_cmd project item-edit \
    --id "$item_id" \
    --project-id "$pid" \
    --field-id "$field_id" \
    --single-select-option-id "$option_id" >/dev/null
  log "Issue #$issue_num on board (Status: $status) — $url"
}

TITLE=""
BODY=""
STATUS="$DEFAULT_STATUS"
LABELS=()
ADD_MODE=false
ADD_NUMS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --title) TITLE="$2"; shift 2 ;;
    --body) BODY="$2"; shift 2 ;;
    --body-file) BODY="$(cat "$2")"; shift 2 ;;
    --label) LABELS+=("$2"); shift 2 ;;
    --status) STATUS="$2"; shift 2 ;;
    --add) ADD_MODE=true; shift; while [[ $# -gt 0 && "$1" != --* ]]; do ADD_NUMS+=("$1"); shift; done ;;
    -h|--help) usage; exit 0 ;;
    *) log "Unknown argument: $1"; usage; exit 1 ;;
  esac
done

ensure_auth

if [[ ! -x "$GH_BIN" ]]; then
  log "ERROR: gh not found at $GH_BIN (run scripts/setup-github-arrdash-project.sh)"
  exit 1
fi

if $ADD_MODE; then
  if [[ ${#ADD_NUMS[@]} -eq 0 ]]; then
    log "ERROR: --add requires at least one issue number"
    exit 1
  fi
  for num in "${ADD_NUMS[@]}"; do
    add_issue_to_board "$num" "$STATUS"
  done
  exit 0
fi

if [[ -z "$TITLE" || -z "$BODY" ]]; then
  log "ERROR: --title and --body are required (or use --add)"
  usage
  exit 1
fi

CREATE_ARGS=(issue create --repo "$REPO" --title "$TITLE" --body "$BODY")
for label in "${LABELS[@]}"; do
  CREATE_ARGS+=(--label "$label")
done

ISSUE_URL=$(gh_cmd "${CREATE_ARGS[@]}")
ISSUE_NUM="${ISSUE_URL##*/}"
log "Created issue #$ISSUE_NUM — $ISSUE_URL"
add_issue_to_board "$ISSUE_NUM" "$STATUS"
