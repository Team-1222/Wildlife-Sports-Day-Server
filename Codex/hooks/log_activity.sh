#!/usr/bin/env sh
# PostToolUse hook - Bash
# Appends a JSONL activity entry for each Bash tool call.

PATH="/usr/bin:/bin:$PATH"
export PATH

payload=$(cat)
project_dir=${CLAUDE_PROJECT_DIR:-.}
log_dir="$project_dir/Codex/logs"
log_file="$log_dir/activity.jsonl"

mkdir -p "$log_dir" 2>/dev/null || exit 0

extract_json_string() {
  key="$1"
  printf '%s' "$payload" |
    tr '\n' ' ' |
    sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" |
    head -n 1
}

extract_json_number() {
  key="$1"
  printf '%s' "$payload" |
    tr '\n' ' ' |
    sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\([0-9-][0-9-]*\).*/\1/p" |
    head -n 1
}

timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null)
tool=$(extract_json_string "tool_name")
[ -z "$tool" ] && tool="Bash"
command=$(extract_json_string "command" | cut -c 1-200)
exit_code=$(extract_json_number "exit_code")
[ -z "$exit_code" ] && exit_code=1

success=false
[ "$exit_code" = "0" ] && success=true

escaped_command=$(printf '%s' "$command" | sed 's/\\/\\\\/g; s/"/\\"/g')
escaped_tool=$(printf '%s' "$tool" | sed 's/\\/\\\\/g; s/"/\\"/g')

printf '{"timestamp":"%s","event":"PostToolUse","tool":"%s","command":"%s","exit_code":%s,"success":%s}\n' \
  "$timestamp" "$escaped_tool" "$escaped_command" "$exit_code" "$success" >> "$log_file"

exit 0
