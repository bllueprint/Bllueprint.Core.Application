# Bllueprint.Core.Application

A lightweight .NET library for **application-layer command handling** that provides a structured, fluent pipeline for orchestrating domain operations with built-in error capture, guard evaluation, and result shaping.

## Features

- **CommandHandler base class** — a clean foundation for MediatR request handlers
- **Handler pipeline** — fluent builder for chaining entity fetches, guards, mutations, and persistence
- **Predicate checks** — inline validation against a resolved entity before continuing
- **Guard pipelines** — async boolean checks and exception-safe guards as first-class pipeline steps
- **Result types** — strongly-typed `ICommandResult<T>` and `CollectionResult<T>` with error, missing, and success states
- **Zero side-effects on failure** — steps short-circuit as soon as the context is poisoned

---

## Installation

> Package not yet published. To use locally, reference the project directly:

```xml
<ProjectReference Include="../Bllueprint.Core.Application/Bllueprint.Core.Application.csproj" />
```

Or once published to NuGet:

```bash
dotnet add package Bllueprint.Core.Application
```

---

## Getting Started

### 1. Create a command handler

Inherit from `CommandHandler<TCommand, T>` and build a pipeline inside `Handle`:

```csharp
public class ConfirmOrderHandler(IOrderRepository repo, INotificationContext notifications)
    : CommandHandler<ConfirmOrderCommand, Order>(notifications)
{
    public override async Task<ICommandResult<Order>> Handle(
        ConfirmOrderCommand request, CancellationToken cancellationToken)
    {
        return await Invoke(() => repo.FindAsync(request.OrderId))
            .WithCheck(PipelineGuard.NotNull<Order>())
                .WithMessage("Order not found.")
            .Invoke(order => order.Confirm())
            .Save(order => repo.SaveAsync(order))
            .ToResultAsync();
    }
}
```

---

### 2. Chain entity-transforming steps

Use `Invoke` overloads to load and project entities across pipeline steps:

```csharp
return await Invoke(() => repo.FindUserAsync(request.UserId))
    .Invoke(user => repo.FindAccountAsync(user.AccountId))
    .Invoke(account => repo.FindPlanAsync(account.PlanId))
    .Save(plan => repo.SaveAsync(plan))
    .ToResultAsync();
```

Each step receives the output of the previous one. If any step returns `null`, the pipeline returns `Missing()` immediately.

---

### 3. Add a boolean guard

Use `Invoke(Func<Task<bool>>)` to run an async check before continuing:

```csharp
return await Invoke(() => authService.UserExistsAsync(request.UserId))
    .With(result => result == true)
    .WithMessage("User does not exist.")
    .Invoke(() => repo.FindOrderAsync(request.OrderId))
    .ToResultAsync();
```

---

### 4. Add an exception-safe guard

Use `Invoke(Func<Task>)` as a fire-and-forget guard step that captures exceptions:

```csharp
return await Invoke(() => paymentService.ValidateAsync(request.PaymentToken))
    .WithMessage("Payment token validation failed.")
    .Invoke(() => repo.FindOrderAsync(request.OrderId))
    .ToResultAsync();
```

If the task throws, the pipeline fails with the configured message instead of propagating the exception.

---

### 5. Handle the result

Inspect `ICommandResult<T>` in your controller or endpoint:

```csharp
var result = await mediator.Send(command);

if (result.HasErrors)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"[{error.TransitionName}] {error.Message}");
}
else if (result.NotFound)
{
    return Results.NotFound();
}
else
{
    return Results.Ok(result.Entity);
}
```

---

## Core Concepts

### `CommandHandler<TCommand, T>`

The base class for all application command handlers. Implements MediatR's `IRequestHandler<TCommand, ICommandResult<T>>` and provides three `Invoke` entry points:

| Method | Returns | Use for |
|---|---|---|
| `Invoke(Func<Task<T?>>)` | `IHandlerPipeline<T>` | Loading the root entity |
| `Invoke(Func<Task<bool>>)` | `IGuardPipeline` | Async boolean preconditions |
| `Invoke(Func<Task>)` | `IExceptionGuardPipeline` | Fire-and-forget guards (exception-safe) |

---

### Handler Pipeline

Pipelines are built as sequential step chains using a fluent API:

| Method | Description |
|---|---|
| `.Invoke<TNext>(Func<Task<TNext?>>)` | Loads the next entity, ignoring the current one |
| `.Invoke<TNext>(Func<T, Task<TNext?>>)` | Loads the next entity using the current one as input |
| `.Invoke(Action<T>)` | Applies a synchronous mutation to the current entity |
| `.Invoke(Func<Task>)` | Runs an async guard task; captures exceptions as errors |
| `.WithCheck(Func<T, bool>)` | Evaluates a predicate against the current entity |
| `.Save(Func<T, Task>)` | Persists the entity; captures exceptions as errors |
| `.ToResultAsync()` | Executes all steps and returns `ICommandResult<T>` |

> Steps are collected lazily and executed in order only when `ToResultAsync()` is called. If the context is already poisoned, each step is skipped without running.

---

### Predicate Checks (`IWithPipeline<T>`)

`WithCheck` branches the pipeline into an `IWithPipeline<T>` that gates the next step on a predicate:

| Method | Description |
|---|---|
| `.WithMessage(string)` | Overrides the default validation failure message |
| `.Invoke<TNext>(Func<Task<TNext?>>)` | Continues if predicate passes; loads next entity (no input) |
| `.Invoke<TNext>(Func<T, Task<TNext?>>)` | Continues if predicate passes; loads next entity (current as input) |
| `.Invoke(Action<T>)` | Continues if predicate passes; applies synchronous mutation |
| `.Invoke(Func<Task>)` | Continues if predicate passes; runs async guard (exception-safe) |
| `.Save(Func<T, Task>)` | Continues if predicate passes; persists entity |
| `.ToResultAsync()` | Terminates the pipeline from this check point |

