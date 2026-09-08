#!/usr/bin/env node
import { dirname } from "node:path";
import { ensureDir, fileExists, getString, logPath, outputPromptContext, parsePayload, readStdin, readTextIfSmall, writeText } from "./common.mjs";
import { resolveCommitIssueContext } from "./commit_issue_context.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const prompt = getString(payload, "prompt");

if (!prompt) {
  process.exit(0);
}

const lower = prompt.toLowerCase();
const snippets = [];
const commitIssueContextPath = logPath("commit_issue_context.json");
let previousCommitContext = null;
try {
  if (fileExists(commitIssueContextPath)) {
    previousCommitContext = parsePayload(readTextIfSmall(commitIssueContextPath) ?? "");
  }
} catch {
  // 문맥 파일을 읽지 못해도 새 커밋 요청의 이슈 확인은 계속합니다.
}
const commitContext = resolveCommitIssueContext(prompt, previousCommitContext, getString(payload, "session_id") || null);

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

if (commitContext) {
  addSnippet("[context] Commit/PR rules: commit title is type: 한국어설명 (no period, no scope). If a related issue exists, first body line is #<issue-number>. Split commits by implementation work unit and, when addressing review feedback, by each individual code-review finding. One independently reviewable finding per commit; never combine distinct findings merely because they touch related files. Do not push or open PRs without explicit approval. PRs with DB impact need backup and rollback notes.");

  ensureDir(dirname(commitIssueContextPath));
  writeText(commitIssueContextPath, `${JSON.stringify(commitContext, null, 2)}\n`);

  if (commitContext.issueRef) {
    addSnippet(`[context] Commit issue/PR reference for this prompt: ${commitContext.issueRef}. Use this exact body reference for commits in this turn; do not reuse issue refs from earlier turns.`);
  } else if (commitContext.noIssueConfirmed) {
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
