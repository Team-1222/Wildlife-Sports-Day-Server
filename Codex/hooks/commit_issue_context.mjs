export function resolveCommitIssueContext(prompt, previousContext = null, sessionId = null) {
  const isCommitRequest = /(commit|git|pr|pull request|커밋|깃|풀리퀘스트|풀 리퀘스트|피알)/i.test(prompt);
  const issueMatch = prompt.match(/#\d+/);
  const noIssueConfirmed = /(관련\s*)?이슈\s*(없|없어|없음)|이슈\s*번호\s*(없|없어|없음)|no\s+(related\s+)?issue|without\s+issue/i.test(prompt);
  const sameSession = !previousContext?.sessionId || previousContext.sessionId === sessionId;
  const awaitingReply = previousContext?.needsIssueConfirmation === true && sameSession;
  const numericReply = awaitingReply ? prompt.match(/^\s*(\d+)\s*(?:번(?:\s*이슈)?)?\s*[.!]?\s*$/) : null;
  const issueRef = issueMatch?.[0] ?? (numericReply ? `#${numericReply[1]}` : null);
  const isPendingReply = awaitingReply && Boolean(issueRef || noIssueConfirmed);

  if (!isCommitRequest && !isPendingReply) {
    return null;
  }

  return {
    timestamp: new Date().toISOString(),
    sessionId,
    issueRef,
    noIssueConfirmed,
    needsIssueConfirmation: !issueRef && !noIssueConfirmed
  };
}
