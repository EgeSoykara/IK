# AGENTS.md

## Goal
Produce the simplest correct repo-aligned solution. Do not guess, hack, over-engineer, widen scope, split into unnecessary phases, or leave duplicate/legacy authority behind.

## Authority
- Current behavior: code first; repo-local docs only when they match code.
- Desired behavior: task + confirmed decisions.
- If docs conflict with code, report doc drift and synchronize docs when the task affects documented behavior or architecture.

## Required Workflow
1. Read the full task.
2. Ask blocker questions only when the answer cannot be proven and materially affects behavior, business rules, contracts, data, authority, cutover, transactions, side effects, failure handling, or canonical docs.
3. Run Discovery before Implementation.
4. Do not implement while material UNKNOWNs/blockers remain.
5. Implement in one clean pass.
6. Fix root causes, not symptoms.
7. Validate strictly, including documentation coverage.
8. After UI/UX implementation, visual polish, final-touch, or regression-fix work, run the checklist in `BetSolution/Docs/UX/ui-ux-regression-checklist.md` and record the evidence in the task/PR before merge.
9. After every implementation, create a subagent to objectively review the implementation in the branch and grade it out of 10. If the result is not 10/10, consult with the subagent, address the concrete gaps, and repeat until the implementation reaches 10/10 production-grade standard or a blocker requires returning to Discovery.

## Complete Cutover Rule
When replacing a behavior, model, pattern, name, resolver, or authority:
- remove the old concept completely: naming, abstractions, helpers, comments, docs, DI wiring, fallbacks, and call sites;
- leave no parallel old/new paths, shadow authority, stale docs, or compatibility residue;
- keep transitional/compatibility logic only when explicitly required, and label it `Intentional Transitional Logic` with why it exists, who approved it, and what removes it later.

Final state must have one active authority, one active concept, correct naming, and no obsolete paths.

## Documentation
If the change affects behavior, contracts, naming, authority ownership, lifecycle, configuration, operator workflow, or canonical architecture, update/create the minimal canonical repo doc in the same run. Archive/remove superseded docs when needed. If no doc change is needed, prove why from Discovery and final code. If the canonical doc target is materially unknown, stop and ask.

## Engineering Rules
- UNKNOWN => stop and ask.
- Assumptions, hacks, silent fallbacks, workaround branches, task-external refactors, commented-out legacy, and unapproved TODO/FIXME are forbidden.
- Preserve repository naming, placement, dependency, transaction, and lifecycle patterns.
- Avoid unnecessary abstractions, config, flags, extension points, or frameworks.
- Tests are allowed. Add or update focused, integration, browser, regression, and broader validation tests when they materially improve confidence in the task outcome, follow repo patterns, and do not create fake authority or unrelated behavior changes.

## Evidence Standard
Important findings must cite evidence as: `path -> symbol -> reason`.
