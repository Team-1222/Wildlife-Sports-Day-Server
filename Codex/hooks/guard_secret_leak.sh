#!/usr/bin/env sh
# PreToolUse hook - Write | Edit
# Blocks writes/edits that appear to contain hardcoded secrets.

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

violations=""

add_violation() {
  if [ -z "$violations" ]; then
    violations="  - $1"
  else
    violations="$violations\\n  - $1"
  fi
}

is_safe_payload() {
  printf '%s' "$payload" | grep -Eiq 'your-password-here|changeme|your-app-password|<password>|<secret>|your-email@gmail\.com'
}

if ! is_safe_payload; then
  printf '%s' "$payload" | grep -Eiq 'Password[[:space:]]*=[[:space:]]*["'\'']?[A-Za-z0-9@#$%^&*!_-]{4,}' &&
    add_violation "hardcoded DB password in connection string in $file_path"

  printf '%s' "$payload" | grep -Eiq '(AppPassword|SmtpPassword|ApiKey|SecretKey)[[:space:]]*[:=][[:space:]]*["'\''][^"'\'']{4,}["'\'']' &&
    add_violation "hardcoded credential value in $file_path"

  printf '%s' "$payload" | grep -Eiq '[a-z]{4}[[:space:]][a-z]{4}[[:space:]][a-z]{4}[[:space:]][a-z]{4}' &&
    add_violation "possible Gmail app password in $file_path"

  printf '%s' "$payload" | grep -Eiq '(JwtSecret|TokenSecret|SigningKey)[[:space:]]*[:=][[:space:]]*["'\''][^"'\'']{8,}["'\'']' &&
    add_violation "hardcoded JWT secret in $file_path"

  printf '%s' "$payload" | grep -Eq -- '-----BEGIN (RSA |EC )?PRIVATE KEY-----' &&
    add_violation "private key material in $file_path"
fi

case "$file_path" in
  *appsettings.json*|*appsettings.Production.json*|*appsettings.Staging.json*|*.env*)
    printf '%s' "$payload" | grep -Eq '"Password"[[:space:]]*:[[:space:]]*"[^"]{1,}"' &&
      add_violation "non-empty Password field found in sensitive config file: $file_path"
    ;;
esac

if [ -n "$violations" ]; then
  printf '{"decision":"block","reason":"[secret-guard] Blocked: potential hardcoded secret detected.\\n\\nViolations:\\n%b\\n\\nUse User Secrets or environment variables instead:\\n  dotnet user-secrets set \\\"Gmail:AppPassword\\\" \\\"your-value\\\"\\n  dotnet user-secrets set \\\"ConnectionStrings:DefaultConnection\\\" \\\"Host=...;Password=...\\\""}\n' "$violations"
fi

exit 0
