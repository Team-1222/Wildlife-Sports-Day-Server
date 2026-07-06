#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import { getString, outputBlock, parsePayload, projectRoot, readStdin } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const command = getString(payload, "command");

if (!/\bgit\s+commit\b/i.test(command)) {
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
    "Split by logical unit before committing: data model/migrations, app behavior, tests, docs, and workflow changes.",
    "",
    "Staged categories:",
    ...categoryNames.map((category) => `  - ${category}: ${categories.get(category).length} file(s)`)
  ].join("\n"));
  process.exit(0);
}