> **Important:** `WithCheck` (and `WithMessage`) only *configure* the predicate — the check step is not registered into the pipeline until one of the `Invoke` or `Save` overloads is called. Calling `ToResultAsync()` directly on `IWithPipeline<T>` without a continuation will silently bypass the predicate and return the current entity as a success. Always follow `WithCheck` with a continuation.

---

### Guard Pipelines

**`IGuardPipeline`** — wraps an async boolean check:

| Method | Description |
|---|---|
| `.With(Func<bool, bool>)` | Evaluates the guard result against a predicate (e.g. `r => r == true`) |

**`IGuardWithPipeline`** — produced by `.With(...)`:

| Method | Description |
|---|---|
| `.WithMessage(string)` | Sets the failure message if the predicate is not satisfied |
| `.Invoke<TNext>(Func<Task<TNext?>>)` | Continues to entity load if guard passed |

**`IExceptionGuardPipeline`** — wraps a fire-and-forget async task:

| Method | Description |
|---|---|
| `.WithMessage(string)` | Sets the failure message if the task throws |
| `.Invoke<TNext>(Func<Task<TNext?>>)` | Continues to entity load if guard succeeded |

---

### `ICommandResult<T>`

The result returned by every pipeline:

| Member | Description |
|---|---|
| `Entity` | The resolved entity, or `null` on failure or missing |
| `Errors` | Read-only list of `Notification` entries |
| `HasErrors` | `true` if any errors were recorded |
| `NotFound` | `true` if a pipeline step returned `null` |

Static factory methods (on `CommandResult<T>` and `CollectionResult<T>`):

| Factory | Description |
|---|---|
| `.Success(entity)` | Entity resolved successfully |
| `.Missing()` | Entity could not be found |
| `.Failed(errors)` | One or more errors occurred |

---

### `PipelineGuard`

A set of reusable predicate factories for use with `.WithCheck(...)`:

| Method | Description |
|---|---|
| `PipelineGuard.NotNull<T>()` | Returns `true` if the entity is not `null` |
| `PipelineGuard.NotEmpty<T, TKey>(selector)` | Returns `true` if the selected key is not the default value |

```csharp
// WithCheck must always be followed by Invoke or Save — the predicate step
// is only registered when a continuation is provided.
.WithCheck(PipelineGuard.NotNull<Order>())
    .WithMessage("Order not found.")
    .Invoke(order => repo.FindLineItemsAsync(order.Id))

.WithCheck(PipelineGuard.NotEmpty<Order, Guid>(o => o.Id))
    .WithMessage("Order has no ID.")
    .Save(order => repo.SaveAsync(order))
```

---

## Architecture Overview

```
CommandHandler<TCommand, T>
│
├── Invoke(Func<Task<T?>>)         ──► IHandlerPipeline<T>
│                                        ├── .Invoke<TNext>(...)
│                                        ├── .Invoke(Action<T>)
│                                        ├── .Invoke(Func<Task>)
│                                        ├── .WithCheck(predicate) ──► IWithPipeline<T>
│                                        │                                ├── .WithMessage(msg)
│                                        │                                └── .Invoke / .Save  ← continuation required
│                                        │                                    (ToResultAsync without a continuation bypasses the predicate)
│                                        ├── .Save(Func<T, Task>)
│                                        └── .ToResultAsync() ──► ICommandResult<T>
│                                                                      ├── .Success(entity)
│                                                                      ├── .Missing()
│                                                                      └── .Failed(errors)
│
├── Invoke(Func<Task<bool>>)       ──► IGuardPipeline
│                                        └── .With(predicate) ──► IGuardWithPipeline
│                                                                      ├── .WithMessage(msg)
│                                                                      └── .Invoke<TNext>(...) ──► IHandlerPipeline<TNext>
│
└── Invoke(Func<Task>)             ──► IExceptionGuardPipeline
                                         ├── .WithMessage(msg)
                                         └── .Invoke<TNext>(...) ──► IHandlerPipeline<TNext>
```

---

## Project Structure

```
Bllueprint.Core.Application/
├── CommandHandler.cs                     # Base class for MediatR command handlers
├── ICommandResult.cs                     # Public result interface
├── CommandResult.cs                      # Single-entity result implementation
├── CollectionResult.cs                   # Collection result implementation
├── FailureDetail.cs                      # Internal failure record passed to PipelineContext
├── PipelineContext.cs                    # Scoped error accumulator wrapping INotificationContext
├── PipelineGuard.cs                      # Reusable predicate factories
├── HandlerPipeline.cs                    # Core pipeline step executor
├── WithPipeline.cs                       # Predicate-gated pipeline branch
├── GuardPipeline.cs                      # Boolean guard step
├── GuardWithPipeline.cs                  # Guard predicate evaluator
├── ExceptionGuardPipeline.cs             # Exception-safe fire-and-forget guard
├── IHandlerPipeline.cs                   # Fluent pipeline interface
├── IWithPipeline.cs                      # Predicate check interface
├── IGuardPipeline.cs                     # Boolean guard interface
├── IGuardWithPipeline.cs                 # Guard predicate interface
└── IExceptionGuardPipeline.cs            # Exception guard interface
```

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

## Author

**Nubaum** — [github.com/bllueprint](https://github.com/bllueprint)