#!/usr/bin/env sh
# PreToolUse hook - Bash
# Blocks dangerous shell commands before execution.

PATH="/usr/bin:/bin:$PATH"
export PATH

payload=$(cat)

extract_json_string() {
  key="$1"
  printf '%s' "$payload" |
    tr '\n' ' ' |
    sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" |
    head -n 1
}

command=$(extract_json_string "command")
command_lower=$(printf '%s' "$command" | tr '[:upper:]' '[:lower:]')

blocked_patterns='
drop database
drop table
delete from
truncate
rm -rf /
rm -rf ~
rm -rf .
rmdir /s
cat appsettings
echo $
printenv
env |
kill -9
shutdown
reboot
format
git push --force
git push -f
git reset --hard HEAD~
git rebase -i
'

warn_patterns='
dotnet ef database drop
dotnet ef migrations remove
git clean -fd
'

printf '%s\n' "$blocked_patterns" | while IFS= read -r pattern; do
  [ -z "$pattern" ] && continue
  case "$command_lower" in
    *"$pattern"*)
      printf '{"decision":"block","reason":"[security-hook] Blocked dangerous command matching pattern: '\''%s'\''\\nCommand: %s\\nIf this is intentional, run it manually in the terminal."}\n' "$pattern" "$command"
      exit 100
      ;;
  esac
done

status=$?
if [ "$status" -eq 100 ]; then
  exit 0
fi

printf '%s\n' "$warn_patterns" | while IFS= read -r pattern; do
  [ -z "$pattern" ] && continue
  case "$command_lower" in
    *"$pattern"*)
      printf '%s\n' "[security-hook] Warning: destructive command detected: '$pattern'. Proceeding." >&2
      exit 0
      ;;
  esac
done

exit 0
