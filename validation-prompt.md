# Validation Prompt

## Task
<INSERT TASK DESCRIPTION>

## Discovery Output
<PASTE DISCOVERY OUTPUT>

## Implementation Output
<PASTE IMPLEMENTATION OUTPUT>

## Fix Output
<OPTIONAL>

## Documentation Impact
<PASTE DOCUMENTATION IMPACT FROM DISCOVERY OR FIX>

## Confirmed Decisions
- none

## Relevant Context
<OPTIONAL>

Use `AGENTS.md` as global policy.

Strictly validate final code and affected docs against the task, confirmed decisions, Discovery, Implementation, Fix output if present, and documentation impact.

## Fail If Any Apply
- requirement incomplete;
- assumption-based logic remains;
- hack, patch, workaround, or over-engineering present;
- legacy/conflicting/duplicate/parallel paths remain;
- repository pattern violation;
- unnecessary scope expansion;
- directly relevant edge cases or failure modes identified by Discovery are unhandled;
- error handling bypasses repository patterns or masks important failures;
- refactor-only work changes behavior without explicit approval;
- performance/scalability regression is introduced on a path Discovery identified as relevant;
- API, database, UI/component, or data-contract changes exceed the surfaces identified by Discovery;
- magic strings or hardcoded string values remain in authoritative domain paths where repository-aligned enums, constants, value objects, typed identifiers, DB-backed config, or documented seed data should be used;
- database schema changes are not production-ready, including missing primary keys, requiredness, lengths/precision, indexes, uniqueness/idempotency constraints, audit fields, or repository-aligned timestamps/status fields where applicable;
- new entity/table/column/relationship mapping uses Fluent API where attributes can express the mapping;
- required foreign-key scalar fields, navigation properties, or relationship attributes are missing or incorrect;
- behavior/docs changed without canonical docs synchronized;
- required doc missing, superseded doc still active, competing docs remain, or `no documentation impact` is unsupported;
- implementation output is missing the required 1-10 self-grade, or the self-grade does not address clean implementation, code quality, repository alignment, single-authority design, minimality, test/validation coverage, and documentation completeness;
- fix output is present but missing the required 1-10 self-grade, or the self-grade does not address root-cause correctness, clean implementation, code quality, repository alignment, single-authority design, minimality, regression coverage, and documentation completeness.

Route to FIX when requirements are clear and implementation is wrong. Route to REDISCOVERY when requirements, doc ownership, or system understanding remain unclear.

## Expected Output
# Validation Summary
# Confirmed Decisions
# Passed Checks
# Failed Checks
# Documentation Findings
# Hack Check
PASS / FAIL
# Over-Engineering Check
PASS / FAIL
# Cleanup Check
PASS / FAIL
# Requirement Coverage Check
PASS / FAIL
# Edge Case Coverage Check
PASS / FAIL / NOT APPLICABLE
# Error Handling Check
PASS / FAIL / NOT APPLICABLE
# Behavior Preservation Check
PASS / FAIL / NOT APPLICABLE
# Performance Regression Check
PASS / FAIL / NOT APPLICABLE
# Surface / Contract Scope Check
PASS / FAIL / NOT APPLICABLE
# Magic String / Hardcoding Check
PASS / FAIL
# Database Schema Check
PASS / FAIL / NOT APPLICABLE
# Attribute Mapping / FK Check
PASS / FAIL / NOT APPLICABLE
# Documentation Coverage Check
PASS / FAIL
# Pattern Compliance Check
PASS / FAIL
# Self-Grade Review
PASS / FAIL / NOT APPLICABLE
# Need Fix or Rediscovery?
FIX / REDISCOVERY / NONE
# Final Verdict
ACCEPT or REJECT
