#!/usr/bin/env node
import { appendText, ensureDir, getNumber, getString, logPath, parsePayload, readStdin } from "./common.mjs";
import { dirname } from "node:path";

const raw = await readStdin();
const payload = parsePayload(raw);
const logFile = logPath("activity.jsonl");

ensureDir(dirname(logFile));

const timestamp = new Date().toISOString().replace(/\.\d{3}Z$/, "Z");
const tool = getString(payload, "tool_name") || "Bash";
const command = getString(payload, "command").slice(0, 200);
const exitCode = getNumber(payload, "exit_code") ?? 1;

appendText(logFile, `${JSON.stringify({
  timestamp,
  event: "PostToolUse",
  tool,
  command,
  exit_code: exitCode,
  success: exitCode === 0
})}\n`);

