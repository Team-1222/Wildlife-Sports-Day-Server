#!/usr/bin/env node
import { getString, listChangedFiles, outputBlock, parsePayload, readStdin, readTextIfSmall, resolveProjectFile } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const filePath = getString(payload, "file_path", "path");
const shouldScanChangedFiles = process.argv.includes("--scan-changed");
const violations = [];
const fileTextKeys = new Set([
  "content",
  "file_content",
  "fileContent",
  "new_string",
  "newString",
  "text"
]);

function addViolation(message) {
  violations.push(`  - ${message}`);
}

function scanContent(content, sourcePath) {
  const hasSafePlaceholder = /your-password-here|changeme|your-app-password|<password>|<secret>|your-email@gmail\.com/i.test(content);

  if (!hasSafePlaceholder) {
    if (/["']?Password["']?\s*[:=]\s*["']?[A-Za-z0-9@#$%^&*!_-]{4,}["']?/i.test(content)) {
      addViolation(`hardcoded DB password in connection string in ${sourcePath}`);
    }

    if (/["']?(AppPassword|SmtpPassword|ApiKey|SecretKey)["']?\s*[:=]\s*["'][^"']{4,}["']/i.test(content)) {
      addViolation(`hardcoded credential value in ${sourcePath}`);
    }

    if (/[a-z]{4}\s[a-z]{4}\s[a-z]{4}\s[a-z]{4}/i.test(content)) {
      addViolation(`possible Gmail app password in ${sourcePath}`);
    }

    if (/["']?(JwtSecret|TokenSecret|SigningKey)["']?\s*[:=]\s*["'][^"']{8,}["']/i.test(content)) {
      addViolation(`hardcoded JWT secret in ${sourcePath}`);
    }

    if (/-----BEGIN (RSA |EC )?PRIVATE KEY-----/.test(content)) {
      addViolation(`private key material in ${sourcePath}`);
    }
  }

  if (/(^|[\\/])(appsettings(?:\.[^.\\/]+)?\.json|\.env)$/i.test(sourcePath) && /"Password"\s*:\s*"[^"]{1,}"/.test(content)) {
    addViolation(`non-empty Password field found in sensitive config file: ${sourcePath}`);
  }
}

function collectFileTexts(value, texts = []) {
  if (value === null || value === undefined) {
    return texts;
  }

  if (Array.isArray(value)) {
    for (const item of value) {
      collectFileTexts(item, texts);
    }
    return texts;
  }

  if (typeof value === "object") {
    for (const [key, nestedValue] of Object.entries(value)) {
      if (fileTextKeys.has(key) && typeof nestedValue === "string") {
        texts.push(nestedValue);
      } else {
        collectFileTexts(nestedValue, texts);
      }
    }
  }

  return texts;
}

const payloadFileTexts = collectFileTexts(payload);
for (const fileText of payloadFileTexts) {
  scanContent(fileText, filePath || "hook file content");
}

if (shouldScanChangedFiles) {
  for (const changedFile of listChangedFiles()) {
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

if (violations.length > 0) {
  outputBlock(`[secret-guard] Blocked: potential hardcoded secret detected.\n\nViolations:\n${violations.join("\n")}\n\nUse User Secrets or environment variables instead:\n  dotnet user-secrets set "Gmail:AppPassword" "your-value"\n  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=..."`);
}
