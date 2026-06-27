#!/usr/bin/env node
import { getString, outputPromptContext, parsePayload, readStdin } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const prompt = getString(payload, "prompt");

if (!prompt) {
  process.exit(0);
}

const lower = prompt.toLowerCase();
const snippets = [];

function addSnippet(snippet) {
  if (!snippets.includes(snippet)) {
    snippets.push(snippet);
  }
}

if (/(register|login|logout|auth|password|email|verification|verify|smtp|mailkit)/i.test(lower)) {
  addSnippet("[context] Auth rules: BCrypt password hashing required. Exception messages must be Korean 합쇼체 with period, no dynamic data. Email code: 6-digit, 5-min expiry, single-use. Session cookie: HttpOnly + Secure + SameSite=Strict.");
}

if (/(secret|appsettings|connection|password|credential|key|env)/i.test(lower)) {
  addSnippet("[context] Security rules: Never hardcode secrets. Use User Secrets (dev) or environment variables (prod). DB password must not appear in appsettings.json.");
}

if (/(migration|efcore|dbcontext|entity|repository|database|postgres)/i.test(lower)) {
  addSnippet("[context] EF Core rules: DbContext used only inside Repository. Use Eager Loading (.Include) to prevent N+1. Migration files must be committed. Column names: snake_case via FluentAPI. Load migration-guide and backup-guide before DB-impacting work.");
}

if (/(commit|git|pr|pull request)/i.test(lower)) {
  addSnippet("[context] Commit/PR rules: commit title is type: 한국어설명 (no period, no scope). If a related issue exists, first body line is #<issue-number>. One logical change per commit. Do not push or open PRs without explicit approval. PRs with DB impact need backup and rollback notes.");
}

if (/(score|ranking|leaderboard|point)/i.test(lower)) {
  addSnippet("[context] Score rules: best score per user for ranking. Ranking query must use GroupBy + Max - avoid N+1 with .Include.");
}

if (/(solid|service|controller|interface|inject|dependency)/i.test(lower)) {
  addSnippet("[context] Architecture rules: Controller -> Service (interface) -> Repository (interface). No business logic in Controller or Repository. Constructor injection only. All async methods need Async suffix.");
}

if (snippets.length > 0) {
  outputPromptContext(`\n\n---\n${snippets.join("\n")}`);
}

