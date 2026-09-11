#!/usr/bin/env node
import { fileExists, logPath, printStderr, readText } from "./common.mjs";

const logFile = logPath("activity.jsonl");

if (!fileExists(logFile)) {
  process.exit(0);
}

const lines = readText(logFile).split(/\r?\n/).filter(Boolean);
if (lines.length === 0) {
  process.exit(0);
}

const recent = lines.slice(-200);
const records = recent.map((line) => {
  try {
    return JSON.parse(line);
  } catch {
    return undefined;
  }
}).filter(Boolean);

const succeeded = records.filter((record) => record.success === true).length;
const failed = records.length - succeeded;
const commands = records.map((record) => String(record.command || "")).filter(Boolean).slice(-3);

printStderr("");
printStderr("==============================================");
printStderr("        Wildlife Survival - Session Summary");
printStderr("==============================================");
printStderr(`  Commands logged : ${records.length}`);
printStderr(`  Succeeded       : ${succeeded}`);
printStderr(`  Failed          : ${failed}`);

if (commands.length > 0) {
  printStderr("  Recent commands :");
  for (const command of commands) {
    printStderr(`    - ${command.slice(0, 50)}`);
  }
}

printStderr("");
