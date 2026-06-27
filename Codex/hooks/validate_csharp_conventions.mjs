#!/usr/bin/env node
import { baseName, getString, listChangedFiles, parsePayload, printStderr, readStdin, readTextIfSmall, resolveProjectFile } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const filePath = getString(payload, "file_path", "path");
const shouldScanChangedFiles = process.argv.includes("--scan-changed");

const errors = [];
const warnings = [];

function addError(sourcePath, message) {
  errors.push(`  - ${sourcePath}: ${message}`);
}

function addWarning(sourcePath, message) {
  warnings.push(`  - ${sourcePath}: ${message}`);
}

function scanContent(content, sourcePath) {
  if (/\basync\s+void\b/.test(content)) {
    addError(sourcePath, "async void detected - use async Task instead");
  }

  if (/\.(Result|Wait)\s*[( ]/.test(content)) {
    addError(sourcePath, ".Result / .Wait() blocking call detected - use await instead");
  }

  if (/new\s+AppException\s*\(\s*\$"[^"]*\{/.test(content)) {
    addError(sourcePath, "AppException message contains dynamic data - use static Korean message only");
  }

  if (/(private|readonly)\s+[A-Za-z0-9_]*DbContext[A-Za-z0-9_]*\s+_[A-Za-z0-9_]+/.test(content)) {
    addWarning(sourcePath, "DbContext injected directly - inject a Service instead");
  }

  const asyncMethodPattern = /\bpublic\s+async\s+Task(?:<[^>\r\n]+>)?\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(/g;
  for (const match of content.matchAll(asyncMethodPattern)) {
    if (!match[1].endsWith("Async")) {
      addWarning(sourcePath, `Async method '${match[1]}' may be missing Async suffix`);
    }
  }

  if (/_logger\.[A-Za-z]+\(\s*\$"/.test(content)) {
    addWarning(sourcePath, "Logger uses string interpolation - use structured logging with placeholders");
  }

  if (/_logger\.[A-Za-z]+\(\s*"[^"]*[가-힣]/.test(content)) {
    addWarning(sourcePath, "Logger message contains Korean - use English verb-led sentences");
  }
}

if (filePath) {
  if (!filePath.endsWith(".cs")) {
    if (!shouldScanChangedFiles) {
      process.exit(0);
    }
  } else {
    scanContent(raw, filePath);
  }
}

if (shouldScanChangedFiles) {
  for (const changedFile of listChangedFiles().filter((file) => file.endsWith(".cs"))) {
    const fullPath = resolveProjectFile(changedFile);
    if (!fullPath) {
      continue;
    }

    try {
      const content = readTextIfSmall(fullPath);
      if (content !== undefined) {
        scanContent(content, changedFile);
      }
    } catch {
      // Skip deleted, binary, unreadable, or concurrently changed files.
    }
  }
}

if (errors.length === 0 && warnings.length === 0) {
  process.exit(0);
}

const targetLabel = filePath && filePath.endsWith(".cs") ? baseName(filePath) : "changed C# files";
printStderr(`[convention-hook] ${targetLabel}`);

if (errors.length > 0) {
  printStderr(`\nErrors - must fix:\n${errors.join("\n")}`);
}

if (warnings.length > 0) {
  printStderr(`\nWarnings - should fix:\n${warnings.join("\n")}`);
}

process.exit(errors.length > 0 ? 2 : 0);
