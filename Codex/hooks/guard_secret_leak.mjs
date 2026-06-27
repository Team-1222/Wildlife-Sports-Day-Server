#!/usr/bin/env node
import { getString, outputBlock, parsePayload, readStdin } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const filePath = getString(payload, "file_path", "path");
const violations = [];

const hasSafePlaceholder = /your-password-here|changeme|your-app-password|<password>|<secret>|your-email@gmail\.com/i.test(raw);

function addViolation(message) {
  violations.push(`  - ${message}`);
}

if (!hasSafePlaceholder) {
  if (/Password\s*=\s*["']?[A-Za-z0-9@#$%^&*!_-]{4,}/i.test(raw)) {
    addViolation(`hardcoded DB password in connection string in ${filePath}`);
  }

  if (/(AppPassword|SmtpPassword|ApiKey|SecretKey)\s*[:=]\s*["'][^"']{4,}["']/i.test(raw)) {
    addViolation(`hardcoded credential value in ${filePath}`);
  }

  if (/[a-z]{4}\s[a-z]{4}\s[a-z]{4}\s[a-z]{4}/i.test(raw)) {
    addViolation(`possible Gmail app password in ${filePath}`);
  }

  if (/(JwtSecret|TokenSecret|SigningKey)\s*[:=]\s*["'][^"']{8,}["']/i.test(raw)) {
    addViolation(`hardcoded JWT secret in ${filePath}`);
  }

  if (/-----BEGIN (RSA |EC )?PRIVATE KEY-----/.test(raw)) {
    addViolation(`private key material in ${filePath}`);
  }
}

if (/(^|[\\/])(appsettings(?:\.[^.\\/]+)?\.json|\.env)$/i.test(filePath) && /"Password"\s*:\s*"[^"]{1,}"/.test(raw)) {
  addViolation(`non-empty Password field found in sensitive config file: ${filePath}`);
}

if (violations.length > 0) {
  outputBlock(`[secret-guard] Blocked: potential hardcoded secret detected.\n\nViolations:\n${violations.join("\n")}\n\nUse User Secrets or environment variables instead:\n  dotnet user-secrets set "Gmail:AppPassword" "your-value"\n  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=..."`);
}

