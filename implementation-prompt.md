# Implementation Prompt

## Task
<INSERT TASK DESCRIPTION>

## Discovery Output
<PASTE DISCOVERY OUTPUT>

## Documentation Impact
<PASTE DOCUMENTATION IMPACT FROM DISCOVERY>

## Confirmed Decisions
- none

## Relevant Context
<OPTIONAL>

Use `AGENTS.md` as global policy.

Implement the task in one clean pass using the task, Discovery, documentation impact, confirmed decisions, and current repo patterns. Do not proceed if material UNKNOWNs remain.

## Required Actions
- Verify requirements are closed.
- Identify and remove/replace conflicting, duplicate, or legacy logic.
- Implement the required behavior through one authority path.
- Handle edge cases and failure modes identified by Discovery using existing repository patterns. If Discovery marks them NOT APPLICABLE, do not add speculative handling.
- Preserve behavior when the task is refactor-only. Any behavior change must be explicitly required by the task or confirmed decisions.
- Avoid introducing performance risk. Only add caching, batching, async orchestration, or structural performance changes when Discovery identified a real bottleneck or the task requires it.
- Keep API, database, UI/component, and data-contract changes limited to the surfaces identified by Discovery.
- Avoid magic strings and hardcoded string values in authoritative domain paths. Use repository-aligned enums, constants, value objects, typed identifiers, DB-backed config, or documented seed data instead.
- When database schema is touched, make table structure production-ready: explicit primary keys, requiredness, lengths/precision, indexes, uniqueness/idempotency constraints, audit fields, and repository-aligned timestamps/status fields where applicable.
- Choose attributes over Fluent API for entity/table/column/relationship mapping. Do not add new Fluent API mapping unless the repository makes attributes impossible; if so, stop and report the exact blocker or approved exception.
- Define required foreign-key scalar fields, navigation properties, and relationship attributes correctly. Do not leave orphan relational IDs or implicit relationships where a real FK is required.
- Synchronize only the canonical docs identified by Discovery; archive/remove superseded docs when needed.
- Keep scope limited to the task.
- If a new blocker appears, stop and return to Discovery.
- Self-check: single authority, no legacy/conflict/duplicates, no hack, no over-engineering, pattern compliance, docs synchronized.
- After every implementation, create a subagent to objectively review the implementation in the branch and grade it out of 10. If the result is not 10/10, consult with the subagent, address the concrete gaps, and repeat until the implementation reaches 10/10 production-grade standard or a blocker requires returning to Discovery.
- Self-grade the implementation on a 1-10 scale before the verdict, focusing on clean implementation, code quality, repository pattern alignment, single-authority design, minimality, test/validation coverage, and documentation completeness. Be strict; if the score is below 10, state the concrete reason.

## Expected Output
# Pre-Change Check
- Requirements complete: YES / NO
- Remaining blocker: YES / NO
# Confirmed Decisions
# Removed or Replaced Logic
# Implementation Summary
# Documentation Changes
- File / action / why, or NONE with proof
# Files Changed
# Self-Check
- Single authority: PASS / FAIL
- Legacy/conflict removed: PASS / FAIL
- No duplicate logic: PASS / FAIL
- No hack: PASS / FAIL
- No over-engineering: PASS / FAIL
- Pattern compliance: PASS / FAIL
- Edge cases handled: PASS / FAIL / NOT APPLICABLE
- Error handling follows repository patterns: PASS / FAIL / NOT APPLICABLE
- Behavior preservation verified when refactor-only: PASS / FAIL / NOT APPLICABLE
- Performance risk introduced: PASS / FAIL / NOT APPLICABLE
- Surface/contract changes limited to Discovery scope: PASS / FAIL / NOT APPLICABLE
- No magic strings or hardcoded string authority: PASS / FAIL
- Production-ready database schema when DB touched: PASS / FAIL / NOT APPLICABLE
- Attribute-first mapping and foreign keys correct when DB touched: PASS / FAIL / NOT APPLICABLE
- Documentation synchronized: PASS / FAIL
# Self-Grade
- Score: N/10
- Reason:
- What prevents 10/10, if anything:
# Subagent Review Gate
- Reviewer created: YES / NO
- Objective grade: N/10
- Findings:
- Remediation completed: YES / NO / NOT APPLICABLE
- Final production-grade result: 10/10 / BLOCKED
# Verdict
IMPLEMENTED or BLOCKED - RETURN TO DISCOVERY
