using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application;

internal record CommandResult<T> : ICommandResult<T>
{
    public T? Entity { get; init; }

    public IReadOnlyList<Notification> Errors { get; init; } = [];

    public bool NotFound { get; init; }

    public bool HasErrors => Errors.Count > 0;

    public static CommandResult<T> Success(T entity) =>
        new() { Entity = entity };

    public static CommandResult<T> Missing() =>
        new() { NotFound = true };

    public static CommandResult<T> Failed(IEnumerable<Notification> errors) =>
        new() { Errors = [.. errors] };
}
