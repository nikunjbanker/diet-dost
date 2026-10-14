<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Clean Architecture & Native Zero-Dependency CQRS

## 1. Native CQRS Dispatcher (Zero MediatR Dependency)
- **Strict Prohibition**: Never add MediatR or third-party mediator NuGet packages.
- All command and query execution uses the native `IDispatcher` interface powered by `Microsoft.Extensions.DependencyInjection`.
- Interfaces:
  - Commands: `ICommand<TResult>`, `ICommandHandler<TCommand, TResult>`
  - Queries: `IQuery<TResult>`, `IQueryHandler<TQuery, TResult>`

## 2. Thin Controllers Standard
- Controllers in `Nutrition.WebGateway` must contain 0 business logic, 0 direct EF Core queries, 0 direct file I/O.
- They:
  1. Extract user identity and claims from `HttpContext.User`.
  2. Bind and sanitize incoming HTTP request DTOs.
  3. Dispatch via `_dispatcher.SendAsync(...)` or `_dispatcher.QueryAsync(...)`.
  4. Map application `Result<T>` envelopes to HTTP responses (`Ok()`, `NotFound()`, `BadRequest()`, `StatusCode(403)`).

## 3. Universal Result Envelope Pattern
- Every application use-case returns `Result<T>` or `Result`.
- Never throw exceptions for expected domain failures (e.g. invalid credentials, quota exceeded, meal not found). Throw exceptions only for unexpected operational or contract violations.

## 4. Universal UTC Persistence & Temporal Consistency
- All timestamps stored in EF Core SQLite are strictly converted to UTC via `ValueConverter<DateTime, DateTime>`.
- Read operations guarantee `DateTimeKind.Utc`. Local timezone translation is strictly performed at the presentation boundary against IANA timezones.

## 5. Persistent SQLite SMB Network Configuration
- Production Azure Container Apps mount Azure Files SMB share at `/app/data`.
- Connection string: `Data Source=/app/data/diet_dost.db`.
- At startup, execute `PRAGMA journal_mode = DELETE;` (eliminates SMB network file lock corruption).
- Azure Container Apps invariant: `minReplicas: 1, maxReplicas: 1`.
