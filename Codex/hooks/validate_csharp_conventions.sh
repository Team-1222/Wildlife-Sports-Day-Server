#!/usr/bin/env sh
# PostToolUse hook - Write | Edit
# Validates C# content against project conventions.

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

file_path=$(extract_json_string "file_path")
[ -z "$file_path" ] && file_path=$(extract_json_string "path")

case "$file_path" in
  *.cs) ;;
  *) exit 0 ;;
esac

errors=""
warnings=""

add_error() {
  if [ -z "$errors" ]; then
    errors="  - $1"
  else
    errors="$errors\\n  - $1"
  fi
}

add_warning() {
  if [ -z "$warnings" ]; then
    warnings="  - $1"
  else
    warnings="$warnings\\n  - $1"
  fi
}

printf '%s' "$payload" | grep -Eq '\basync[[:space:]]+void\b' &&
  add_error "async void detected - use async Task instead"

printf '%s' "$payload" | grep -Eq '\.(Result|Wait)[[:space:]]*[( ]' &&
  add_error ".Result / .Wait() blocking call detected - use await instead"

printf '%s' "$payload" | grep -Eq 'new[[:space:]]+AppException[[:space:]]*\([[:space:]]*\$"[^"]*\{' &&
  add_error "AppException message contains dynamic data - use static Korean message only"

printf '%s' "$payload" | grep -Eq '(private|readonly)[[:space:]]+[A-Za-z0-9_]*DbContext[A-Za-z0-9_]*[[:space:]]+_[A-Za-z0-9_]+' &&
  add_warning "DbContext injected directly - inject a Service instead"

printf '%s' "$payload" | grep -Eq 'public[[:space:]]+async[[:space:]]+Task[^()]*[[:space:]]+[A-Za-z0-9_]*[[:space:]]*\(' &&
  printf '%s' "$payload" | grep -Evq 'public[[:space:]]+async[[:space:]]+Task[^()]*Async[[:space:]]*\(' &&
  add_warning "Async method may be missing Async suffix"

printf '%s' "$payload" | grep -Eq '_logger\.[A-Za-z]+\([[:space:]]*\$"' &&
  add_warning "Logger uses string interpolation - use structured logging with placeholders"

printf '%s' "$payload" | grep -Eq '_logger\.[A-Za-z]+\([[:space:]]*"[^"]*[가-힣]' &&
  add_warning "Logger message contains Korean - use English verb-led sentences"

[ -z "$errors" ] && [ -z "$warnings" ] && exit 0

short_path=$(basename "$file_path")
printf '[convention-hook] %s\n' "$short_path" >&2

if [ -n "$errors" ]; then
  printf '\nErrors - must fix:\n%b\n' "$errors" >&2
fi

if [ -n "$warnings" ]; then
  printf '\nWarnings - should fix:\n%b\n' "$warnings" >&2
fi

[ -n "$errors" ] && exit 2
exit 0
