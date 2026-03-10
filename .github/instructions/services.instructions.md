---
applyTo: "src/DemoFresh/Services/**"
---
# Service Implementation Guide

When creating or modifying services:

1. Every service must have a corresponding interface (e.g., `IRepoService` for `RepoService`)
2. Register services in `Extensions/ServiceCollectionExtensions.cs`
3. Use constructor injection for dependencies
4. All public methods should be async and return `Task` or `Task<T>`
5. Use `ILogger<T>` for structured logging with message templates (not string interpolation)
6. Dispose/clean up resources properly (implement IAsyncDisposable where needed)
