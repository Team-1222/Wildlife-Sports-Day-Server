#!/usr/bin/env node
import { getString, outputBlock, parsePayload, readStdin } from "./common.mjs";

const raw = await readStdin();
const payload = parsePayload(raw);
const command = getString(payload, "command");
if (!command.trim()) {
  outputBlock("[security-hook] Blocked shell tool call because the hook payload did not include a non-empty command.");
  process.exit(0);
}

const commandLower = command.toLowerCase();

const blockedPatterns = [
  ["drop database", /\bdrop\s+database\b/i],
  ["drop table", /\bdrop\s+table\b/i],
  ["delete from", /\bdelete\s+from\b/i],
  ["truncate", /\btruncate\b/i],
  ["rm -rf sensitive target", /\brm\s+-[a-z]*r[a-z]*f[a-z]*\s+(\/|~|\.)(?:\s|$)/i],
  ["rmdir /s", /\brmdir\s+\/s\b/i],
  ["cat appsettings", /\b(cat|type|get-content)\b[^\n\r]*(appsettings|\.env)/i],
  ["echo environment variable", /\becho\s+[$%][A-Za-z_][A-Za-z0-9_]*%?/i],
  ["printenv", /\bprintenv\b/i],
  ["env pipe", /\benv\s*\|/i],
  ["kill -9", /\bkill\s+-9\b/i],
  ["shutdown", /\bshutdown\b/i],
  ["reboot", /\breboot\b/i],
  ["format drive", /(^|[;&|]\s*)format(?:\.com|\.exe)?\s+[a-z]:/i],
  ["git push --force", /\bgit\s+push\b[^\n\r]*(--force|-f)\b/i],
  ["git reset --hard", /\bgit\s+reset\s+--hard\b/i],
  ["git rebase -i", /\bgit\s+rebase\s+-i\b/i]
];

const warnPatterns = [
  ["dotnet ef database drop", /\bdotnet\s+ef\s+database\s+drop\b/i],
  ["dotnet ef migrations remove", /\bdotnet\s+ef\s+migrations\s+remove\b/i],
  ["git clean -fd", /\bgit\s+clean\b[^\n\r]*-[a-z]*f[a-z]*d[a-z]*/i]
];

for (const [label, pattern] of blockedPatterns) {
  if (pattern.test(commandLower)) {
    outputBlock(`[security-hook] Blocked dangerous command matching pattern: '${label}'\nCommand: ${command}\nIf this is intentional, run it manually in the terminal.`);
    process.exit(0);
  }
}

for (const [label, pattern] of warnPatterns) {
  if (pattern.test(commandLower)) {
    process.stderr.write(`[security-hook] Warning: destructive command detected: '${label}'. Proceeding.\n`);
    process.exit(0);
  }
}
