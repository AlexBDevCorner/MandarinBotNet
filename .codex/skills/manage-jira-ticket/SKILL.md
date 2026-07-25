---
name: manage-jira-ticket
description: Keep MandarinBotNet Jira tickets in project MHB synchronized with engineering work. Use whenever Codex implements or fixes an MHB Jira ticket, works from a branch or PR containing an MHB issue key, creates or updates a PR for an MHB ticket, or addresses review comments. Move active work to In Progress, move review-ready work to Code Review, and never move a ticket to Done without an explicit user instruction.
---

# Manage Jira Ticket

Keep the Jira status aligned with the actual state of work in this repository. Use the connected Jira/Atlassian tools for project `MHB` on the MandarinHugBot Jira site.

## Resolve the Ticket

1. Resolve the issue key in this order:
   - Use an explicit `MHB-N` key from the request.
   - Otherwise, extract it from the current branch name.
   - Otherwise, extract it from the relevant PR branch or title.
2. Fetch the issue and verify that it belongs to project `MHB`.
3. If no unique issue can be resolved, ask the user for the key before changing Jira or starting implementation.
4. Treat requests to inspect, explain, estimate, or plan as read-only. Do not transition the issue until implementation or review-fix work begins.

## Synchronize the Workflow

| Work event | Jira status | Timing |
| --- | --- | --- |
| Begin implementation or a bug fix | `In Progress` | Before editing code |
| Begin addressing review comments | `In Progress` | Before editing code |
| Successfully create the PR | `Code Review` | Immediately after PR creation |
| Finish, validate, and push review fixes to the PR | `Code Review` | After the PR is ready for another review |
| Finish local work without creating or updating a PR | Keep `In Progress` | Do not advance it |
| PR is approved or merged | Keep `Code Review` | Wait for the user |
| User explicitly instructs Codex to mark the ticket done | `Done` | Only after that instruction |

Apply these rules:

- Fetch the issue's current status before acting.
- Fetch the issue's currently available transitions and select the transition by destination status name. Never hard-code a transition ID or assume the same ID applies to another issue.
- Skip a transition when the issue is already in the target status.
- Re-fetch the issue after each transition and verify the resulting status.
- Keep the issue `In Progress` while implementation is blocked or incomplete unless the user explicitly requests another status.
- If a PR creation or push fails, keep the issue `In Progress`.
- If Jira cannot be read or updated, report the failure immediately and ask whether to continue without synchronization. Never silently omit the Jira step.

## Handle Review Feedback

When the issue is in `Code Review` and the user asks to address comments:

1. Move the issue to `In Progress`.
2. Implement and validate the requested fixes.
3. Push or otherwise update the existing PR when the task includes that action.
4. Move the issue back to `Code Review` only after the updated PR is ready for review.

If the fixes remain only in the local worktree, leave the issue `In Progress`.

## Protect Done

Never transition an issue to `Done` merely because:

- the implementation is complete;
- tests pass;
- a PR was created, approved, or merged;
- the user says to finish the coding task; or
- no further code changes appear necessary.

Require an explicit instruction that identifies the ticket and asks to mark or move it to `Done`. If the wording is ambiguous, ask. Do not reopen a ticket already in `Done` without explicit user direction.

## Report Status

At handoff, state the issue key and verified Jira status. If no PR was created, make clear that the issue intentionally remains `In Progress`.
