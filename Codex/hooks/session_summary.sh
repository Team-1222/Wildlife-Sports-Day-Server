#!/usr/bin/env sh
# Stop hook - *
# Prints a compact session summary from Codex/logs/activity.jsonl.

PATH="/usr/bin:/bin:$PATH"
export PATH

project_dir=${CLAUDE_PROJECT_DIR:-.}
log_file="$project_dir/Codex/logs/activity.jsonl"

[ -f "$log_file" ] || exit 0

total=$(wc -l < "$log_file" 2>/dev/null | tr -d ' ')
[ -z "$total" ] && exit 0
[ "$total" -eq 0 ] 2>/dev/null && exit 0

recent_file=$(mktemp 2>/dev/null || printf '%s' "$project_dir/Codex/logs/.recent_activity")
tail -n 200 "$log_file" > "$recent_file" 2>/dev/null || exit 0

recent_total=$(wc -l < "$recent_file" 2>/dev/null | tr -d ' ')
succeeded=$(grep -c '"success":true' "$recent_file" 2>/dev/null || printf '0')
failed=$((recent_total - succeeded))

printf '\n' >&2
printf '==============================================\n' >&2
printf '        Wildlife Survival - Session Summary\n' >&2
printf '==============================================\n' >&2
printf '  Commands logged : %s\n' "$recent_total" >&2
printf '  Succeeded       : %s\n' "$succeeded" >&2
printf '  Failed          : %s\n' "$failed" >&2

commands=$(sed -n 's/.*"command":"\([^"]*\)".*/\1/p' "$recent_file" | head -n 3)
if [ -n "$commands" ]; then
  printf '  Recent commands :\n' >&2
  printf '%s\n' "$commands" | while IFS= read -r cmd; do
    printf '    - %.50s\n' "$cmd" >&2
  done
fi

printf '\n' >&2
rm -f "$recent_file" 2>/dev/null
exit 0
