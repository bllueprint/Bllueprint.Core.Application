using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application;

internal record CollectionResult<T> : ICommandResult<IEnumerable<T>>
{
    public IEnumerable<T>? Entity { get; init; }

    public IReadOnlyList<Notification> Errors { get; init; } = [];

    public bool NotFound { get; init; }

    public bool HasErrors => Errors.Count > 0;

    public static CollectionResult<T> Success(IEnumerable<T> items) =>
        new() { Entity = items };

    public static CollectionResult<T> Missing() =>
        new() { NotFound = true };

    public static CollectionResult<T> Failed(IEnumerable<Notification> errors) =>
        new() { Errors = [.. errors] };
}
