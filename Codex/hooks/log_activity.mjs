#!/usr/bin/env node
import { appendText, ensureDir, getNumber, getString, logPath, parsePayload, readStdin } from "./common.mjs";
import { dirname } from "node:path";

const raw = await readStdin();
const payload = parsePayload(raw);
const logFile = logPath("activity.jsonl");
const redacted = "[REDACTED]";

ensureDir(dirname(logFile));

function sanitizeCommand(command) {
  return String(command)
    .replace(/-----BEGIN (?:RSA |EC )?PRIVATE KEY-----[\s\S]*?(?:-----END (?:RSA |EC )?PRIVATE KEY-----|$)/g, redacted)
    .replace(/\b(Bearer\s+)[A-Za-z0-9._~+/-]+=*/gi, `$1${redacted}`)
    .replace(/((?:password|passwd|pwd|appPassword|smtpPassword|apiKey|secretKey|jwtSecret|tokenSecret|signingKey|accessToken|refreshToken|token)\s*[:=]\s*)(?:"[^"]*"|'[^']*'|[^\s;&|]+)/gi, `$1${redacted}`)
    .replace(/((?:--|-|\/)(?:password|passwd|pwd|app-password|smtp-password|api-key|secret-key|jwt-secret|token-secret|signing-key|access-token|refresh-token|token)\b(?:\s+|=))(?:"[^"]*"|'[^']*'|[^\s;&|]+)/gi, `$1${redacted}`)
    .replace(/(\buser-secrets\s+set\s+(?:"[^"]*(?:password|secret|token|key)[^"]*"|'[^']*(?:password|secret|token|key)[^']*'|[^\s"']*(?:password|secret|token|key)[^\s"']*)\s+)(?:"[^"]*"|'[^']*'|[^\s;&|]+)/gi, `$1${redacted}`);
}

const timestamp = new Date().toISOString().replace(/\.\d{3}Z$/, "Z");
const tool = getString(payload, "tool_name") || "Bash";
const command = sanitizeCommand(getString(payload, "command")).slice(0, 200);
const exitCode = getNumber(payload, "exit_code") ?? 1;

appendText(logFile, `${JSON.stringify({
  timestamp,
  event: "PostToolUse",
  tool,
  command,
  exit_code: exitCode,
  success: exitCode === 0
})}\n`);
