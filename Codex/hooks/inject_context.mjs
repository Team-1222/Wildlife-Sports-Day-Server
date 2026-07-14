#!/usr/bin/env node
import { dirname } from "node:path";
import { ensureDir, getString, logPath, outputPromptContext, parsePayload, readStdin, writeText } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const prompt = getString(payload, "prompt");

if (!prompt) {
  process.exit(0);
}

const lower = prompt.toLowerCase();
const snippets = [];
const commitIssueContextPath = logPath("commit_issue_context.json");

function addSnippet(snippet) {
  if (!snippets.includes(snippet)) {
    snippets.push(snippet);
  }
}

if (/(register|login|logout|auth|password|email|verification|verify|smtp|mailkit|회원가입|로그인|로그아웃|인증|비밀번호|이메일|검증|확인|메일)/i.test(lower)) {
  addSnippet("[context] Auth rules: BCrypt password hashing required. Exception messages must be Korean 합쇼체 with period, no dynamic data. Email code: 6-digit, 5-min expiry, single-use. Session cookie: HttpOnly + Secure + SameSite=Strict.");
}

if (/(secret|appsettings|connection|password|credential|key|env|시크릿|비밀|설정|연결문자열|연결 문자열|자격증명|키|환경변수|환경 변수|비밀번호)/i.test(lower)) {
  addSnippet("[context] Security rules: Never hardcode secrets. Use User Secrets (dev) or environment variables (prod). DB password must not appear in appsettings.json.");
}

if (/(migration|efcore|dbcontext|entity|repository|database|postgres|마이그레이션|엔티티|리포지토리|레포지토리|저장소|데이터베이스|디비|DB|포스트그레스)/i.test(lower)) {
  addSnippet("[context] EF Core rules: DbContext used only inside Repository. Use Eager Loading (.Include) to prevent N+1. Migration files must be committed. Column names: snake_case via FluentAPI. Load migration-guide and backup-guide before DB-impacting work.");
}

if (/(commit|git|pr|pull request|커밋|깃|풀리퀘스트|풀 리퀘스트|피알|PR)/i.test(lower)) {
  addSnippet("[context] Commit/PR rules: commit title is type: 한국어설명 (no period, no scope). If a related issue exists, first body line is #<issue-number>. One logical change per commit. Do not push or open PRs without explicit approval. PRs with DB impact need backup and rollback notes.");

  const issueMatch = prompt.match(/#\d+/);
  const noIssueConfirmed = /(관련\s*)?이슈\s*(없|없어|없음)|이슈\s*번호\s*(없|없어|없음)|no\s+(related\s+)?issue|without\s+issue/i.test(prompt);
  const context = {
    timestamp: new Date().toISOString(),
    issueRef: issueMatch?.[0] ?? null,
    noIssueConfirmed,
    needsIssueConfirmation: !issueMatch && !noIssueConfirmed
  };

  ensureDir(dirname(commitIssueContextPath));
  writeText(commitIssueContextPath, `${JSON.stringify(context, null, 2)}\n`);

  if (issueMatch) {
    addSnippet(`[context] Commit issue/PR reference for this prompt: ${issueMatch[0]}. Use this exact body reference for commits in this turn; do not reuse issue refs from earlier turns.`);
  } else if (noIssueConfirmed) {
    addSnippet("[context] The current prompt says there is no related issue. Do not add an issue reference to commit bodies.");
  } else {
    addSnippet("[context] Commit issue/PR reference is missing from this prompt. Ask the user before committing; do not reuse issue refs from earlier turns.");
  }
}

if (/(score|ranking|leaderboard|point|점수|랭킹|순위|리더보드|포인트)/i.test(lower)) {
  addSnippet("[context] Score rules: best score per user for ranking. Ranking query must use GroupBy + Max - avoid N+1 with .Include.");
}

if (/(solid|service|controller|interface|inject|dependency|서비스|컨트롤러|인터페이스|주입|의존성|아키텍처|구조|계층)/i.test(lower)) {
  addSnippet("[context] Architecture rules: Controller -> Service (interface) -> Repository (interface). No business logic in Controller or Repository. Constructor injection only. All async methods need Async suffix.");
}

if (snippets.length > 0) {
  outputPromptContext(`\n\n---\n${snippets.join("\n")}`);
}
