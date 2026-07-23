# Discovery Prompt

## Task
<INSERT TASK DESCRIPTION>

## Confirmed Decisions
- none

## Relevant Context
<OPTIONAL>

Use `AGENTS.md` as global policy.

Discovery only. Inspect the task, code, and matching repo-local docs. Do not implement.

## Required Analysis
- Extract explicit requirements and confirmed decisions.
- Trace relevant end-to-end code paths.
- Determine current behavior, authority ownership, data flow, affected files/services, and invariants.
- Compare required vs current behavior.
- Identify missing, conflicting, duplicate, legacy logic and doc drift.
- Identify repo patterns for avoiding magic strings and hardcoded string values in affected domain paths; determine whether values belong in enums/constants/value objects, DB-backed config, or documented seed data.
- When database schema may be affected, identify production schema requirements: primary keys, required fields, field lengths/precision, indexes, uniqueness/idempotency constraints, foreign keys, navigation properties, delete behavior, audit fields, and the repository's attribute-based mapping expectations.
- Identify directly relevant edge cases and failure modes. Include null/empty state, permission boundaries, lifecycle states, concurrency, external dependency failures, and transaction boundaries when applicable. Do not invent hypothetical edge cases outside task scope.
- Evaluate performance/scalability impact only when the task touches large data paths, repeated queries, rendering loops, external calls, caching, memory-heavy logic, or hot paths. If not applicable, state NONE with evidence.
- Identify surface/contract impact when the task creates or changes API endpoints, database schema, UI/component structure, or data contracts. If no surface or contract changes are involved, state NONE with evidence.
- Determine impact on behavior, contracts, naming, authority, lifecycle, configuration, operator workflow, and canonical architecture.
- Identify canonical docs to update/create/archive/remove. Use `none` or `unknown` only when proven.
- Mark every unprovable material point as UNKNOWN and ask only blocker questions that affect implementation.
- Define one clean implementation pass, including required documentation work.

## Expected Output
# Requirements Understood
# Confirmed Decisions
# Current Behavior
- Evidence: `path -> symbol -> reason`
# Gap Analysis
- Correct
- Missing
- Conflicting
- Duplicate
- Legacy
# Edge Cases and Failure Modes
- Relevant cases
- Failure boundaries
- NONE with evidence if not applicable
# Performance / Scalability Impact
- Bottlenecks / query count / rendering / memory / external call risks
- NONE with evidence if not applicable
# Surface / Contract Impact
- API endpoints
- Database schema
- UI/component structure
- Data contracts
- NONE with evidence if not applicable
# Impacted Areas
# Invariants to Preserve
# Documentation Impact
- Update / create / archive / remove
- Why
- Canonical target
# Unknowns
# Blocker Questions
For each: question, why blocking, affected decision, suggested options if useful.
# Implementation Scope
# Verdict
READY FOR IMPLEMENTATION or BLOCKED
