#!/usr/bin/env sh
# UserPromptSubmit hook - *
# Injects compact project context based on prompt keywords.

PATH="/usr/bin:/bin:$PATH"
export PATH

payload=$(cat)
prompt=$(printf '%s' "$payload" |
  tr '\n' ' ' |
  sed -n 's/.*"prompt"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' |
  head -n 1)

[ -z "$prompt" ] && exit 0

lower=$(printf '%s' "$prompt" | tr '[:upper:]' '[:lower:]')
snippets=""

add_snippet() {
  snippet="$1"
  case "$snippets" in
    *"$snippet"*) ;;
    "")
      snippets="$snippet"
      ;;
    *)
      snippets="$snippets\\n$snippet"
      ;;
  esac
}

case "$lower" in
  *register*|*login*|*logout*|*auth*|*password*|*email*|*verification*|*verify*|*smtp*|*mailkit*)
    add_snippet "[context] Auth rules: BCrypt password hashing required. Exception messages must be Korean 합쇼체 with period, no dynamic data. Email code: 6-digit, 5-min expiry, single-use. Session cookie: HttpOnly + Secure + SameSite=Strict."
    ;;
esac

case "$lower" in
  *secret*|*appsettings*|*connection*|*password*|*credential*|*key*|*env*)
    add_snippet "[context] Security rules: Never hardcode secrets. Use User Secrets (dev) or environment variables (prod). DB password must not appear in appsettings.json."
    ;;
esac

case "$lower" in
  *migration*|*efcore*|*dbcontext*|*entity*|*repository*|*database*|*postgres*)
    add_snippet "[context] EF Core rules: DbContext used only inside Repository. Use Eager Loading (.Include) to prevent N+1. Migration files must be committed. Column names: snake_case via FluentAPI. Load migration-guide and backup-guide before DB-impacting work."
    ;;
esac

case "$lower" in
  *commit*|*git*|*pr*|*pull\ request*)
    add_snippet "[context] Commit/PR rules: type(scope): 한국어설명 (no period). One logical change per commit. Do not push or open PRs without explicit approval. PRs with DB impact need backup and rollback notes."
    ;;
esac

case "$lower" in
  *score*|*ranking*|*leaderboard*|*point*)
    add_snippet "[context] Score rules: best score per user for ranking. Ranking query must use GroupBy + Max — avoid N+1 with .Include."
    ;;
esac

case "$lower" in
  *solid*|*service*|*controller*|*interface*|*inject*|*dependency*)
    add_snippet "[context] Architecture rules: Controller → Service (interface) → Repository (interface). No business logic in Controller or Repository. Constructor injection only. All async methods need Async suffix."
    ;;
esac

[ -z "$snippets" ] && exit 0

printf '{"hookSpecificOutput":{"hookEventName":"UserPromptSubmit","additionalContext":"\\n\\n---\\n%b"}}\n' "$snippets"
exit 0
