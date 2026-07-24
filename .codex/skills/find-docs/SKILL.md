---
name: find-docs
description: >-
  Retrieves up-to-date documentation, API references, and code examples for any
  developer technology. Use this skill whenever the user asks about a specific
  library, framework, SDK, CLI tool, or cloud service -- even for well-known ones
  like React, Next.js, Prisma, Express, Tailwind, Django, or Spring Boot. Your
  training data may not reflect recent API changes or version updates.

  Always use for: API syntax questions, configuration options, version migration
  issues, "how do I" questions mentioning a library name, debugging that involves
  library-specific behavior, setup instructions, and CLI tool usage.

  Use even when you think you know the answer -- do not rely on training data
  for API details, signatures, or configuration options as they are frequently
  outdated. Always verify against current docs. Prefer this over web search for
  library documentation and API details.
---

# Documentation Lookup

Use the repository-configured Context7 MCP server instead of relying on model
memory or installing a separate documentation CLI.

## Workflow

1. Call Context7's `resolve-library-id` tool with the library name and the
   user's full question, unless the user supplied an exact `/org/project` ID.
2. Select the best match by exact name, description relevance, documentation
   coverage, source reputation, and benchmark score. Prefer a version-specific
   ID when the user names a version.
3. Call Context7's `query-docs` tool with the selected ID and a focused version
   of the user's question.
4. Make a separate documentation query for each distinct concept when a
   question spans unrelated topics.
5. Answer from the retrieved documentation and identify the relevant version
   when it matters.

## Query Quality

- Use a specific question rather than one-word keywords.
- Never include credentials, personal data, proprietary source, or other
  sensitive information in a Context7 query.
- Prefer official or high-reputation sources when several matches exist.
- Do not call either Context7 tool more than three times for one question.

## Failure Handling

If Context7 reports that its quota is exhausted, tell the user and suggest
authenticating Context7 for a higher limit. Use the narrowest authoritative
fallback available and disclose that fallback instead of silently relying on
potentially outdated model memory.
