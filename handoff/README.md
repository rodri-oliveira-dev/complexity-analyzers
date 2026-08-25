# Analyzer Handoff

This directory stores minimal semantic handoff data for isolated SDD chats.

Git is the factual memory of the repository. Versioned specs are the SDD contract. Handoff files are only compact semantic memory for continuity between steps.

Future chats must reconstruct context from the repository, Git state, applicable `AGENTS.md` files, specs, and relevant code. A handoff file never replaces repository inspection and must not be preserved when it conflicts with Git or code.

`phase-1.json` is limited to this shape:

```json
{
  "delivery": "",
  "currentObjective": "",
  "completedStep": "",
  "decisions": [],
  "constraints": [],
  "relevantFiles": [],
  "validations": [],
  "knownPendingItems": [],
  "nextStep": "",
  "lastCommit": {
    "hash": "",
    "message": ""
  }
}
```

Do not add conversation history, chain-of-thought, logs, full command output, diffs, full file contents, or discarded attempts.
