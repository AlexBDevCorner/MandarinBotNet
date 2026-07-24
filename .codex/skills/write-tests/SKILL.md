---
name: write-tests
description: Write and update automated tests for changed or newly added code. Use when Codex needs to cover uncommitted changes in the current branch, create tests for recently added functionality, add missing regression tests for a bug fix, or implement tests that follow repository-specific testing conventions and existing project patterns.
---

# Write Tests

## Overview

Add focused automated tests for the behavior that changed. Inspect the current repo instructions, identify the smallest affected test surface, copy local conventions from nearby tests, and implement tests that prove the new or changed behavior without broad unrelated refactors.

## Workflow

1. Inspect repo guidance first.
2. Inspect the changed code and recent diff before designing tests.
3. Identify the behavior that changed, plus happy-path, edge-case, and failure-path expectations.
4. Find the closest existing test project, fixture style, and naming pattern before writing anything.
5. Add tests in the narrowest correct place.
6. Validate with the cheapest relevant check available, and state what was and was not run.

## Required Behavior

- Read local repo instructions such as `AGENTS.md`, project testing docs, and nearby tests before writing new tests.
- Prefer extending the existing test project for the changed production code instead of creating a new project unless the repo clearly expects one.
- Mirror local framework choices instead of inventing a new stack.
- Treat missing tests for behavior changes as a blocker, not an optional follow-up.
- For bug fixes, prefer writing the reproducing test first when feasible.
- Keep each test focused on one behavior.
- Avoid speculative production refactors unless they are required to make the changed behavior testable.

## Test Design

- Start from behavior, not implementation details.
- Name tests descriptively using the local convention. In .NET repos like this one, prefer `MethodName_Scenario_ExpectedResult`.
- Cover the happy path, important edge cases, and failure cases introduced by the change.
- Assert observable outcomes: return values, state changes, emitted calls, thrown exceptions, and persisted output.
- Reuse existing builders, fixtures, helper methods, and shared test infrastructure when they already exist.
- Keep test data explicit. Use generated data only when it improves readability and maintenance.
- Always use arrange/act/assert pattern

## Framework Selection

- In .NET projects, prefer NUnit, AwesomeAssertions, Moq, and AutoFixture when that is the local standard.
- In TypeScript projects, prefer Vitest when that is the local standard.
- Do not introduce a new test framework into an established project unless explicitly asked.

## .NET-Specific Guidance

- Prefer `MockRepository(MockBehavior.Strict)` and create mocks from the shared repository in setup code when the local test suite does that.
- Use AwesomeAssertions for assertions.
- Use `async Task` tests for async code.
- Mock `IHttpClientFactory` and `HttpMessageHandler` patterns the same way the repo already does when testing HTTP client flows.
- Verify critical side effects and interactions explicitly when behavior depends on them.
- Set controller/request context when request metadata affects the outcome.

Read [references/repo-testing-guidelines.md](references/repo-testing-guidelines.md) for the detailed repo-aligned testing rules and examples.

## Validation

- Prefer targeted validation over broad suites.
- Do not run a whole-repo test suite by default if repo guidance says it is slow or commonly times out.
- Run the narrowest relevant test command when explicitly asked, when the cost is low, or when validation is necessary to confirm the change.
- If validation is skipped or partial, say so clearly.

## Output Expectations

- Explain which behavior the new tests cover.
- Mention where the tests were added.
- State the validation performed and any gaps.
