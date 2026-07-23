# Fix Prompt

## Issue
<INSERT ISSUE DESCRIPTION>

## Discovery Output
<OPTIONAL>

## Implementation Output
<OPTIONAL>

## Validation Output
<OPTIONAL>

## Documentation Impact
<OPTIONAL>

## Confirmed Decisions
- none

## Relevant Context
<OPTIONAL>

Use `AGENTS.md` as global policy.

Fix the root cause only. Do not patch symptoms, add workaround branches, widen scope, or leave stale/competing docs.

## Required Actions
- Summarize issue and impact.
- Reconstruct the failing path from code.
- Identify root cause with evidence.
- If requirements/doc target are materially unclear, stop and route to Discovery.
- Remove or correct broken/conflicting/duplicated logic.
- Apply the simplest repo-aligned fix without creating a parallel path.
- Check directly relevant edge cases and failure modes around the broken path, including null/empty state, permissions, lifecycle state, concurrency, external dependency failures, and transaction boundaries when applicable.
- Preserve unrelated behavior. If the fix is for a refactor regression, verify intended behavior remains unchanged outside the reported issue.
- Do not add caching, batching, new abstractions, or broader performance changes unless the issue is itself a proven performance/root-cause problem.
- Keep API, database, UI/component, and data-contract changes limited to what the root cause requires.
- Avoid magic strings and hardcoded string values in authoritative domain paths. Use repository-aligned enums, constants, value objects, typed identifiers, DB-backed config, or documented seed data instead.
- When database schema is touched, make table structure production-ready: explicit primary keys, requiredness, lengths/precision, indexes, uniqueness/idempotency constraints, audit fields, and repository-aligned timestamps/status fields where applicable.
- Choose attributes over Fluent API for entity/table/column/relationship mapping. Do not add new Fluent API mapping unless the repository makes attributes impossible; if so, stop and report the exact blocker or approved exception.
- Define required foreign-key scalar fields, navigation properties, and relationship attributes correctly. Do not leave orphan relational IDs or implicit relationships where a real FK is required.
- Determine documentation impact; update/create/archive/remove canonical docs when required.
- State validation focus.
- Self-grade the fix on a 1-10 scale before the verdict, focusing on root-cause correctness, clean implementation, code quality, repository pattern alignment, single-authority design, minimality, regression coverage, and documentation completeness. Be strict; if the score is below 10, state the concrete reason.

## Expected Output
# Issue Summary
# Confirmed Decisions
# Root Cause
- Evidence
# Removed or Corrected Logic
# Fix Applied
# Documentation Changes
- File / action / why, or NONE with proof
# Why This Fix Is Correct
- Root cause addressed
- Not a patch
- Not over-engineered
- Relevant edge cases/failure modes covered or not applicable
- Unrelated behavior preserved
- No magic strings or hardcoded string authority added
- Database schema is production-ready when touched
- Attribute-first mapping and foreign keys are correct when DB touched
# Validation Focus
- Edge cases / failure modes
- Error handling
- Behavior preservation if refactor-related
- Performance regression if relevant
# Need Rediscovery?
YES / NO
# Self-Grade
- Score: N/10
- Reason:
- What prevents 10/10, if anything:
# Verdict
FIXED or BLOCKED
