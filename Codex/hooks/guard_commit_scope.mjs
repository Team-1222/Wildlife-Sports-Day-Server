#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import { fileExists, getString, logPath, outputBlock, parsePayload, projectRoot, readStdin, readText } from "./common.mjs";
import { commitIssueContextFileName } from "./commit_issue_context.mjs";

const commitSplitPolicy = [
  "Split commits by implementation work unit and by individual code-review finding.",
  "Keep one independently reviewable finding per commit; do not combine distinct findings merely because they touch related files."
];

const raw = await readStdin();
const payload = parsePayload(raw);
const command = getString(payload, "command");

if (!/\bgit\s+commit\b/i.test(command)) {
  process.exit(0);
}

function extractCommitMessages(commandText) {
  const messages = [];
  const pattern = /(?:^|\s)(?:-m|--message)(?:=|\s+)(?:"([^"]*)"|'([^']*)'|([^\s]+))/g;
  let match;
  while ((match = pattern.exec(commandText)) !== null) {
    messages.push(match[1] ?? match[2] ?? match[3] ?? "");
  }

  return messages;
}

function latestIssueContext() {
  const sessionId = getString(payload, "session_id") || null;
  const paths = [...new Set([
    logPath(commitIssueContextFileName(sessionId)),
    logPath("commit_issue_context.json")
  ])];
  for (const path of paths) {
    try {
      if (fileExists(path)) {
        const context = JSON.parse(readText(path));
        if (!sessionId || context.sessionId === sessionId) {
          return context;
        }
      }
    } catch {
      // 다른 작업의 문맥을 적용하지 않고 현재 작업의 이전 문맥을 확인합니다.
    }
  }
  return undefined;
}

function issueRefsFrom(messages) {
  return messages.flatMap((message) => message.match(/#\d+/g) ?? []);
}

const commitMessages = extractCommitMessages(command);
const commitIssueRefs = issueRefsFrom(commitMessages.slice(1));
const issueContext = latestIssueContext();
const expectedIssueRefs = issueContext?.issueRefs ?? (issueContext?.issueRef ? [issueContext.issueRef] : []);

if (expectedIssueRefs.length > 0) {
  const distinctRefs = [...new Set(commitIssueRefs)];
  if (distinctRefs.length !== 1 || !expectedIssueRefs.includes(distinctRefs[0])) {
    outputBlock([
      "[commit-issue-hook] Commit body issue reference does not match the latest user-provided issue reference.",
      `Expected exactly one of: ${expectedIssueRefs.join(", ")}`,
      `Found: ${commitIssueRefs.length > 0 ? commitIssueRefs.join(", ") : "(none)"}`,
      "Ask the user before committing if the issue/PR number is unclear."
    ].join("\n"));
    process.exit(0);
  }

} else if (issueContext?.noIssueConfirmed) {
  if (commitIssueRefs.length > 0) {
    outputBlock([
      "[commit-issue-hook] The latest user prompt confirmed no related issue, but the commit body includes an issue reference.",
      `Found: ${commitIssueRefs.join(", ")}`,
      "Remove the issue reference or ask the user again."
    ].join("\n"));
    process.exit(0);
  }
} else if (issueContext?.needsIssueConfirmation) {
  outputBlock([
    "[commit-issue-hook] The latest commit request did not include an issue/PR number.",
    "Ask the user for the issue/PR number, or confirm that there is no related issue, before committing."
  ].join("\n"));
  process.exit(0);
}

function stagedFiles() {
  try {
    const output = execFileSync("git", ["diff", "--cached", "--name-only", "--diff-filter=ACMRTUXB"], {
      cwd: projectRoot,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"]
    });

    return output.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  } catch {
    return [];
  }
}

function categoryOf(file) {
  const normalized = file.replace(/\\/g, "/");

  if (normalized.startsWith("Codex/") || normalized.startsWith(".codex/") || normalized === "AGENTS.md") {
    return "codex-workflow";
  }

  if (normalized.includes(".Tests/")) {
    return "tests";
  }

  if (normalized.includes("/Migrations/")) {
    return "migrations";
  }

  if (/(^|\/)(Entities|Infrastructure\/Configurations)\//.test(normalized)
    || normalized.endsWith("/Infrastructure/AppDbContext.cs")) {
    return "data-model";
  }

  if (/(^|\/)(Controllers|Services|Repositories|Dtos|Middleware)\//.test(normalized)
    || normalized.endsWith("/Program.cs")
    || normalized.endsWith(".http")) {
    return "app-behavior";
  }

  if (normalized.endsWith(".csproj")
    || normalized.endsWith(".sln")
    || normalized === ".gitignore"
    || normalized.startsWith(".github/")) {
    return "project-config";
  }

  if (normalized.toLowerCase().endsWith(".md")) {
    return "docs";
  }

  return "other";
}

const files = stagedFiles();
if (files.length === 0) {
  process.exit(0);
}

const categories = new Map();
for (const file of files) {
  const category = categoryOf(file);
  if (!categories.has(category)) {
    categories.set(category, []);
  }
  categories.get(category).push(file);
}

const categoryNames = [...categories.keys()];
const hasCodexWorkflow = categories.has("codex-workflow");
const hasNonCodexWorkflow = categoryNames.some((category) => category !== "codex-workflow");

if (hasCodexWorkflow && hasNonCodexWorkflow) {
  outputBlock([
    "[commit-scope-hook] Codex workflow changes are staged together with app changes.",
    "Split Codex skill/hook/policy updates into their own commit.",
    ...commitSplitPolicy,
    "",
    "Staged categories:",
    ...categoryNames.map((category) => `  - ${category}: ${categories.get(category).length} file(s)`)
  ].join("\n"));
  process.exit(0);
}

const broadCategories = categoryNames.filter((category) => category !== "project-config");
if (files.length >= 12 && broadCategories.length >= 3) {
  outputBlock([
    "[commit-scope-hook] Staged changes look like a broad all-in-one commit.",
    ...commitSplitPolicy,
    "Implementation work units include data model/migrations, app behavior, tests, docs, and workflow changes.",
    "",
    "Staged categories:",
    ...categoryNames.map((category) => `  - ${category}: ${categories.get(category).length} file(s)`)
  ].join("\n"));
  process.exit(0);
}
