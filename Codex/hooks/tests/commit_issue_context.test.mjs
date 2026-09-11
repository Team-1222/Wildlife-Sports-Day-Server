import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { commitIssueContextFileName, resolveCommitIssueContext } from "../commit_issue_context.mjs";

test("이슈 번호만 답해도 확인 대기 상태를 해제합니다", () => {
  const pending = resolveCommitIssueContext("커밋해줘", null, "session-a");
  const answer = resolveCommitIssueContext("#13", pending, "session-a");
  assert.equal(answer.issueRef, "#13");
  assert.equal(answer.needsIssueConfirmation, false);
});

test("관련 이슈 없음 답변을 저장합니다", () => {
  const pending = resolveCommitIssueContext("커밋해줘");
  const answer = resolveCommitIssueContext("관련 이슈 없음", pending);
  assert.equal(answer.issueRef, null);
  assert.equal(answer.noIssueConfirmed, true);
  assert.equal(answer.needsIssueConfirmation, false);
});

test("확인 대기 중에는 숫자만 입력한 답변도 이슈 번호로 해석합니다", () => {
  const pending = resolveCommitIssueContext("커밋해줘");
  for (const prompt of ["13", "13번", "13번 이슈"])
    assert.equal(resolveCommitIssueContext(prompt, pending).issueRef, "#13");
  assert.equal(resolveCommitIssueContext("13"), null);
});

test("무관한 답변은 확인 대기를 덮어쓰지 않습니다", () => {
  const pending = resolveCommitIssueContext("커밋해줘");
  assert.equal(resolveCommitIssueContext("빌드 결과 알려줘", pending), null);
  assert.equal(resolveCommitIssueContext("#13 오류 설명해줘"), null);
});

test("다른 세션의 답변은 이전 세션의 확인을 완료하지 않습니다", () => {
  const pending = resolveCommitIssueContext("커밋해줘", null, "session-a");
  assert.equal(resolveCommitIssueContext("#13", pending, "session-b"), null);
});

test("새 커밋 요청은 이전 이슈 번호를 재사용하지 않습니다", () => {
  const previous = resolveCommitIssueContext("커밋해줘 #1");
  assert.equal(resolveCommitIssueContext("커밋해줘", previous).needsIssueConfirmation, true);
  assert.equal(resolveCommitIssueContext("커밋해줘 #13", previous).issueRef, "#13");
});

for (const [answer, body] of [["#13", "#13"], ["관련 이슈 없음", ""]]) {
  test(`실제 훅 파일이 후속 답변 '${answer}'을 저장하고 커밋 검사를 통과합니다`, () => {
    const fixture = mkdtempSync(join(tmpdir(), "wildlife-hook-test-"));
    const source = resolve(dirname(fileURLToPath(import.meta.url)), "..");
    const hooks = join(fixture, "Codex", "hooks");
    mkdirSync(hooks, { recursive: true });
    try {
      for (const file of ["common.mjs", "inject_context.mjs", "commit_issue_context.mjs", "guard_commit_scope.mjs"]) {
        copyFileSync(join(source, file), join(hooks, file));
      }
      const submit = prompt => execFileSync(process.execPath, [join(hooks, "inject_context.mjs")], {
        input: JSON.stringify({ prompt, session_id: "test-session" }), encoding: "utf8"
      });
      submit("커밋해줘");
      submit(answer);
      const stored = JSON.parse(readFileSync(join(fixture, "Codex", "logs", commitIssueContextFileName("test-session")), "utf8"));
      assert.equal(stored.needsIssueConfirmation, false);
      assert.equal(stored.issueRef, body || null);
      const command = `git commit -m 'chore: 테스트 훅 확인'${body ? ` -m '${body}'` : ""}`;
      const output = execFileSync(process.execPath, [join(hooks, "guard_commit_scope.mjs")], {
        input: JSON.stringify({ session_id: "test-session", tool_input: { command } }), encoding: "utf8"
      });
      assert.equal(output.trim(), "");
    } finally {
      const fixturePath = resolve(fixture);
      assert.ok(fixturePath.startsWith(`${resolve(tmpdir())}${sep}wildlife-hook-test-`));
      rmSync(fixturePath, { recursive: true, force: true });
    }
  });
}

function withHookFixture(run) {
  const fixture = mkdtempSync(join(tmpdir(), "wildlife-hook-test-"));
  const hooks = join(fixture, "Codex", "hooks");
  const source = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  mkdirSync(hooks, { recursive: true });
  try {
    for (const file of ["common.mjs", "inject_context.mjs", "commit_issue_context.mjs", "guard_commit_scope.mjs"]) {
      copyFileSync(join(source, file), join(hooks, file));
    }
    const invoke = (script, payload) => execFileSync(process.execPath, [join(hooks, script)], {
      input: JSON.stringify(payload), encoding: "utf8"
    }).trim();
    run({
      submit: (sessionId, prompt) => invoke("inject_context.mjs", { session_id: sessionId, prompt }),
      guard: (sessionId, reference) => invoke("guard_commit_scope.mjs", {
        session_id: sessionId,
        tool_input: { command: `git commit -m 'fix: 테스트 변경' -m '${reference}'` }
      }),
      legacy: context => {
        const logs = join(fixture, "Codex", "logs");
        mkdirSync(logs, { recursive: true });
        writeFileSync(join(logs, "commit_issue_context.json"), JSON.stringify(context));
      }
    });
  } finally {
    const fixturePath = resolve(fixture);
    assert.ok(fixturePath.startsWith(`${resolve(tmpdir())}${sep}wildlife-hook-test-`));
    rmSync(fixturePath, { recursive: true, force: true });
  }
}

test("작업 요청이 교차해도 각 작업의 이슈 번호와 확인 대기를 유지합니다", () => {
  withHookFixture(({ submit, guard }) => {
    submit("session-a", "커밋해줘 #13");
    submit("session-b", "커밋해줘");
    assert.equal(guard("session-a", "#13"), "");
    assert.match(guard("session-a", "#1"), /does not match/);
    assert.match(guard("session-b", "#13"), /did not include/);
    submit("session-b", "#1");
    assert.equal(guard("session-b", "#1"), "");
    assert.match(guard("session-b", "#13"), /does not match/);
    assert.equal(guard("session-a", "#13"), "");
  });
});

test("기존 공유 문맥은 일치하는 작업에만 적용하고 후속 답변을 별도로 저장합니다", () => {
  withHookFixture(({ legacy, submit, guard }) => {
    legacy(resolveCommitIssueContext("커밋해줘", null, "session-a"));
    assert.equal(guard("session-b", "#13"), "");
    assert.match(guard("session-a", "#13"), /did not include/);
    submit("session-a", "#13");
    legacy(resolveCommitIssueContext("커밋해줘 #1", null, "session-b"));
    assert.equal(guard("session-a", "#13"), "");
    assert.equal(guard("session-b", "#1"), "");
  });
});

test("세션 식별자가 없는 호출은 기존 확인 절차를 유지합니다", () => {
  withHookFixture(({ submit, guard }) => {
    submit(null, "커밋해줘");
    assert.match(guard(null, "#13"), /did not include/);
    submit(null, "#13");
    assert.equal(guard(null, "#13"), "");
    assert.match(guard(null, "#1"), /does not match/);
  });
});

test("세션 식별자는 경로에 직접 포함하지 않습니다", () => {
  assert.match(commitIssueContextFileName("../../outside"), /^commit_issue_context\.[a-f0-9]{64}\.json$/);
  assert.notEqual(commitIssueContextFileName("a"), commitIssueContextFileName("b"));
});
