import { appendFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { basename, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const hookDir = dirname(fileURLToPath(import.meta.url));
export const projectRoot = dirname(dirname(hookDir));

export async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) {
    chunks.push(Buffer.from(chunk));
  }

  return Buffer.concat(chunks).toString("utf8");
}

export function parsePayload(raw) {
  const text = raw.replace(/^\uFEFF/, "").trim();

  if (!text) {
    return {};
  }

  try {
    return JSON.parse(text);
  } catch {
    return {};
  }
}

export function findValue(value, key) {
  if (value === null || value === undefined) {
    return undefined;
  }

  if (Array.isArray(value)) {
    for (const item of value) {
      const found = findValue(item, key);
      if (found !== undefined) {
        return found;
      }
    }
    return undefined;
  }

  if (typeof value === "object") {
    if (Object.prototype.hasOwnProperty.call(value, key)) {
      return value[key];
    }

    for (const item of Object.values(value)) {
      const found = findValue(item, key);
      if (found !== undefined) {
        return found;
      }
    }
  }

  return undefined;
}

export function getString(payload, ...keys) {
  for (const key of keys) {
    const value = findValue(payload, key);
    if (value !== undefined && value !== null) {
      return String(value);
    }
  }

  return "";
}

export function getNumber(payload, ...keys) {
  for (const key of keys) {
    const value = findValue(payload, key);
    if (value !== undefined && value !== null && !Number.isNaN(Number(value))) {
      return Number(value);
    }
  }

  return undefined;
}

export function outputBlock(reason) {
  process.stdout.write(`${JSON.stringify({ decision: "block", reason })}\n`);
}

export function outputPromptContext(additionalContext) {
  process.stdout.write(`${JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "UserPromptSubmit",
      additionalContext
    }
  })}\n`);
}

export function printStderr(message) {
  process.stderr.write(`${message}\n`);
}

export function ensureDir(path) {
  mkdirSync(path, { recursive: true });
}

export function appendText(path, value) {
  appendFileSync(path, value, "utf8");
}

export function writeText(path, value) {
  writeFileSync(path, value, "utf8");
}

export function readText(path) {
  return readFileSync(path, "utf8");
}

export function fileExists(path) {
  return existsSync(path);
}

export function removeFile(path) {
  rmSync(path, { force: true });
}

export function baseName(path) {
  return basename(path);
}

export function logPath(fileName) {
  return join(projectRoot, "Codex", "logs", fileName);
}

export function escapeJsonText(value) {
  return String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}
