# Repo Testing Guidelines

Use this reference when the repository expects the testing standards provided by the user, or when nearby tests show the same patterns.

## Framework Defaults

- .NET: use NUnit, AwesomeAssertions, Moq `<=4.18.4 or >=4.20.69`, and AutoFixture.
- TypeScript: use Vitest.

## Core Rules

- Cover every behavior change with tests.
- Treat missing tests as a valid PR blocker.
- Cover happy-path, edge-case, and error-path behavior.
- Update tests when behavior changes.
- Keep tests readable enough to act as executable specifications.
- Test one thing at a time.
- For bug fixes, write the reproducing test first when feasible.
- Urgency is not a reason to skip tests.

## Naming

- Use descriptive test names.
- In .NET repos following this convention, prefer `MethodName_Scenario_ExpectedResult`.

## Structure

- Use AAA for unit tests:
  - Arrange
  - Act
  - Assert
- Use AAAC when the test creates resources that require cleanup:
  - Arrange
  - Act
  - Assert
  - Cleanup

## Assertions

- Use AwesomeAssertions in .NET tests.
- Assert exceptions explicitly when failure behavior matters.
- Assert state changes, not only return values.
- Prefer assertions that make failures diagnosable.

## Mocking

### Moq strict mode

Prefer strict mocks so unexpected calls fail the test immediately.

```csharp
_mockRepository = new MockRepository(MockBehavior.Strict);
_builderMock = _mockRepository.Create<ISearchDocumentBuilder>();
_workContextMock = _mockRepository.Create<IWorkContext>();
```

If the repo does not use a shared `MockRepository`, use `new Mock<T>(MockBehavior.Strict)`.

### HTTP client mocking

When production code uses `IHttpClientFactory`, prefer mocking the handler behind a real `HttpClient` instead of mocking `HttpClient` directly.

```csharp
private Mock<HttpMessageHandler> _httpMessageHandler;

[SetUp]
public void Setup()
{
    _httpMessageHandler = new Mock<HttpMessageHandler>();
    var httpClient = new HttpClient(_httpMessageHandler.Object);

    _httpClientFactory = new Mock<IHttpClientFactory>();
    _httpClientFactory
        .Setup(x => x.CreateClient(It.Is<string>(y => y == Constants.HttpClients.ShortServiceName)))
        .Returns(httpClient);
}
```

Use `Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ...)` to verify request details, capture sent payloads, and return the desired response.

## .NET Unit Test Patterns

- Use `async Task` test methods for async code.
- Reuse shared setup in `[SetUp]`.
- Prefer AutoFixture for test data, overriding only scenario-specific values.
- Verify important mock interactions when they represent real side effects.
- For controller tests, set `ControllerContext` and mock request metadata when needed.

## Integration Tests

- Use integration tests to verify interactions between components.
- Use realistic data where practical.
- Test across the environments that matter to the application.
- For SQL repository tests, prefer an in-memory database strategy when the repo supports it.

## Performance, Security, and Accessibility

- Add performance or load tests for critical paths when the task requires them.
- Treat security testing as part of overall test quality.
- Add accessibility tests where UI behavior must satisfy accessibility standards.

## Coverage

- Use code coverage tools as a signal for under-tested areas.
- In Coverlet-based .NET test projects, reference only projects that are actually under test.
- If common test helpers are needed across projects, extract them into a dedicated shared test project instead of referencing unrelated production modules.

## Legacy Code

When adding tests to legacy code:

- Identify the risky or changed behavior first.
- Break dependencies where necessary.
- Use seams to isolate the code under test.
- Add characterization tests before changing fragile behavior.
- Add tests incrementally.
- Refactor toward testability as you go.

## Repo Workflow Notes

- Prefer the narrowest relevant test project and command.
- Do not run the full solution test suite by default in repos where it is known to be slow or timeout-prone.
- Report what was validated and what was not.
