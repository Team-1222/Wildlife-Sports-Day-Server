#!/usr/bin/env node
import { baseName, getString, parsePayload, printStderr, readStdin } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const filePath = getString(payload, "file_path", "path");

if (!filePath.endsWith(".cs")) {
  process.exit(0);
}

const errors = [];
const warnings = [];

function addError(message) {
  errors.push(`  - ${message}`);
}

function addWarning(message) {
  warnings.push(`  - ${message}`);
}

if (/\basync\s+void\b/.test(raw)) {
  addError("async void detected - use async Task instead");
}

if (/\.(Result|Wait)\s*[( ]/.test(raw)) {
  addError(".Result / .Wait() blocking call detected - use await instead");
}

if (/new\s+AppException\s*\(\s*\$"[^"]*\{/.test(raw)) {
  addError("AppException message contains dynamic data - use static Korean message only");
}

if (/(private|readonly)\s+[A-Za-z0-9_]*DbContext[A-Za-z0-9_]*\s+_[A-Za-z0-9_]+/.test(raw)) {
  addWarning("DbContext injected directly - inject a Service instead");
}

const asyncMethodPattern = /\bpublic\s+async\s+Task(?:<[^>\r\n]+>)?\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(/g;
for (const match of raw.matchAll(asyncMethodPattern)) {
  if (!match[1].endsWith("Async")) {
    addWarning(`Async method '${match[1]}' may be missing Async suffix`);
  }
}

if (/_logger\.[A-Za-z]+\(\s*\$"/.test(raw)) {
  addWarning("Logger uses string interpolation - use structured logging with placeholders");
}

if (/_logger\.[A-Za-z]+\(\s*"[^"]*[가-힣]/.test(raw)) {
  addWarning("Logger message contains Korean - use English verb-led sentences");
}

if (errors.length === 0 && warnings.length === 0) {
  process.exit(0);
}

printStderr(`[convention-hook] ${baseName(filePath)}`);

if (errors.length > 0) {
  printStderr(`\nErrors - must fix:\n${errors.join("\n")}`);
}

if (warnings.length > 0) {
  printStderr(`\nWarnings - should fix:\n${warnings.join("\n")}`);
}

process.exit(errors.length > 0 ? 2 : 0);

